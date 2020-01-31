using System.IO;
using Ilmn.Das.Std.BioinformaticUtils.Contigs;
using Ilmn.Das.Std.VariantUtils.Vcf.Variants;
using JetBrains.Annotations;

namespace Ilmn.Das.App.Wittyer.Utilities.Enums
{
    /// <summary>
    /// For cross reference match. we always match the truth reference type
    /// </summary>
    internal enum GenomeType
    {
        Unknown = 0,
        Ucsc,
        Grch
    }

    internal static class GenomeTypeUtils
    {
        [NotNull]
        internal static IVcfVariant ConvertGenomeType([NotNull] this IVcfVariant variant, GenomeType type)
        {
            switch (type)
            {
                case GenomeType.Ucsc:
                    return variant.ToUcscStyleVariant();
                case GenomeType.Grch:
                    return variant.ToGrchStyleVariant();
                case GenomeType.Unknown:
                    return variant;
                default:
                    throw new InvalidDataException(
                        $"Not sure why there's a genometype {type.ToString()} in vcf which we are not supporting!");
            }
        }

        internal static GenomeType GetGenomeType([NotNull] this IContigInfo variantContig)
        {
            var isUcsc = variantContig.ToUcscStyle().Name == variantContig.Name;
            var isGrch37 = variantContig.ToGrchStyle().Name == variantContig.Name;
            if (isUcsc && isGrch37) return GenomeType.Unknown; // means not found in either.
            return isUcsc ? GenomeType.Ucsc : GenomeType.Grch;
        }
    }
}