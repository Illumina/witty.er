namespace Ilmn.Das.App.Wittyer.Vcf.Samples
{
    public interface ICopyNumberProvider
    {
        /// <summary>
        /// Gets the CN tag. Only CNV has CN
        /// </summary>
        /// <value>
        /// The cn.
        /// </value>
        decimal? Cn { get; }
    }
}
