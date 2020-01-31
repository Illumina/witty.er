using Ilmn.Das.App.Wittyer.Utilities;
using Ilmn.Das.Std.AppUtils.Intervals;
using Ilmn.Das.Std.BioinformaticUtils.Contigs;
using Ilmn.Das.Std.BioinformaticUtils.GenomicFeatures;
using JetBrains.Annotations;
using Xunit;

namespace Ilmn.Das.App.Wittyer.Test
{
    public class IntervalUtilsTest
    {
        [Theory]
        [InlineData("chr1", 1000, "chr1", 7000, -100, 200, 0.05, 500, 700, 1300)]
        [InlineData("chr1", 1, "chr1", 7000, -100, 200, 0.05, 500, 1, 351)]
        [InlineData("chr1", 1000, "chr10", 7000, -100, 200, 0.05, 500, 500, 1500)]
        [InlineData("chr1", 200, "chr10", 7000, -100, 700, 0.05, 500, 1, 900)]
        public void CalculateBndBorderDistanceWorks([NotNull] string chr, uint position, [NotNull] string otherChr, uint otherPos,
            int ciStart, int ciEnd, double percentDistance, uint basepairDistance, uint borderStart, uint borderEnd)
        {
            var thisPos = ContigAndPosition.Create(ContigInfo.Create(chr), position);
            var otherPosition = ContigAndPosition.Create(ContigInfo.Create(otherChr), otherPos);
            var ci = new InclusiveInterval<int>(ciStart, ciEnd);

            var actualCi = thisPos.CalculateBndBorderInterval(otherPosition, ci, percentDistance, basepairDistance);
            var expectedCi = ContigAndInterval.Create(ContigInfo.Create(chr), borderStart, borderEnd);

            Assert.Equal(expectedCi, actualCi);
        }
 

        [Theory]
        [InlineData(1, 1000, 0, 1, 0.05, 500, 1, 51, false)]
        [InlineData(1, 10000, -200, 600, 0.1, 500, 1, 601, false)]
        [InlineData(1, 10000, -200, 600, 0.1, 500, 9500, 10600, true)]
        public void CalculateBorderDistanceWorks(uint position, uint end, int ciStart, int ciEnd,
            double percentDistance, uint basepairDistance, uint borderStart, uint borderEnd, bool isEnd)
        {
            var baseInterval = new InclusiveInterval<uint>(position, end);
            var ci = new InclusiveInterval<int>(ciStart, ciEnd);

            var target = isEnd ? end : position;
            var actualInterval = target.CalculateBorderInterval(baseInterval, ci, percentDistance, basepairDistance);

            var expectedInterval = new InclusiveInterval<uint>(borderStart, borderEnd);
            Assert.Equal(expectedInterval, actualInterval);
        }
    }
}