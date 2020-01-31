using Ilmn.Das.Std.BioinformaticUtils.GenomicFeatures;

namespace Ilmn.Das.App.Wittyer.Vcf.Variants
{
    /// <summary>
    /// 
    /// </summary>
    /// <seealso cref="Ilmn.Das.App.Wittyer.Vcf.Variants.IWittyerSimpleVariant" />
    public interface IWittyerVariant : IWittyerSimpleVariant
    {
        /// <summary>
        /// Gets the interval around the vcf position.
        /// </summary>
        /// <value>
        /// The position interval.
        /// </value>
        IContigAndInterval PosInterval { get; }
    }

    internal interface IMutableWittyerVariant : IWittyerVariant, IMutableWittyerSimpleVariant
    {

    }
}