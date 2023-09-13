using System.Collections.Immutable;
using Ilmn.Das.App.Wittyer.Utilities.Enums;
using Ilmn.Das.App.Wittyer.Vcf.Variants.Genotype;
using Ilmn.Das.Std.VariantUtils.Vcf.Variants.Samples;

namespace Ilmn.Das.App.Wittyer.Vcf.Samples
{
    public interface IWittyerGenotypedCopyNumberSample : IWittyerGenotypedSample, IWittyerCopyNumberSample
    {

    }

    internal class WittyerGenotypedCopyNumberSample : IWittyerGenotypedCopyNumberSample
    {
        internal readonly WittyerCopyNumberSample BaseSample;

        public IVcfSample OriginalSample => BaseSample.OriginalSample;
        public WitDecision Wit => BaseSample.Wit;
        public IImmutableList<MatchSet> What => BaseSample.What;
        public IImmutableList<FailedReason> Why => BaseSample.Why;
        public IGenotypeInfo Gt { get; }
        public decimal? Cn => BaseSample.Cn;

        private WittyerGenotypedCopyNumberSample(WittyerCopyNumberSample sample, IGenotypeInfo gt)
        {
            BaseSample = sample;
            Gt = gt;
        }

        internal static WittyerGenotypedCopyNumberSample Create(WittyerCopyNumberSample sample, IGenotypeInfo gt) =>
            new(sample, gt);
    }
}
