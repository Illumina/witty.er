using System.Collections.Generic;
using System.Linq;
using Ilmn.Das.App.Wittyer.Utilities;
using Ilmn.Das.Std.AppUtils.Intervals;
using Ilmn.Das.Std.XunitUtils;
using Xunit;

namespace Ilmn.Das.App.Wittyer.Test
{
    public class MergedIntervalTreeTest
    {
        public class ClosedOpen
        {
            // these unit tests assume that you have a list of ordered intervals that are non-overlapping
            // and none start with MinValue and none stop at MaxValue
            public static readonly IList<IInterval<uint>> ExistingIntervals = new List<IInterval<uint>>
            (
                new IInterval<uint>[]
                {
                    new ClosedOpenInterval<uint>(10, 15),
                    new ClosedOpenInterval<uint>(19, 20),
                    new ClosedOpenInterval<uint>(25, 26)
                }
            );

            public static readonly MergedIntervalTree<uint> ExistingTree = MergedIntervalTree.Create(ExistingIntervals);

            [Fact]
            public void InsertSameTreeIntoTree()
            {
                var tree = MergedIntervalTree.Create(ExistingIntervals);
                foreach (var target in ExistingIntervals)
                    tree.Add(target);

                // non-overlapping should have one more interval
                MultiAssert.True(ExistingIntervals.SequenceEqual(tree));

                // subtract back the stuff we added to get leftovers.
                var fpTree = MergedIntervalTree.Create<uint>();
                foreach (var interval in tree)
                {
                    var overlaps = ExistingTree.Search(interval).ToList();
                    if (overlaps.Count == 0)
                        fpTree.Add(interval);
                    else
                        fpTree.AddRange(interval.Subtract(overlaps));
                }

                // since we added the same, leftovers should be none.
                MultiAssert.False(fpTree.Any());

                MultiAssert.AssertAll();
            }

            [Fact]
            public void InsertSameTreeSlightlyExpandedIntervalsIntoTree()
            {
                var tree = MergedIntervalTree.Create(ExistingIntervals);
                var newIntervals = new List<IInterval<uint>>();
                foreach (var target in ExistingIntervals)
                {
                    var interval = ClosedOpenInterval<uint>.Create(target.Start - 1, target.IsStartInclusive,
                        target.Stop + 1, target.IsStopInclusive);
                    tree.Add(interval);
                    newIntervals.Add(interval);
                }

                // non-overlapping should have one more interval
                MultiAssert.True(newIntervals.SequenceEqual(tree));

                // subtract back the stuff we added to get leftovers.
                var fpTree = MergedIntervalTree.Create<uint>();
                foreach (var interval in tree)
                {
                    var overlaps = ExistingTree.Search(interval).ToList();
                    if (overlaps.Count == 0)
                        fpTree.Add(interval);
                    else
                        fpTree.AddRange(interval.Subtract(overlaps));
                }

                // double since we basically add a 1-length interval on each side.
                MultiAssert.Equal(ExistingIntervals.Count * 2, fpTree.Count());

                MultiAssert.AssertAll();
            }

            [Fact]
            public void InsertNonOverlappingAtMiddle()
            {
                var tree = MergedIntervalTree.Create(ExistingIntervals);
                var target = new ClosedOpenInterval<uint>(ExistingIntervals[0].Stop, ExistingIntervals[1].Start);
                tree.Add(target);

                // non-overlapping should have one more interval
                MultiAssert.Equal(ExistingIntervals.Count + 1, tree.Count());

                // subtract back the stuff we added to get leftovers.
                var fpTree = MergedIntervalTree.Create<uint>();
                foreach (var interval in tree)
                {
                    var overlaps = ExistingTree.Search(interval).ToList();
                    if (overlaps.Count == 0)
                        fpTree.Add(interval);
                    else
                        fpTree.AddRange(interval.Subtract(overlaps));
                }

                MultiAssert.Equal(1, fpTree.Count());
                MultiAssert.Equal(target.GetLength(), fpTree.FirstOrDefault()?.GetLength());

                MultiAssert.AssertAll();
            }

            [Fact]
            public void InsertNonOverlappingAtBeginning()
            {
                var tree = MergedIntervalTree.Create(ExistingIntervals);
                var target = new ClosedOpenInterval<uint>(uint.MinValue, ExistingIntervals[0].Start);
                tree.Add(target);

                // non-overlapping should have one more interval
                MultiAssert.Equal(ExistingIntervals.Count + 1, tree.Count());
                MultiAssert.Equal(tree.First().Start, target.Start);
                MultiAssert.Equal(tree.First().Stop, target.Stop);

                // subtract back the stuff we added to get leftovers.
                var fpTree = MergedIntervalTree.Create<uint>();
                foreach (var interval in tree)
                {
                    var overlaps = ExistingTree.Search(interval).ToList();
                    if (overlaps.Count == 0)
                        fpTree.Add(interval);
                    else
                        fpTree.AddRange(interval.Subtract(overlaps));
                }

                MultiAssert.Equal(1, fpTree.Count());
                MultiAssert.Equal(target.GetLength(), fpTree.FirstOrDefault()?.GetLength());

                MultiAssert.AssertAll();
            }

            [Fact]
            public void InsertIntervalBarelyOverlappingFirstTwoInterval()
            {
                var tree = MergedIntervalTree.Create(ExistingIntervals);
                var target =
                    new ClosedOpenInterval<uint>(ExistingIntervals[0].Stop - 1, ExistingIntervals[1].Start + 1);
                tree.Add(target);

                // insert one, but remove 2 would be net of - 1
                MultiAssert.Equal(ExistingIntervals.Count - 1, tree.Count());
                MultiAssert.Equal(ExistingIntervals.First().Start, tree.First().Start);

                // overlapping the two first ones means the second's stop is  the stop of first in tree
                MultiAssert.Equal(ExistingIntervals.Skip(1).First().Stop, tree.First().Stop);

                // subtract back the stuff we added to get leftovers.
                var fpTree = MergedIntervalTree.Create<uint>();
                foreach (var interval in tree)
                {
                    var overlaps = ExistingTree.Search(interval).ToList();
                    if (overlaps.Count == 0)
                        fpTree.Add(interval);
                    else
                        fpTree.AddRange(interval.Subtract(overlaps));
                }

                // since only 2 overlapped, those 2 get removed, while 1 gets added.
                MultiAssert.Equal(1, fpTree.Count());
                // gets trimmed by 2 since we overlap 2 intervals
                MultiAssert.Equal(target.GetLength() - 2, fpTree.FirstOrDefault()?.GetLength());

                MultiAssert.AssertAll();
            }

            [Fact]
            public void InsertNonOverlappingAtEnd()
            {
                var tree = MergedIntervalTree.Create(ExistingIntervals);
                var target = new ClosedOpenInterval<uint>(ExistingIntervals.Last().Stop, uint.MaxValue);
                tree.Add(target);
                MultiAssert.Equal(ExistingIntervals.Count + 1, tree.Count());
                MultiAssert.Equal(target, tree.Last());

                // subtract back the stuff we added to get leftovers.
                var fpTree = MergedIntervalTree.Create<uint>();
                foreach (var interval in tree)
                {
                    var overlaps = ExistingTree.Search(interval).ToList();
                    if (overlaps.Count == 0)
                        fpTree.Add(interval);
                    else
                        fpTree.AddRange(interval.Subtract(overlaps));
                }

                MultiAssert.Equal(1, fpTree.Count());
                MultiAssert.Equal(target.GetLength(), fpTree.FirstOrDefault()?.GetLength());

                MultiAssert.AssertAll();
            }

            [Fact]
            public void InsertTotallyOverlappingBecomesOneInterval()
            {
                var tree = MergedIntervalTree.Create(ExistingIntervals);
                var target =
                    new ClosedOpenInterval<uint>(ExistingIntervals[0].Stop - 1, ExistingIntervals.Last().Start + 1);
                tree.Add(target);
                MultiAssert.Equal(1, tree.Count());
                MultiAssert.Equal(ExistingIntervals.First().Start, tree.First().Start);
                MultiAssert.Equal(ExistingIntervals.Last().Stop, tree.Last().Stop);


                // subtract back the stuff we added to get leftovers.
                var fpTree = MergedIntervalTree.Create<uint>();
                foreach (var interval in tree)
                {
                    var overlaps = ExistingTree.Search(interval).ToList();
                    if (overlaps.Count == 0)
                        fpTree.Add(interval);
                    else
                        fpTree.AddRange(interval.Subtract(overlaps));
                }

                // should be one less since it's the # of gaps
                MultiAssert.Equal(ExistingIntervals.Count - 1, fpTree.Count());

                MultiAssert.AssertAll();
            }
        }

        public class Inclusive
        {
            // these unit tests assume that you have a list of ordered intervals that are non-overlapping (separated by at least 3)
            // and none start with MinValue and none stop at MaxValue
            public readonly IList<IInterval<uint>> ExistingIntervals = new List<IInterval<uint>>
            (
                new IInterval<uint>[]
                {
                    new InclusiveInterval<uint>(10, 15),
                    new InclusiveInterval<uint>(19, 19),
                    new InclusiveInterval<uint>(25, 26)
                }
            );

            [Fact]
            public void InsertSameTreeIntoTree()
            {
                var tree = MergedIntervalTree.Create(ExistingIntervals);
                foreach (var target in ExistingIntervals)
                    tree.Add(target);

                // non-overlapping should have one more interval
                Assert.True(ExistingIntervals.SequenceEqual(tree));
            }

            [Fact]
            public void InsertSameTreeSlightlyExpandedIntervalsIntoTree()
            {
                var tree = MergedIntervalTree.Create(ExistingIntervals);
                var newIntervals = new List<IInterval<uint>>();
                foreach (var target in ExistingIntervals)
                {
                    var interval = ClosedOpenInterval<uint>.Create(target.Start - 1, target.IsStartInclusive, target.Stop + 1, target.IsStopInclusive);
                    tree.Add(interval);
                    newIntervals.Add(interval);
                }

                // non-overlapping should have one more interval
                Assert.True(newIntervals.SequenceEqual(tree));
            }

            [Fact]
            public void InsertNonOverlappingAtMiddle()
            {
                var tree = MergedIntervalTree.Create(ExistingIntervals);
                var target = new InclusiveInterval<uint>(ExistingIntervals[0].Stop + 1, ExistingIntervals[1].Start - 1);
                tree.Add(target);

                // non-overlapping should have one more interval
                Assert.Equal(ExistingIntervals.Count + 1, tree.Count());
            }

            [Fact]
            public void InsertNonOverlappingAtBeginning()
            {
                var tree = MergedIntervalTree.Create(ExistingIntervals);
                var target = new InclusiveInterval<uint>(uint.MinValue, ExistingIntervals[0].Start - 1);
                tree.Add(target);

                // non-overlapping should have one more interval
                MultiAssert.Equal(ExistingIntervals.Count + 1, tree.Count());
                MultiAssert.Equal(tree.First().Start, target.Start);
                MultiAssert.Equal(tree.First().Stop, target.Stop);
                MultiAssert.AssertAll();
            }

            [Fact]
            public void InsertIntervalBarelyOverlappingFirstTwoInterval()
            {
                var tree = MergedIntervalTree.Create(ExistingIntervals);
                var target = new InclusiveInterval<uint>(ExistingIntervals[0].Stop, ExistingIntervals[1].Start);
                tree.Add(target);

                // insert one, but remove 2 would be net of - 1
                MultiAssert.Equal(ExistingIntervals.Count - 1, tree.Count());
                MultiAssert.Equal(ExistingIntervals.First().Start, tree.First().Start);

                // overlapping the two first ones means the second's stop is  the stop of first in tree
                MultiAssert.Equal(ExistingIntervals.Skip(1).First().Stop, tree.First().Stop);
                MultiAssert.AssertAll();
            }

            [Fact]
            public void InsertNonOverlappingAtEnd()
            {
                var tree = MergedIntervalTree.Create(ExistingIntervals);
                var target = new InclusiveInterval<uint>(ExistingIntervals.Last().Stop + 1, uint.MaxValue);
                tree.Add(target);
                MultiAssert.Equal(ExistingIntervals.Count + 1, tree.Count());
                MultiAssert.Equal(target, tree.Last());
                MultiAssert.AssertAll();
            }

            [Fact]
            public void InsertTotallyOverlappingBecomesOneInterval()
            {
                var tree = MergedIntervalTree.Create(ExistingIntervals);
                var target = new InclusiveInterval<uint>(ExistingIntervals[0].Stop, ExistingIntervals.Last().Start);
                tree.Add(target);
                MultiAssert.Equal(1, tree.Count());
                MultiAssert.Equal(ExistingIntervals.First().Start, tree.First().Start);
                MultiAssert.Equal(ExistingIntervals.Last().Stop, tree.Last().Stop);
                MultiAssert.AssertAll();
            }
        }
    }
}
