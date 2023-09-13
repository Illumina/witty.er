using System;
using System.Collections.Generic;
using Ilmn.Das.Core.Tries;
using Ilmn.Das.Core.Tries.Extensions;
using Ilmn.Das.Std.AppUtils.Collections;
using Ilmn.Das.Std.AppUtils.Collections.Generic;
using Ilmn.Das.Std.BioinformaticUtils.Genomes;
using Ilmn.Das.Std.VariantUtils.Vcf.Headers;
using JetBrains.Annotations;

namespace Ilmn.Das.App.Wittyer.Vcf.Readers;

/// <summary>
/// Parser Settings
/// </summary>
public class VcfVariantParserSettings
{
    private VcfVariantParserSettings(
        IReadOnlyDictionary<string, int> sampleToIndexDictionary,
        int expectedNumberOfColumns,
        IGenomeAssembly? referenceGenome)
    {
        SampleToIndexDictionary = sampleToIndexDictionary;
        ExpectedNumberOfColumns = expectedNumberOfColumns;
        ReferenceGenome = referenceGenome;
    }

    internal IReadOnlyDictionary<string, int> SampleToIndexDictionary { get; }

    internal int ExpectedNumberOfColumns { get; }

    public IGenomeAssembly? ReferenceGenome { get; }

    [Pure]
    public static VcfVariantParserSettings Create(
        IReadOnlyList<string> sampleNames,
        IGenomeAssembly? referenceGenome = null)
    {
        var expectedNumberOfColumns = 8;
        if (sampleNames.Count > 0)
            expectedNumberOfColumns += sampleNames.Count + 1;
        var source = OrderedDictionary<string, int>.Create();
        for (var index = 0; index < sampleNames.Count; ++index)
            source.Add(sampleNames[index], index);
        return new VcfVariantParserSettings(source.ToReadOnlyOrderedDictionary(), expectedNumberOfColumns, referenceGenome);
    }

    [Pure]
    public static ITry<VcfVariantParserSettings> TryCreateFromHeader(IVcfHeader header) => header.TryGetReferenceGenome().Select<IGenomeAssembly, VcfVariantParserSettings>((Func<IGenomeAssembly, VcfVariantParserSettings>) (assembly => Create(header.SampleNames, assembly)));
}