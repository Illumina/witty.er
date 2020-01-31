using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Ilmn.Das.Std.AppUtils.Intervals;
using JetBrains.Annotations;

namespace Ilmn.Das.App.Wittyer.Utilities
{
    public static class MergedIntervalTree
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="MergedIntervalTree{T}"/> class.
        /// </summary>
        /// <param name="intervals">The intervals to initialize the tree with.</param>
        /// <returns></returns>
        [NotNull]
        public static MergedIntervalTree<T> Create<T>([NotNull, ItemNotNull] IEnumerable<IInterval<T>> intervals)
            where T : IComparable<T>
            => MergedIntervalTree<T>.Create(intervals);

        /// <summary>
        /// Initializes an empty instance of the <see cref="MergedIntervalTree{T}"/> class.
        /// </summary>
        /// <returns></returns>
        [NotNull]
        public static MergedIntervalTree<T> Create<T>(params IInterval<T>[] intervals)
            where T : IComparable<T>
            => MergedIntervalTree<T>.Create(intervals);
    }

    public class MergedIntervalTree<T> : IIntervalTree<T, IInterval<T>>
        where T : IComparable<T>
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
        [NotNull]
        internal static MergedIntervalTree<T> Create([CanBeNull, ItemNotNull] IEnumerable<IInterval<T>> intervals)
        {
            var ret = new MergedIntervalTree<T>();

            if (intervals == null) return ret;

            foreach (var interval in intervals)
                ret.Add(interval);
            return ret;
        }

        /// <inheritdoc />
        public IEnumerator<IInterval<T>> GetEnumerator() => _tree.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        #region Implementation of IIntervalTree<T,IInterval<T>>

        /// <inheritdoc />
        public void Add(IInterval<T> interval)
        {
            var overlapping = _tree.Search(interval);

            var (start, isStartInclusive, stop, isStopInclusive) = interval;
            var starts = (start, isStartInclusive);
            var stops = (stop, isStopInclusive);
            foreach (var overlap in overlapping)
            {
                _tree.Remove(overlap);
                var (oStart, oStartInclusive, oStop, oStopInclusive) = overlap;
                starts = Compare(in starts, (oStart, oStartInclusive), true);
                stops = Compare(in stops, (oStop, oStopInclusive), false);
            }

            var isSame = Equals(starts.start, interval.Start)
                         && Equals(stops.stop, interval.Stop)
                         && starts.isStartInclusive == interval.IsStartInclusive
                         && stops.isStopInclusive == interval.IsStopInclusive;

            if (!isSame)
                interval = Interval<T>.Create(starts.start, starts.isStartInclusive, stops.stop, stops.isStopInclusive);

            _tree.Add(interval);
            
            (T value, bool isInclusive) Compare(in (T value, bool isInclusive) first,
                in (T value, bool isInclusive) second, bool wantsSmaller)
            {
                var compare = first.value.CompareTo(second.value);
                if (compare < 0) return wantsSmaller ? first : second; // second is greater
                if (compare > 0) return wantsSmaller ? second : first; // first is greater
                return first.isInclusive ? first : second; // equal, so whatever is inclusive, return that one.
            }
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