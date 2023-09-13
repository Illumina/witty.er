using Ilmn.Das.Std.VariantUtils.Vcf.Variants.Alleles.BreakEnds;

namespace Ilmn.Das.App.Wittyer.Vcf.Variants.Breakend
{
    /// <inheritdoc />
    /// <summary>
    /// A Breakend
    /// </summary>
    /// <seealso cref="T:Ilmn.Das.Std.VariantUtils.Vcf.Variants.IVcfVariant" />
    /// <seealso cref="T:Ilmn.Das.Std.VariantUtils.Vcf.Variants.Alleles.BreakEnds.ISimpleBreakEnd" />
    public interface IGeneralBnd : IVcfVariant, ISimpleBreakEnd
    {
        /// <summary>
        /// Gets the Breakend's mate.
        /// </summary>
        /// <value>
        /// The mate.
        /// </value>
        ISimpleBreakEnd Mate { get; }
    }
}