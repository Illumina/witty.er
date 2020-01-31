using System.Collections.Immutable;
using Ilmn.Das.App.Wittyer.Utilities.Enums;
using Ilmn.Das.App.Wittyer.Vcf.Variants.Genotype;
using Ilmn.Das.Std.VariantUtils.Vcf.Variants.Samples;
using JetBrains.Annotations;

namespace Ilmn.Das.App.Wittyer.Vcf.Samples
{
    public interface IWittyerGenotypedCopyNumberSample : IWittyerGenotypedSample, IWittyerCopyNumberSample
    {

    }

    internal class WittyerGenotypedCopyNumberSample : IWittyerGenotypedCopyNumberSample
    {
        internal readonly WittyerCopyNumberSample BaseSample;

        public IVcfSample GetOriginalSample()
        {
            return BaseSample.GetOriginalSample();
        }

        public WitDecision Wit => BaseSample.Wit;
        public IImmutableList<MatchEnum> What => BaseSample.What;
        public IImmutableList<FailedReason> Why => BaseSample.Why;
        public IGenotypeInfo Gt { get; }
        public uint? Cn => BaseSample.Cn;

        private WittyerGenotypedCopyNumberSample(WittyerCopyNumberSample sample, IGenotypeInfo gt)
        {
            BaseSample = sample;
            Gt = gt;
        }

        [NotNull]
        internal static WittyerGenotypedCopyNumberSample Create([NotNull] WittyerCopyNumberSample sample, 
            [NotNull] IGenotypeInfo gt) 
            => new WittyerGenotypedCopyNumberSample(sample, gt);
    }
}
