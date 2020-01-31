using System.ComponentModel;
using Ilmn.Das.Std.VariantUtils.Vcf;

namespace Ilmn.Das.App.Wittyer.Utilities.Enums
{
    /// <summary>
    /// Value used in WHAT sample tag
    /// </summary>
    public enum MatchEnum
    {
        /// <summary>
        /// Unknown value, usually non-overlapping or not supported type
        /// </summary>
        [Description(VcfConstants.MissingValueString)]
        Unmatched = 0,

        /// <summary>
        /// Local match, meaning limited overlap (not reaching threshold) and genotype does not match
        /// </summary>
        [Description("lm")]
        LocalMatch,

        /// <summary>
        /// Local and Genotype match, limited overlap (same as lm) but genotype matches
        /// </summary>
        [Description("lgm")]
        LocalAndGenotypeMatch,

        /// <summary>
        /// Allele match, distance overlap meet minimum threshold but genotype does not match
        /// </summary>
        [Description("am")]
        AlleleMatch,

        /// <summary>
        /// Allele and genotype match, distance overlap meet minimum threshold and genotype matches
        /// </summary>
        [Description("agm")]
        AlleleAndGenotypeMatch

    }
}
