using System.Collections.Immutable;
using System.IO;
using Ilmn.Das.App.Wittyer.Utilities.Enums;
using Ilmn.Das.App.Wittyer.Vcf.Variants;
using Ilmn.Das.App.Wittyer.Vcf.Variants.Genotype;
using Ilmn.Das.Std.VariantUtils.Vcf;
using Ilmn.Das.Std.VariantUtils.Vcf.Variants.Samples;

using JetBrains.Annotations;

namespace Ilmn.Das.App.Wittyer.Vcf.Samples
{
    /// <summary>
    /// A WittyerSample companion object
    /// </summary>
    public static class WittyerSample
    {
        /// <summary>
        /// Creates based on the baseVariant and the given sample.
        /// </summary>
        /// <param name="baseVariant">The base variant.</param>
        /// <param name="sample">The sample.</param>
        /// <param name="isReference">if set to <c>true</c> [is reference].</param>
        /// <returns></returns>
        /// <exception cref="InvalidDataException"></exception>
        [Pure]
        public static IWittyerSample CreateFromVariant(IVcfVariant baseVariant, IVcfSample? sample, bool isReference)
        {
            if (isReference)
                return CreateReferenceSample(baseVariant, sample);

            if (sample == null)
                return WittyerSampleInternal.Create(null);

            var wittyerSample = WittyerSampleInternal.Create(sample);

            var hasGt = sample.SampleDictionary.ContainsKey(VcfConstants.GenotypeKey);

            if (!sample.SampleDictionary.TryGetValue(VcfConstants.CnSampleFieldKey, out var cnString))
                return hasGt
                    ? WittyerGenotypedSample.Create(wittyerSample, GenotypeInfo.CreateFromSample(sample))
                    : wittyerSample;

            decimal? cnNumber;
            if (cnString == VcfConstants.MissingValueString)
                cnNumber = null;
            else if (decimal.TryParse(cnString, out var cnNumberLocal))
                cnNumber = cnNumberLocal;
            else
                throw new InvalidDataException($"{VcfConstants.CnSampleFieldKey} does not have a valid value in {baseVariant}");
                    
            var cnSample = WittyerCopyNumberSample.Create(wittyerSample, cnNumber);
            if (!hasGt) return cnSample;

            var gtInfo = GenotypeInfo.CreateFromSample(sample);
            return WittyerGenotypedCopyNumberSample.Create(cnSample, gtInfo);
        }

        /// <summary>
        /// Creates the specified base sample.
        /// </summary>
        /// <param name="baseSample">The base sample.</param>
        /// <param name="wit">The wit.</param>
        /// <param name="what">The what.</param>
        /// <param name="why">The why.</param>
        /// <returns></returns>
        [Pure]
        public static IWittyerSample Create(IVcfSample baseSample, WitDecision wit,
            IImmutableList<MatchSet> what, ImmutableList<FailedReason> why)
            => WittyerSampleInternal.Create(baseSample, wit, what, why);

        internal static IWittyerGenotypedCopyNumberSample CreateReferenceSample(IVcfVariant baseVariant, IVcfSample? sample)
        {
            var ploidy = 2;
            if (sample == null)
                return WittyerGenotypedCopyNumberSample.Create(
                    WittyerCopyNumberSample.Create(WittyerSampleInternal.Create(null), (uint)ploidy),
                    GenotypeInfo.CreateRef(ploidy, false));

            var isPhased = false;
            if (sample.SampleDictionary.TryGetValue(VcfConstants.GenotypeKey, out var originalGt))
            {
                isPhased = originalGt.Contains(VcfConstants.GtPhasedValueDelimiter);
                ploidy = originalGt
                    .Split(isPhased ? VcfConstants.GtPhasedValueDelimiter : VcfConstants.GtUnphasedValueDelimiter).Length;
            }

            var cnSample = WittyerCopyNumberSample.Create(WittyerSampleInternal.Create(sample), (uint) ploidy);
            return WittyerGenotypedCopyNumberSample.Create(cnSample, GenotypeInfo.CreateRef(ploidy, isPhased));
        }
    }

    internal class WittyerSampleInternal : IWittyerSample
    {
        public IVcfSample? OriginalSample { get; }

        public WitDecision Wit { get; internal set; }
        public IImmutableList<MatchSet> What { get; internal set; }
        public IImmutableList<FailedReason> Why { get; internal set; }

        private WittyerSampleInternal(IVcfSample? baseSample, WitDecision wit,
            IImmutableList<MatchSet> what, IImmutableList<FailedReason> why)
        {
            OriginalSample = baseSample;
            Wit = wit;
            What = what;
            Why = why;
        }
        
        internal static WittyerSampleInternal Create(IVcfSample? baseSample) 
            => new(baseSample, WitDecision.NotAssessed,
            ImmutableList<MatchSet>.Empty, 
            ImmutableList<FailedReason>.Empty);

        internal static WittyerSampleInternal Create(IVcfSample baseSample, WitDecision wit,
            IImmutableList<MatchSet> what, ImmutableList<FailedReason> why)
            => new(baseSample, wit, what, why);
    }
}