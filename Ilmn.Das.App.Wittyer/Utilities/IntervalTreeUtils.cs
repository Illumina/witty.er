using System;
using System.Collections.Generic;
using System.Linq;
using Ilmn.Das.Core.Tries.Extensions;
using Ilmn.Das.Std.AppUtils.Intervals;
using Ilmn.Das.Std.BioinformaticUtils.Contigs;
using JetBrains.Annotations;

namespace Ilmn.Das.App.Wittyer.Utilities
{
    /// <summary>
    /// Utilities for <see cref="IIntervalTree{TBounds,TInterval}"/>
    /// </summary>
    public static class IntervalTreeUtils
    {
        public static IEnumerable<IInterval<T>> Merge<T>(this IEnumerable<IInterval<T>> source) where T : IComparable<T>
            => MergedIntervalTree<T>.MergeInternal(source, false);

        /// <summary>
        /// Converts the given IEnumerable of Intervals into a <see cref="MergedIntervalTree"/>.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="intervals">The intervals.</param>
        /// <param name="createCopy">if set to <c>false</c>, this will return the same object if the intervals is already a <see cref="MergedIntervalTree"/>.  Otherwise, it will create a copy no matter what.</param>
        /// <returns></returns>
        [Pure]
        public static MergedIntervalTree<T> ToMergedIntervalTree<T>(
            this IEnumerable<IInterval<T>> intervals, bool createCopy = false)
            where T : IComparable<T>
            => createCopy
                ? MergedIntervalTree.Create(intervals)
                : intervals as MergedIntervalTree<T> ?? MergedIntervalTree.Create(intervals);

        internal static uint GetTotalLength<T>(this IEnumerable<KeyValuePair<IContigInfo, T>> intervals) 
            where T : IEnumerable<IInterval<uint>>
            => (uint) intervals.Select(kvp => kvp.Value.GetTotalMergedLength()).Sum();

        /// <summary>
        /// Gets the total length of all the intervals after merging overlapping ones.
        /// </summary>
        /// <param name="intervals">The intervals.</param>
        /// <returns></returns>
        public static long GetTotalMergedLength(this IEnumerable<IInterval<uint>> intervals)
            => GetTotalLength(intervals.ToMergedIntervalTree());

        /// <summary>
        /// Gets the total length of the <see cref="MergedIntervalTree{UInt32}"/>.
        /// </summary>
        /// <param name="tree">The tree.</param>
        /// <returns></returns>
        public static long GetTotalLength(this MergedIntervalTree<uint> tree)
            => tree.Sum(i => i.GetLength());

        /// <summary>
        /// Subtracts the specified intervals from the source interval.
        /// </summary>
        /// <param name="source">The source.</param>
        /// <param name="others">The others.</param>
        /// <returns></returns>
        public static IEnumerable<IInterval<uint>> Subtract(this IInterval<uint> source,
            IEnumerable<IInterval<uint>> others)
        {
            // need to order the intervals so using an intervaltree for this
            others = (others as IIntervalTree<uint, IInterval<uint>> ?? others.ToIntervalTree<uint, IInterval<uint>>()).Search(source); 
            
            var returnFinalTarget = true;
            foreach (var other in others)
            {
                var overlap = source.TryGetOverlap(other).GetOrDefault();
                if (overlap == null) // sanity check, but since we search a tree, this should never happen.
                    continue;

                IInterval<uint> firstOne = null, secondOne = null;
                // subtraction only gives a result of 0, 1, or 2 returns, everything else is impossible.
                foreach (var leftover in source.SubtractOverlap(overlap))
                {
                    if (firstOne == null) // means first time in loop
                        firstOne = leftover;
                    else
                        secondOne = leftover;
                }

                if (firstOne == null) // means there was no leftovers
                {
                    // if this is the last one, then we should not release the final one.
                    returnFinalTarget = false;
                    continue;
                }

                // some leftover stuff, so should release those.
                returnFinalTarget = true;
                if (secondOne == null) // means only one leftover, we just continue subtracting.
                    source = firstOne;
                else // means 2 leftover, which means we yield the first one and then continue subtracting from the second one.
                {
                    yield return firstOne;
                    source = secondOne;
                }
            }

            if (returnFinalTarget)
                yield return source; // return last one or only one if no loops
        }

        internal static IEnumerable<IInterval<T>> SubtractOverlap<T>(this IInterval<T> target,
            IInterval<T> overlap) where T : IComparable<T>
        {
            var startComparison = target.Start.CompareTo(overlap.Start);

            if (startComparison < 0)
                yield return Interval<T>.Create(target.Start, target.IsStartInclusive, overlap.Start,
                    !overlap.IsStartInclusive);
            else if (startComparison == 0 && target.IsStartInclusive != overlap.IsStartInclusive)
                yield return new InclusiveInterval<T>(target.Start, target.Start);

            var stopComparison = target.Stop.CompareTo(overlap.Stop);

            if (stopComparison > 0)
                yield return Interval<T>.Create(overlap.Stop, !overlap.IsStopInclusive, target.Stop,
                    target.IsStopInclusive);
            else if (stopComparison == 0 && target.IsStopInclusive != overlap.IsStopInclusive)
                yield return new InclusiveInterval<T>(target.Stop, target.Stop);
        }
    }
}
