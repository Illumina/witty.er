using System.Collections.Immutable;
using System.IO;
using Ilmn.Das.App.Wittyer.Utilities.Enums;
using Ilmn.Das.App.Wittyer.Vcf.Variants.Genotype;
using Ilmn.Das.Std.VariantUtils.Vcf.Variants.Samples;


namespace Ilmn.Das.App.Wittyer.Vcf.Samples
{
    public interface IWittyerGenotypedSample : IWittyerSample, IGenotypeProvider
    {

    }

    internal class WittyerGenotypedSample : IWittyerGenotypedSample
    {
        internal readonly WittyerSampleInternal BaseSample;

        public IVcfSample OriginalSample => BaseSample.OriginalSample ??
                                            throw new InvalidDataException(
                                                $"Got a null sample for a {nameof(WittyerGenotypedSample)}!");

        public WitDecision Wit => BaseSample.Wit;
        public IImmutableList<MatchSet> What => BaseSample.What;
        public IImmutableList<FailedReason> Why => BaseSample.Why;
        public IGenotypeInfo Gt { get; }

        private WittyerGenotypedSample(WittyerSampleInternal baseSample, IGenotypeInfo gt)
        {
            BaseSample = baseSample;
            Gt = gt;
        }
        internal static IWittyerGenotypedSample Create(WittyerSampleInternal sample, IGenotypeInfo gt) 
            => new WittyerGenotypedSample(sample, gt);
    }
}
