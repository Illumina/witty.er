using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using Ilmn.Das.Std.AppUtils.Collections;
using Ilmn.Das.Std.VariantUtils.Vcf;
using Ilmn.Das.Std.VariantUtils.Vcf.Variants;
using Ilmn.Das.Std.VariantUtils.Vcf.Variants.Samples;

namespace Ilmn.Das.App.Wittyer.Vcf.Variants.Genotype
{
    public class GenotypeInfo : IGenotypeInfo
    {
        private GenotypeInfo(string originalGtString, bool isPhased, IImmutableList<string> genotypeIndices)
        {
            OriginalGtString = originalGtString;
            IsPhased = isPhased;
            GenotypeIndices = genotypeIndices;
        }

        public bool Equals(IGenotypeInfo other)
        {
            return IsPhased.Equals(other.IsPhased)
                   && (IsPhased
                       ? GenotypeIndices.SequenceEqual(other.GenotypeIndices)
                       : GenotypeIndices.IsScrambledEquals(other.GenotypeIndices));
        }

        public string OriginalGtString { get; }
        public bool IsPhased { get; }
        public IImmutableList<string> GenotypeIndices { get; }

        public static IGenotypeInfo Create(string originalGtString, bool isPhased,
            IImmutableList<string> genotypeIndices)
        {
            return new GenotypeInfo(originalGtString, isPhased, genotypeIndices);
        }
        public static IGenotypeInfo Create(IVcfSample sample)
        {
            if(!sample.SampleDictionary.ContainsKey(VcfConstants.GenotypeKey))
                throw new InvalidDataException($"{sample} has not GT field for this variant {sample}");

            var gtString = sample.SampleDictionary[VcfConstants.GenotypeKey];
            var isPhased = gtString.Contains(VcfConstants.GtPhasedValueDelimiter);

            var gtIndices = new List<string>();
            foreach (var gt in gtString.Split(VcfConstants.GtPhasedValueDelimiter[0],
                VcfConstants.GtUnphasedValueDelimiter[0]))
            {
                if (uint.TryParse(gt, out uint _) || gt == ".")
                {
                    gtIndices.Add(gt);
                }
                else
                {
                    throw new InvalidDataException($"{gtString} is not a valid {VcfConstants.GenotypeKey}");
                }
            }
            return Create(gtString, isPhased, gtIndices.ToImmutableList());
        }

        internal static IGenotypeInfo CreateRef(int ploidy, bool isPhased)
        {
            var del = isPhased ? VcfConstants.GtPhasedValueDelimiter : VcfConstants.GtUnphasedValueDelimiter;
            var gtIndicies = Enumerable.Repeat("0", ploidy).ToImmutableList();
            return Create(gtIndicies.StringJoin(del), isPhased, gtIndicies);
        }

        public static IGenotypeInfo Create(IVcfVariant variant, string sampleName)
        {

            if (!variant.Samples.ContainsKey(sampleName))
            {
                throw new InvalidDataException($"{sampleName} not found! Double check the name is spelt correct");
            }

            return Create(variant.Samples[sampleName]);
        }
    }
}