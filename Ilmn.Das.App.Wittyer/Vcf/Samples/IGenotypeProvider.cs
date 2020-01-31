using Ilmn.Das.App.Wittyer.Vcf.Variants.Genotype;

namespace Ilmn.Das.App.Wittyer.Vcf.Samples
{
    public interface IGenotypeProvider
    {
        IGenotypeInfo Gt { get; }
    }
}
