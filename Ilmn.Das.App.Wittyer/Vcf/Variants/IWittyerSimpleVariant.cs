using System.Collections.Generic;
using Ilmn.Das.App.Wittyer.Input;
using Ilmn.Das.App.Wittyer.Utilities.Enums;
using Ilmn.Das.App.Wittyer.Vcf.Samples;
using Ilmn.Das.App.Wittyer.Vcf.Variants.Annotations;
using Ilmn.Das.Std.AppUtils.Intervals;
using Ilmn.Das.Std.BioinformaticUtils.GenomicFeatures;

namespace Ilmn.Das.App.Wittyer.Vcf.Variants
{
    /// <inheritdoc />
    public interface IWittyerSimpleVariant : IContigAndInterval
    {
        /// <summary>
        /// The type of Variant.
        /// </summary>
        WittyerType VariantType { get; }

        /// <summary>
        /// The Witty.er size bin.
        /// </summary>
        Winner Win { get; }

        /// <summary>
        /// The Interval around Position that CIPOS creates
        /// </summary>
        IInterval<uint> CiPosInterval { get; }

        /// <summary>
        /// The Interval around END that CIEND creates
        /// </summary>
        IInterval<uint> CiEndInterval { get; }

        /// <summary>
        /// The End Interval.
        /// </summary>
        IContigAndInterval EndInterval { get; }
        
        /// <summary>
        /// Gets the original 1-based END position of the vcf variant.
        /// </summary>
        uint EndRefPos { get; }

        /// <summary>
        ///     WHO, WHAT, WOW, WHERE in INFO field
        /// </summary>
        /// <value>
        ///     The overlap information.
        /// </value>
        IReadOnlyList<OverlapAnnotation> OverlapInfo { get; }

        /// <summary>
        /// The Sample associated with this variant.
        /// </summary>
        IWittyerSample Sample { get; }

        /// <summary>
        /// The original variant associated with this variant.
        /// </summary>
        IVcfVariant OriginalVariant { get; }
        
        /// <summary>
        /// Missing for insertions of unknown length and translocations.
        /// Note: For CN:TRs, the start and stop are adjusted such that start might not be the original pos anymore!!
        /// </summary>
        IInterval<uint>? SvLenInterval { get; }
    }

    internal interface IMutableWittyerSimpleVariant : IWittyerSimpleVariant
    {
        void AddToOverlapInfo(OverlapAnnotation newAnnotation);

        new List<OverlapAnnotation> OverlapInfo { get; }

        void Finalize(WitDecision falseDecision, EvaluationMode mode,
            GenomeIntervalTree<IContigAndInterval>? includedRegions, int? maxMatches);
    }
}