using System.Collections.Immutable;
using System.Linq;
using Ilmn.Das.App.Wittyer.Utilities;
using Ilmn.Das.Std.VariantUtils.SimpleVariants;
using Ilmn.Das.Std.VariantUtils.Vcf;
using Ilmn.Das.Std.VariantUtils.Vcf.Variants;
using Ilmn.Das.Std.VariantUtils.Vcf.Variants.Samples;
using JetBrains.Annotations;

namespace Ilmn.Das.App.Wittyer.Input
{
    internal static class InputUtils
    {
        internal static bool IsFilterIncluded([NotNull] this IVcfVariant variant,
            IImmutableSet<string> includedFilters, IImmutableSet<string> excludedFilters)
            => !variant.Filters.Any(excludedFilters.Contains) &&
               (includedFilters.Count == 0 ||
                variant.Filters.Any(includedFilters.Contains));

        internal static bool IsSamplePassFilter([NotNull] this IVcfVariant variant, [CanBeNull] string name)
            => name == null || variant.Samples.Count == 0 || variant.Samples[name].IsSampleFtPassFilter();

        private static bool IsSampleFtPassFilter([NotNull] this IVcfSample sample)
            => !sample.SampleDictionary.ContainsKey(WittyerConstants.Ft)
               || sample.SampleDictionary[WittyerConstants.Ft].Equals(VcfConstants.PassFilter);


        internal static bool IsRefCall([NotNull] this IVcfVariant variant, [CanBeNull] string sampleName)
        {
            //refsite is a refcall for sure
            if (variant.IsRefSite())
                return true;

            //if not refsite and no sample field, not a refcall
            if (variant.Samples.Count == 0)
                return false;

            var sample = sampleName == null ? variant.Samples[0] : variant.Samples[sampleName];

            var isCn = sample.SampleDictionary.TryGetValue(VcfConstants.CnSampleFieldKey, out var cnString);
            var isGt = sample.SampleDictionary.TryGetValue(VcfConstants.GenotypeKey, out var gt);
            if (isGt)
            {
                //todo: refining how to deal with ploidy. Also here we don't deal with LOH. assuming CN = ploidy is ref
                var gtArray = gt.Split('/', '|');
                if (isCn && int.TryParse(cnString, out var intCn))
                    return intCn == gtArray.Length;
                return gtArray.All(alleleIndex => alleleIndex == "0");
            }

            return isCn && cnString == "2";
        }

        
    }
}