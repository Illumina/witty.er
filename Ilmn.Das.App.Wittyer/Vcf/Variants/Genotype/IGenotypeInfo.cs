using System;
using System.Collections.Immutable;

namespace Ilmn.Das.App.Wittyer.Vcf.Variants.Genotype
{
    public interface IGenotypeInfo : IEquatable<IGenotypeInfo>
    {
        string OriginalGtString { get; }

        bool IsPhased { get; }

        IImmutableList<string> GenotypeIndices { get; } // to accommendate "."
    }
}