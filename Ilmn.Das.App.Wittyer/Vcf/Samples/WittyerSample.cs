using System.Collections.Immutable;
using System.IO;
using Ilmn.Das.App.Wittyer.Utilities.Enums;
using Ilmn.Das.App.Wittyer.Vcf.Variants.Genotype;
using Ilmn.Das.Std.VariantUtils.Vcf;
using Ilmn.Das.Std.VariantUtils.Vcf.Variants;
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
        [NotNull]
        public static IWittyerSample CreateFromVariant(IVcfVariant baseVariant, [CanBeNull] IVcfSample sample, bool isReference)
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
        [NotNull]
        public static IWittyerSample Create([NotNull] IVcfSample baseSample, WitDecision wit,
            [NotNull] IImmutableList<MatchEnum> what, [NotNull] IImmutableList<FailedReason> why)
            => WittyerSampleInternal.Create(baseSample, wit, what, why);

        [NotNull]
        internal static IWittyerGenotypedCopyNumberSample CreateReferenceSample([NotNull] IVcfVariant baseVariant, [CanBeNull] IVcfSample sample)
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