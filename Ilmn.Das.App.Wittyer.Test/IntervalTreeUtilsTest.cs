using System.Collections.Generic;
using System.Linq;
using Ilmn.Das.App.Wittyer.Utilities;
using Ilmn.Das.Std.AppUtils.Intervals;
using Ilmn.Das.Std.AppUtils.Misc;
using Ilmn.Das.Std.BioinformaticUtils.Bed;
using Ilmn.Das.Std.XunitUtils;
using JetBrains.Annotations;
using Xunit;

namespace Ilmn.Das.App.Wittyer.Test
{
    public class IntervalTreeUtilsTest
    {
        [Fact]
        public static void GetMergedIntervalWorks()
        {
            var tree = GetOriginalTree();
            tree.Add(new InclusiveInterval<uint>(15, 17));

            var mergedIntervals = tree.ToMergedIntervalTree();

            Assert.Equal(3, mergedIntervals.Count());
        }

        [Fact]
        public static void MergeMultipleIntervalWorks()
        {
            var tree = GetOriginalTree();
            tree.Add(new InclusiveInterval<uint>(9, 30));

            var mergedIntervals = tree.ToMergedIntervalTree();

            MultiAssert.Equal(1, mergedIntervals.Count());
            MultiAssert.Equal(22L, tree.GetTotalMergedLength());
            MultiAssert.AssertAll();

        }

        public static TheoryData<IInterval<uint>, IEnumerable<IInterval<uint>>> SubstractIntervalData 
            = new TheoryData<IInterval<uint>, IEnumerable<IInterval<uint>>>
            {
                {new InclusiveInterval<uint>(12, 13),
                    new IInterval<uint>[]{new ClosedOpenInterval<uint>(10, 12), new OpenClosedInterval<uint>(13, 15)} },

                {new InclusiveInterval<uint>(12, 18),
                    new IInterval<uint>[]{new ClosedOpenInterval<uint>(10, 12)} },

                {new InclusiveInterval<uint>(10, 15), new List<IInterval<uint>>()},

                {new ExclusiveInterval<uint>(10, 15), new IInterval<uint>[]
                {
                    new ClosedOpenInterval<uint>(10, 11), new InclusiveInterval<uint>(15, 15)
                }},

                {new ClosedOpenInterval<uint>(10, 15),
                    new IInterval<uint>[]{new InclusiveInterval<uint>(15, 15)}},

                {new OpenClosedInterval<uint>(10, 15),
                new IInterval<uint>[]{ new ClosedOpenInterval<uint>(10, 11)}}
            };

        [Theory]
        [MemberData(nameof(SubstractIntervalData))]
        public static void SubstractIntervalWorks(IInterval<uint> interval, IEnumerable<IInterval<uint>> expected)
        {
            var target = new InclusiveInterval<uint>(10, 15);
            var actual = target.Subtract(interval.FollowedBy());

            Assert.Equal(expected, actual);
        }

        [Fact]
        public void SubtractMultipleIntervalsWorks()
        {
            var target = BedInterval.Create(10, 20);
            var subtracts = new[]
            {
                BedInterval.Create(16, 18), BedInterval.Create(19, 25), BedInterval.Create(27, 30),
                BedInterval.Create(2, 4), BedInterval.Create(8, 11), BedInterval.Create(13, 15)
            };

            var expectedLengths = new List<uint>
            {
                // [11,13)
                2,
                // [15,16)
                1,
                // [18,19)
                1
            };
            var intervals = target.Subtract(subtracts).ToList();
            Assert.True(expectedLengths.SequenceEqual(intervals.Select(i => i.GetLength())));
        }
        
        [Fact]
        public static void GetTotalLengthWorks()
        {
            var totalLength = GetOriginalTree().GetTotalMergedLength();

            Assert.Equal(16L, totalLength);
        }


        [NotNull]
        private static IntervalTree<uint, IInterval<uint>> GetOriginalTree()
        {
            var tree = new IntervalTree<uint, IInterval<uint>>
            {
                new InclusiveInterval<uint>(10, 15),
                new ExclusiveInterval<uint>(16, 20),
                new ClosedOpenInterval<uint>(21, 25),
                new OpenClosedInterval<uint>(27, 30)
            };

            return tree;
        }
    }
}
