using System.Collections.Generic;
using Ilmn.Das.App.Wittyer.Vcf.Samples;
using Ilmn.Das.App.Wittyer.Vcf.Variants.Annotations;
using Ilmn.Das.Std.BioinformaticUtils.GenomicFeatures;
using Ilmn.Das.Std.VariantUtils.Vcf.Variants;

namespace Ilmn.Das.App.Wittyer.Vcf.Variants
{
    public interface IWittyerSimpleVariant : IContigAndInterval
    {
        WittyerVariantType VariantType { get; }

        Winner Win { get; }

        IContigAndInterval EndInterval { get; }

        /// <summary>
        ///     WHO, WHAT, WOW, WHERE in INFO field
        /// </summary>
        /// <value>
        ///     The overlap information.
        /// </value>
        List<OverlapAnnotation> OverlapInfo { get; }

        IWittyerSample Sample { get; }

        IVcfVariant OriginalVariant { get; }

        void AddToOverlapInfo(OverlapAnnotation newAnnotation);
    }
}