using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Ilmn.Das.Std.AppUtils.Intervals;
using Ilmn.Das.Std.AppUtils.Misc;

namespace Ilmn.Das.App.Wittyer.Utilities
{
    /// <summary>
    /// The static companion class for <see cref="MergedIntervalTree{T}"/>
    /// </summary>
    public static class MergedIntervalTree
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="MergedIntervalTree{T}"/> class.
        /// </summary>
        /// <param name="intervals">The intervals to initialize the tree with.</param>
        /// <returns></returns>
        public static MergedIntervalTree<T> Create<T>(IEnumerable<IInterval<T>> intervals)
            where T : IComparable<T>
            => MergedIntervalTree<T>.Create(intervals);

        /// <summary>
        /// Initializes an empty instance of the <see cref="MergedIntervalTree{T}"/> class.
        /// </summary>
        /// <returns></returns>
        public static MergedIntervalTree<T> Create<T>(params IInterval<T>[] intervals)
            where T : IComparable<T>
            => MergedIntervalTree<T>.Create(intervals);

        /// <summary>
        /// Initializes an empty instance of the <see cref="MergedIntervalTree{T}"/> class.
        /// </summary>
        /// <returns></returns>
        internal static MergedIntervalTree<T> CreateAlreadyMerged<T>(IEnumerable<IInterval<T>> intervals)
            where T : IComparable<T>
        {
            var ret = MergedIntervalTree<T>.Create(null);
            foreach (var interval in intervals)
                ret.AddInternal(interval);

            return ret;
        }
    }

    /// <inheritdoc />
    /// <summary>
    /// An IntervalTree that basically keeps only one interval per region (merges overlapping intervals into a single one)
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class MergedIntervalTree<T> : IIntervalTree<T, IInterval<T>>
        where T : IComparable<T> // TODO: make this generic enough to take any intervals.  Probably need to take in a merge function.
    {
        private readonly IIntervalTree<T, IInterval<T>> _tree = new IntervalTree<T>();

        private MergedIntervalTree()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="MergedIntervalTree{T}"/> class.
        /// </summary>
        /// <param name="intervals">The intervals to initialize the tree with.</param>
        /// <returns></returns>
        internal static MergedIntervalTree<T> Create(IEnumerable<IInterval<T>>? intervals)
        {
            var ret = new MergedIntervalTree<T>();
            if (intervals == null)
                return ret;

            foreach (var interval in MergeInternal(intervals, false))
                ret.AddInternal(interval);
            return ret;
        }

        /// <inheritdoc />
        public IEnumerator<IInterval<T>> GetEnumerator() => _tree.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        #region Implementation of IIntervalTree<T,IInterval<T>>

        /// <inheritdoc />
        public void Add(IInterval<T> interval)
        {
            var overlapping = new HashSet<IInterval<T>>();
            overlapping.Add(interval);
            var removal = new List<IInterval<T>>();
            foreach (var candidate in _tree)
            {
                if (interval.IsOverlapping(candidate))
                {
                    removal.Add(candidate);
                    overlapping.Add(candidate);
                }
                else if (IsImmediatelyAdjacent(interval, candidate))
                    overlapping.Add(candidate);
            }
            foreach (var candidate in removal)
                _tree.Remove(candidate);
            
            foreach (var merged in MergeInternal(interval.FollowedBy(overlapping), true))
                AddInternal(merged);
        }
        
        internal static IEnumerable<IInterval<T>> MergeInternal(IEnumerable<IInterval<T>> source, bool checkedOverlapping)
        {
            IInterval<T>? previousInterval = null;
            var orderedEnumerable = source.OrderBy(x => x.Start).ThenBy(x => x.Stop).ToList();
            foreach (var interval in orderedEnumerable)
            {
                if (previousInterval is null)
                {
                    previousInterval = interval;
                    continue;
                }

                if (!checkedOverlapping && !previousInterval.IsOverlapping(interval) && !IsImmediatelyAdjacent(previousInterval, interval))
                {
                    yield return previousInterval;
                    previousInterval = interval;
                    continue;
                }

                var (start, isStartInclusive, stop, isStopInclusive) = previousInterval;
                var starts = (start, isStartInclusive);
                var stops = (stop, isStopInclusive);
                starts = Compare(in starts, (interval.Start, interval.IsStartInclusive), true);
                stops = Compare(in stops, (interval.Stop, interval.IsStopInclusive), false);

                var isSame = Equals(starts.start, interval.Start)
                             && Equals(stops.stop, interval.Stop)
                             && starts.isStartInclusive == interval.IsStartInclusive
                             && stops.isStopInclusive == interval.IsStopInclusive;

                if (!isSame)
                    previousInterval = Interval<T>.Create(starts.start, starts.isStartInclusive, stops.stop,
                        stops.isStopInclusive);
            }
            if (previousInterval != null)
                yield return previousInterval;
        }
        
        internal void AddInternal(IInterval<T> interval) => _tree.Add(interval);

        private static (T value, bool isInclusive) Compare(in (T value, bool isInclusive) first,
                in (T value, bool isInclusive) second, bool wantsSmaller)
        {
            var compare = first.value.CompareTo(second.value);
            if (compare < 0) return wantsSmaller ? first : second; // second is greater
            if (compare > 0) return wantsSmaller ? second : first; // first is greater
            return first.isInclusive ? first : second; // equal, so whatever is inclusive, return that one.
        }

        private static bool IsImmediatelyAdjacent(IInterval<T> interval1, IInterval<T> interval2)
        {
            var (start, isStartInclusive, stop, isStopInclusive) = interval1;
            return (isStartInclusive || interval2.IsStopInclusive) && Equals(interval2.Stop, start) ||
                   (isStopInclusive || interval2.IsStartInclusive) && Equals(interval2.Start, stop);
        }

        /// <inheritdoc />
        public void Remove(IInterval<T> interval) => _tree.Remove(interval);

        /// <inheritdoc />
        public IEnumerable<IInterval<T>> Search(T val) => _tree.Search(val);

        /// <inheritdoc />
        public IEnumerable<IInterval<T>> Search(IInterval<T> i) => _tree.Search(i);

        /// <inheritdoc />
        public IInterval<T> SearchFirstOverlapping(IInterval<T> interval)
            => Search(interval).FirstOrDefault() 
               ?? throw new InvalidOperationException("Called SearchFirstOverlapping when no overlap!");

        #endregion
    }
}