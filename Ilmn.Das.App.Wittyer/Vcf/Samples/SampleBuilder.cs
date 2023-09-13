using Ilmn.Das.Std.VariantUtils.Vcf.Variants.Samples;

namespace Ilmn.Das.App.Wittyer.Vcf.Samples;

using Std.AppUtils.Collections;
using Std.AppUtils.Collections.Generic;
using JetBrains.Annotations;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;

public class SampleBuilder
{
    private readonly IReadOnlyList<string> _dictKeys;
    private readonly OrderedDictionary<string, OrderedDictionary<string, string>> _sampleBuilder;

    private SampleBuilder(
        OrderedDictionary<string, OrderedDictionary<string, string>> sampleBuilder,
        IReadOnlyList<string> dictKeys)
    {
        _dictKeys = dictKeys;
        _sampleBuilder = sampleBuilder;
    }

    public SampleBuilder ClearSamples()
    {
        _sampleBuilder.Clear();
        return this;
    }

    public SampleBuilder AddSample(string sampleName)
    {
        _sampleBuilder.Add(sampleName, _dictKeys.ToOrderedDictionary(k => k, _ => "."));
        return this;
    }

    public SampleBuilder RemoveSample(string sampleName)
    {
        _sampleBuilder.Remove(sampleName);
        return this;
    }

    public SampleDictionaryBuilder MoveOnToDictionaries() => new(_sampleBuilder);

    [Pure]
    internal static SampleBuilder CreateSampleBuilder() => new(OrderedDictionary<string, OrderedDictionary<string, string>>.Create(), ImmutableList<string>.Empty);

    [Pure]
    internal static SampleBuilder CreateSampleBuilder(SampleDictionaries baseDictionary) => new(baseDictionary.ToOrderedDictionary<KeyValuePair<string, IVcfSample>, string, OrderedDictionary<string, string>>((Func<KeyValuePair<string, IVcfSample>, string>) (kvp => kvp.Key), (Func<KeyValuePair<string, IVcfSample>, OrderedDictionary<string, string>>) (kvp => kvp.Value.SampleDictionary.ToOrderedDictionary<string, string>())), baseDictionary.Values.Count == 0 ? ImmutableList<string>.Empty : baseDictionary.Values[0].SampleDictionary.Keys);
}