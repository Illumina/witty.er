using Ilmn.Das.Std.BioinformaticUtils.GenomicFeatures;

namespace Ilmn.Das.App.Wittyer.Vcf.Variants
{
    public interface IWittyerVariant : IWittyerSimpleVariant
    {
        IContigAndInterval PosInterval { get; }
    }
}