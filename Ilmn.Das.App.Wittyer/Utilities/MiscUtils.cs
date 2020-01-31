using System.Collections.Generic;
using System.Linq;
using Ilmn.Das.App.Wittyer.Input;
using Ilmn.Das.Std.BioinformaticUtils.GenomicFeatures;
using Ilmn.Das.Std.VariantUtils.Vcf;
using Ilmn.Das.Std.VariantUtils.Vcf.Variants;
using JetBrains.Annotations;

namespace Ilmn.Das.App.Wittyer.Utilities
{
    internal static class MiscUtils
    {
        internal static (IVcfVariant first, IVcfVariant second) FindBndEntriesOrder(IVcfVariant variantA,
            IVcfVariant variantB)
            => ContigAndPositionComparer.Default.Compare(variantA, variantB) > 0
                ? (variantB, variantA)
                : (variantA, variantB);

        internal static bool IsIncludedForEvaluation([NotNull] this IVcfVariant variant,
            [NotNull, ItemNotNull] IReadOnlyList<string> includedFilters, [NotNull, ItemNotNull] IReadOnlyList<string> excludedFilters,
            [CanBeNull] string sampleName = null)
        {
            var general = !variant.Filters.Any(excludedFilters.Contains)
                   && (includedFilters.Count == 0 || variant.Filters.Any(includedFilters.Contains));

            if (includedFilters.Count != 1 || !includedFilters.Contains(VcfConstants.PassFilter)) return general;
            if (sampleName == null)
                return general;

            var ft = variant.Samples.ContainsKey(sampleName) &&
                     variant.Samples[sampleName].SampleDictionary.ContainsKey(WittyerConstants.Ft) &&
                     variant.Samples[sampleName].SampleDictionary[WittyerConstants.Ft].Equals(VcfConstants.PassFilter);
            return general && ft;

        }

        public static void Deconstruct([CanBeNull] this ISamplePair samplePair, [CanBeNull] out string truthSampleName, [CanBeNull] out string querySampleName)
        {
            truthSampleName = samplePair?.TruthSampleName;
            querySampleName = samplePair?.QuerySampleName;
        }
    }
}
