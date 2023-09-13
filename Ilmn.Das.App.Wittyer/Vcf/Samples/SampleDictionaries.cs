using Ilmn.Das.Std.VariantUtils.Vcf.Variants.Samples;

namespace Ilmn.Das.App.Wittyer.Vcf.Samples;

using Core.Tries;
using Core.Tries.Extensions;
using Std.AppUtils.Collections;
using Std.AppUtils.Collections.ReadOnlyOrderedDictionary;
using Std.AppUtils.Misc;
using JetBrains.Annotations;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

public class SampleDictionaries : ReadOnlyOrderedDictionary<IVcfSample>
  {
    public static readonly SampleDictionaries Empty = new(ImmutableDictionary<string, int>.Empty, ImmutableList<IVcfSample>.Empty);

    protected internal SampleDictionaries(
      IReadOnlyDictionary<string, int> keyToIndexDictionary,
      IEnumerable<IVcfSample> values)
      : base(keyToIndexDictionary.ToImmutableDictionary(), values.ToImmutableList())
    {
    }

    internal IReadOnlyDictionary<string, int> KeyToIndexDictionaryInternal => KeyToIndexDictionary;

    public IEnumerable<IReadOnlyOrderedDictionary<string>> SampleDicts => Values.Select<IVcfSample, IReadOnlyOrderedDictionary<string>>(v => v.SampleDictionary);

    [Pure]
    internal static SampleDictionaries Create(
      IReadOnlyDictionary<string, int> keyToIndexDictionary,
      IReadOnlyList<IVcfSample> values)
    {
      return new SampleDictionaries(keyToIndexDictionary, values);
    }

    [Pure]
    public IEnumerable<ITry<string>> TryGetValues(string key) => Values.Select<IVcfSample, ITry<string>>(sample => sample.SampleDictionary.TryGetValue<string, string>(key));

    [Pure]
    public static SampleBuilder CreateBuilder() => SampleBuilder.CreateSampleBuilder();

    public override string ToString()
    {
      if (Count == 0)
        return string.Empty;
      IReadOnlyOrderedDictionary<string> sampleDictionary = this[0].SampleDictionary;
      return sampleDictionary.Count == 0 ? Enumerable.Repeat<string>(".", 2).StringJoin<string>(":") : sampleDictionary.Keys.FollowedBy<IReadOnlyList<string>>(Values.Select<IVcfSample, IReadOnlyList<string>>((Func<IVcfSample, IReadOnlyList<string>>) (sample => sample.SampleDictionary.Values))).Select<IReadOnlyList<string>, string>((Func<IReadOnlyList<string>, string>) (fields => fields.StringJoin<string>(":"))).StringJoin<string>("\t");
    }
  }