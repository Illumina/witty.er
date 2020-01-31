using System.Collections.Immutable;
using System.IO;
using Ilmn.Das.App.Wittyer.Utilities.Enums;
using Ilmn.Das.App.Wittyer.Vcf.Variants.Genotype;
using Ilmn.Das.Std.AppUtils.Collections;
using Ilmn.Das.Std.VariantUtils.Vcf;
using Ilmn.Das.Std.VariantUtils.Vcf.Variants;
using Ilmn.Das.Std.VariantUtils.Vcf.Variants.Samples;
using JetBrains.Annotations;

namespace Ilmn.Das.App.Wittyer.Vcf.Samples
{
    public static class WittyerSample
    {
        [NotNull]
        public static IWittyerSample CreateOverall(IVcfVariant baseVariant, [CanBeNull] string sample, bool isReference)
        {
            if (isReference)
                return CreateReferenceSample(baseVariant, sample);

            if (sample.IsNullOrEmpty())
                return WittyerSampleInternal.Create(null);

            var wittyerSample = WittyerSampleInternal.Create(baseVariant, sample);

            var hasGt = baseVariant.Samples[sample].SampleDictionary.ContainsKey(VcfConstants.GenotypeKey);

            if (!baseVariant.Samples[sample].SampleDictionary.TryGetValue(VcfConstants.CnSampleFieldKey, out var cnString))
                return hasGt
                    ? WittyerGenotypedSample.Create(wittyerSample, GenotypeInfo.Create(baseVariant, sample))
                        as IWittyerSample
                    : wittyerSample;

            uint? cnNumber;
            if (cnString == VcfConstants.MissingValueString)
                cnNumber = null;
            else if (uint.TryParse(cnString, out var cnNumberLocal))
                cnNumber = cnNumberLocal;
            else
                throw new InvalidDataException($"{VcfConstants.CnSampleFieldKey} does not have a valid value in {baseVariant}");
                    
            var cnSample = WittyerCopyNumberSample.Create(wittyerSample, cnNumber);
            if (!hasGt) return cnSample;

            var gtInfo = GenotypeInfo.Create(baseVariant, sample);
            return WittyerGenotypedCopyNumberSample.Create(cnSample, gtInfo);
        }

        [NotNull]
        public static IWittyerSample Create([NotNull] IVcfSample baseSample, WitDecision wit,
            [NotNull] IImmutableList<MatchEnum> what, [NotNull] IImmutableList<FailedReason> why)
            => WittyerSampleInternal.Create(baseSample, wit, what, why);

        [NotNull]
        public static IWittyerCopyNumberSample Create([NotNull] IVcfSample baseSample, WitDecision wit,
            [NotNull] IImmutableList<MatchEnum> what, [NotNull] IImmutableList<FailedReason> why, uint cn)
            => WittyerCopyNumberSample.Create(
                WittyerSampleInternal.Create(baseSample, wit, what, why), cn);

        [NotNull]
        public static IWittyerGenotypedSample Create([NotNull] IVcfSample baseSample, WitDecision wit,
            [NotNull] IImmutableList<MatchEnum> what, [NotNull] IImmutableList<FailedReason> why, [NotNull] IGenotypeInfo gt) 
            => WittyerGenotypedSample.Create(WittyerSampleInternal.Create(baseSample, wit, what, why), gt);

        [NotNull]
        public static IWittyerGenotypedCopyNumberSample Create([NotNull] IVcfSample baseSample, WitDecision wit,
            [NotNull] IImmutableList<MatchEnum> what, [NotNull] IImmutableList<FailedReason> why, uint cn, [NotNull] IGenotypeInfo gt)
            => WittyerGenotypedCopyNumberSample.Create(
                WittyerCopyNumberSample.Create(WittyerSampleInternal.Create(baseSample, wit, what, why), cn), gt);

        [NotNull]
        internal static IWittyerGenotypedCopyNumberSample CreateReferenceSample([NotNull] IVcfVariant baseVariant, [CanBeNull] string sampleName)
        {
            var isPhased = false;
            var ploidy = 2;
            if (baseVariant.Samples.Count > 0)
            {
                var sample = sampleName == null ? baseVariant.Samples[0] : baseVariant.Samples[sampleName];
                if (sample.SampleDictionary.TryGetValue(VcfConstants.GenotypeKey, out var originalGt))
                {
                    isPhased = originalGt.Contains(VcfConstants.GtPhasedValueDelimiter);
                    ploidy = originalGt
                        .Split(isPhased ? VcfConstants.GtPhasedValueDelimiter : VcfConstants.GtUnphasedValueDelimiter).Length;
                }
            }
           
            return WittyerGenotypedCopyNumberSample.Create(WittyerCopyNumberSample.Create(
                sampleName == null
                        ? WittyerSampleInternal.Create(null)
                        : WittyerSampleInternal.Create(baseVariant, sampleName), (uint) ploidy), GenotypeInfo.CreateRef(ploidy, isPhased));
        }
    }

    internal class WittyerSampleInternal : IWittyerSample
    {
        private readonly IVcfSample _baseSample;
        public IVcfSample GetOriginalSample() => _baseSample;
        public WitDecision Wit { get; internal set; }
        public IImmutableList<MatchEnum> What { get; internal set; }
        public IImmutableList<FailedReason> Why { get; internal set; }

        private WittyerSampleInternal(IVcfSample baseSample, WitDecision wit,
            [NotNull] IImmutableList<MatchEnum> what, [NotNull] IImmutableList<FailedReason> why)
        {
            _baseSample = baseSample;
            Wit = wit;
            What = what;
            Why = why;
        }

        [NotNull]
        internal static WittyerSampleInternal Create([NotNull] IVcfVariant variant, string sampleName)
        {
            if(!variant.Samples.ContainsKey(sampleName))
                throw new InvalidDataException($"{sampleName} not found in {variant}");
            return Create(variant.Samples[sampleName]);
        }

        [NotNull]
        internal static WittyerSampleInternal Create([CanBeNull] IVcfSample baseSample) 
            => new WittyerSampleInternal(baseSample, WitDecision.NotAssessed,
            ImmutableList<MatchEnum>.Empty,
            ImmutableList<FailedReason>.Empty);

        [NotNull]
        internal static WittyerSampleInternal Create([NotNull] IVcfSample baseSample, WitDecision wit,
            [NotNull] IImmutableList<MatchEnum> what, [NotNull] IImmutableList<FailedReason> why)
            => new WittyerSampleInternal(baseSample, wit, what, why);
    }
}