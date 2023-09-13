using Ilmn.Das.Std.VariantUtils.Vcf.Variants.Samples;
namespace Ilmn.Das.App.Wittyer.Vcf.Samples;

using Std.AppUtils.Collections.ReadOnlyOrderedDictionary;
using Std.AppUtils.Comparers;
using JetBrains.Annotations;

internal class VcfSample : IVcfSample
{
    private VcfSample(
        string sampleName,
        IReadOnlyOrderedDictionary<string> sampleDictionary)
    {
        SampleName = sampleName;
        SampleDictionary = sampleDictionary;
    }

    [Pure]
    public static IVcfSample Create(string sampleName, IReadOnlyOrderedDictionary<string> sampleDictionary)
        => new VcfSample(sampleName, sampleDictionary);

    public string SampleName { get; }

    public IReadOnlyOrderedDictionary<string> SampleDictionary { get; }

    public bool Equals(IVcfSample? other)
    {
        var nullable = ComparerUtils.HandleNullEqualityComparison<IVcfSample>(this, other);
        if (nullable.HasValue)
            return nullable.GetValueOrDefault();
        return string.Equals(SampleName, other.SampleName) && SampleDictionary.Equals(other.SampleDictionary);
    }

    public override bool Equals(object? obj)
    {
        if (obj == null)
            return false;
        return this == obj || obj is IVcfSample other && Equals(other);
    }

    public override int GetHashCode() => SampleName.GetHashCode() * 397 ^ SampleDictionary.GetHashCode();
}