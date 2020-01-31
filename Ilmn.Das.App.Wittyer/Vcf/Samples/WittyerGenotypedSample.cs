using System.Collections.Immutable;
using Ilmn.Das.App.Wittyer.Utilities.Enums;
using Ilmn.Das.App.Wittyer.Vcf.Variants.Genotype;
using Ilmn.Das.Std.VariantUtils.Vcf.Variants.Samples;
using JetBrains.Annotations;

namespace Ilmn.Das.App.Wittyer.Vcf.Samples
{
    public interface IWittyerGenotypedSample : IWittyerSample, IGenotypeProvider
    {

    }

    internal class WittyerGenotypedSample : IWittyerGenotypedSample
    {
        internal readonly WittyerSampleInternal BaseSample;
        public IVcfSample GetOriginalSample() => BaseSample.GetOriginalSample();

        public WitDecision Wit => BaseSample.Wit;
        public IImmutableList<MatchEnum> What => BaseSample.What;
        public IImmutableList<FailedReason> Why => BaseSample.Why;
        public IGenotypeInfo Gt { get; }

        private WittyerGenotypedSample(WittyerSampleInternal baseSample, IGenotypeInfo gt)
        {
            BaseSample = baseSample;
            Gt = gt;
        }
        [NotNull]
        internal static IWittyerGenotypedSample Create([NotNull] WittyerSampleInternal sample, [NotNull] IGenotypeInfo gt) 
            => new WittyerGenotypedSample(sample, gt);
    }
}
