using System.Collections.Generic;
using Ilmn.Das.App.Wittyer.Input;
using Ilmn.Das.App.Wittyer.Utilities.Enums;
using Ilmn.Das.App.Wittyer.Vcf.Samples;
using Ilmn.Das.App.Wittyer.Vcf.Variants.Annotations;
using Ilmn.Das.Std.AppUtils.Intervals;
using Ilmn.Das.Std.BioinformaticUtils.GenomicFeatures;
using Ilmn.Das.Std.VariantUtils.Vcf.Variants;
using JetBrains.Annotations;

namespace Ilmn.Das.App.Wittyer.Vcf.Variants
{
    /// <inheritdoc />
    public interface IWittyerSimpleVariant : IContigAndInterval
    {
        /// <summary>
        /// The type of Variant.
        /// </summary>
        [NotNull]
        WittyerType VariantType { get; }

        /// <summary>
        /// The Witty.er size bin.
        /// </summary>
        [NotNull]
        Winner Win { get; }

        /// <summary>
        /// The Interval around Position that CIPOS creates
        /// </summary>
        [NotNull]
        IInterval<uint> CiPosInterval { get; }

        /// <summary>
        /// The Interval around END that CIEND creates
        /// </summary>
        [NotNull]
        IInterval<uint> CiEndInterval { get; }

        /// <summary>
        /// The End Interval.
        /// </summary>
        [NotNull]
        IContigAndInterval EndInterval { get; }

        /// <summary>
        ///     WHO, WHAT, WOW, WHERE in INFO field
        /// </summary>
        /// <value>
        ///     The overlap information.
        /// </value>
        [NotNull]
        IReadOnlyList<OverlapAnnotation> OverlapInfo { get; }

        /// <summary>
        /// The Sample associated with this variant.
        /// </summary>
        [NotNull]
        IWittyerSample Sample { get; }

        /// <summary>
        /// The original variant associated with this variant.
        /// </summary>
        [NotNull]
        IVcfVariant OriginalVariant { get; }
    }

    internal interface IMutableWittyerSimpleVariant : IWittyerSimpleVariant
    {
        void AddToOverlapInfo(OverlapAnnotation newAnnotation);

        void Finalize(WitDecision falseDecision, EvaluationMode mode,
            [CanBeNull] GenomeIntervalTree<IContigAndInterval> includedRegions);
    }
}