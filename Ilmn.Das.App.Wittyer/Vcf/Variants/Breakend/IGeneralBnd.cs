using Ilmn.Das.Std.VariantUtils.Vcf.Variants;
using Ilmn.Das.Std.VariantUtils.Vcf.Variants.Alleles.BreakEnds;

namespace Ilmn.Das.App.Wittyer.Vcf.Variants.Breakend
{
    public interface IGeneralBnd : IVcfVariant, ISimpleBreakEnd
    {
        ISimpleBreakEnd Mate { get; }
    }
}