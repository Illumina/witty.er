using Ilmn.Das.App.Wittyer.Utilities;
using Ilmn.Das.App.Wittyer.Utilities.Enums;
using Ilmn.Das.App.Wittyer.Vcf.Samples;
using Ilmn.Das.App.Wittyer.Vcf.Variants;
using Ilmn.Das.App.Wittyer.Vcf.Variants.Genotype;
using Ilmn.Das.Std.BioinformaticUtils.Contigs;
using Ilmn.Das.Std.BioinformaticUtils.GenomicFeatures;
using JetBrains.Annotations;
using Moq;
using Xunit;

namespace Ilmn.Das.App.Wittyer.Test
{
    public class OverlapUtilsTest
    {
        public static readonly IContigInfo PrimaryContig = ContigInfo.Create("chr5");

        public static readonly IContigAndInterval PrimaryContigAndInterval =
            ContigAndInterval.Create(PrimaryContig, 1050, 5600);

        public static readonly IContigAndInterval PrimaryFailedContigAndInterval =
            ContigAndInterval.Create(PrimaryContig, 100, 5200);

        public static readonly IContigInfo SecondaryContig = ContigInfo.Create("chr15");
        
        //public static readonly IContigAndInterval SecondaryContigAndInterval 

        [Theory] //1001, 1500, 5001, 5600
        [InlineData(true, 900, 1100, 5200, 5700, true, MatchEnum.AlleleAndGenotypeMatch, FailedReason.Unset)]
        [InlineData(false, 900, 1100, 5500, 5555, true, MatchEnum.LocalAndGenotypeMatch, FailedReason.FailedBoundary)]
        [InlineData(false, 100, 110, 5110, 5200, false, MatchEnum.LocalMatch, FailedReason.FailedBoundary)]
        [InlineData(true, 1000, 1200, 5100, 5700, false, MatchEnum.AlleleMatch, FailedReason.GtMismatch)]
        public void GenerateWhatAndWhyWorksForWittyerVariant(bool isAlleleMatch, uint posStart, uint posEnd, uint endStart, uint endEnd, 
            bool isGtMatch, MatchEnum matchResult, FailedReason reasonResult)
        {
            var originalVariant = GetOriginalCnvGtVariant();

            var otherVariant =
                SetupBasicVariant(isAlleleMatch ? PrimaryContigAndInterval : PrimaryFailedContigAndInterval);

            otherVariant.SetupGet(v => v.PosInterval).Returns(ContigAndInterval.Create(PrimaryContig, posStart, posEnd));
            otherVariant.SetupGet(v => v.EndInterval).Returns(ContigAndInterval.Create(PrimaryContig, endStart, endEnd));
            var otherSample = new Mock<IWittyerGenotypedSample>();

            var gt = new Mock<IGenotypeInfo>();
            if (originalVariant.Sample is IWittyerGenotypedSample gtSample)
            {
                gt.Setup(g => g.Equals(gtSample.Gt)).Returns(isGtMatch);
            }
            
            otherSample.SetupGet(s => s.Gt).Returns(gt.Object);

            otherVariant.SetupGet(v => v.Sample).Returns(otherSample.Object);

            var actual = otherVariant.Object.GenerateWhatAndWhy(originalVariant);
            Assert.Equal((matchResult, reasonResult), actual);
        }

        [Theory]
        [InlineData(true, 900, 1100, 5200, 5700, true, 3, MatchEnum.AlleleAndGenotypeMatch, FailedReason.Unset)]
        [InlineData(true, 900, 1100, 5200, 5700, true, 2, MatchEnum.LocalAndGenotypeMatch, FailedReason.CnMismatch)]
        [InlineData(false, 900, 1100, 5500, 5555, true, 3, MatchEnum.LocalAndGenotypeMatch, FailedReason.FailedBoundary)]
        [InlineData(false, 100, 110, 5110, 5200, false, 1, MatchEnum.LocalMatch, FailedReason.FailedBoundary)]
        [InlineData(true, 1000, 1200, 5100, 5700, false, 3, MatchEnum.AlleleMatch, FailedReason.GtMismatch)]
        [InlineData(true, 1000, 1200, 5100, 5700, false, 1, MatchEnum.LocalMatch, FailedReason.CnMismatch)]
        public void GenerateWhatAndWhyWorksWithCnSample(bool isAlleleMatch, uint posStart, uint posEnd, uint endStart, uint endEnd,
            bool isGtMatch, uint cn, MatchEnum matchResult, FailedReason reasonResult)
        {
            var originalVariant = GetOriginalCnvGtVariant();
            var otherVariant =
                SetupBasicVariant(isAlleleMatch ? PrimaryContigAndInterval : PrimaryFailedContigAndInterval);

            otherVariant.SetupGet(v => v.PosInterval).Returns(ContigAndInterval.Create(PrimaryContig, posStart, posEnd));
            otherVariant.SetupGet(v => v.EndInterval).Returns(ContigAndInterval.Create(PrimaryContig, endStart, endEnd));
            var otherSample = new Mock<IWittyerGenotypedCopyNumberSample>();

            var gt = new Mock<IGenotypeInfo>();
            if (originalVariant.Sample is IWittyerGenotypedSample gtSample)
            {
                gt.Setup(g => g.Equals(gtSample.Gt)).Returns(isGtMatch);
                
            }

            otherSample.SetupGet(s => s.Gt).Returns(gt.Object);
            otherSample.SetupGet(s => s.Cn).Returns(cn);

            otherVariant.SetupGet(v => v.Sample).Returns(otherSample.Object);

            var actual = otherVariant.Object.GenerateWhatAndWhy(originalVariant);
            Assert.Equal((matchResult, reasonResult), actual);
        }

        
        private IWittyerVariant GetOriginalCnvGtVariant()
        {
            var variant = SetupBasicVariant(PrimaryContigAndInterval);
            variant.SetupGet(v => v.PosInterval).Returns(ContigAndInterval.Create(PrimaryContig, 1001, 1500));
            variant.SetupGet(v => v.EndInterval).Returns(ContigAndInterval.Create(PrimaryContig, 5001, 5601));
            
            var sample = new Mock<IWittyerGenotypedCopyNumberSample>();
            sample.SetupGet(s => s.Cn).Returns(3);

            sample.SetupGet(s => s.Gt).Returns((new Mock<IGenotypeInfo>()).Object);
            variant.SetupGet(v => v.Sample).Returns(sample.Object);

            return variant.Object;
        }

        [NotNull]
        private static Mock<IWittyerVariant> SetupBasicVariant([NotNull] IContigAndInterval contigAndInterval)
        {
            var variant = new Mock<IWittyerVariant>();
            variant.SetupGet(v => v.Contig).Returns(contigAndInterval.Contig);
            variant.SetupGet(v => v.Start).Returns(contigAndInterval.Start);
            variant.SetupGet(v => v.Stop).Returns(contigAndInterval.Stop);
            variant.SetupGet(v => v.IsStartInclusive).Returns(contigAndInterval.IsStartInclusive);
            variant.SetupGet(v => v.IsStopInclusive).Returns(contigAndInterval.IsStopInclusive);
            return variant;
        }

    }
}
