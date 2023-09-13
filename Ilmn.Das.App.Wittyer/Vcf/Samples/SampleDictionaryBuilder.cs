using System.Collections.Generic;
using System.Linq;
using Ilmn.Das.Core.Tries.Extensions;
using Ilmn.Das.Std.AppUtils.Collections;
using Ilmn.Das.Std.AppUtils.Collections.Generic;
using Ilmn.Das.Std.AppUtils.Misc;
using Ilmn.Das.Std.VariantUtils.Vcf.Variants.Samples;
using JetBrains.Annotations;

namespace Ilmn.Das.App.Wittyer.Vcf.Samples;

public class SampleDictionaryBuilder : IBuilder<SampleDictionaries>
  {
    private readonly OrderedDictionary<string, OrderedDictionary<string, string>> _sampleBuilder;

    internal SampleDictionaryBuilder(OrderedDictionary<string, OrderedDictionary<string, string>> sampleBuilder)
      => _sampleBuilder = sampleBuilder;

    [Pure]
    public SampleDictionaries Build()
    {
      List<IVcfSample> values = new();
      Dictionary<string, int> keyToIndexDictionary = new();
      foreach (var keyValuePair in _sampleBuilder)
      {
        var key = keyValuePair.Key;
        var source1 = keyValuePair.Value;
        IEnumerable<KeyValuePair<string, string>> source2 = source1;
        if (source1.TryGetValue("GT", out var str) && source1.First().Key != "GT")
          source2 = new KeyValuePair<string, string>("GT", str).FollowedBy(source1.Where(kvp => kvp.Key != "GT")).ToReadOnlyOrderedDictionary();
        keyToIndexDictionary.Add(key, values.Count);
        values.Add(VcfSample.Create(key, source2.ToReadOnlyOrderedDictionary<string>()));
      }
      return SampleDictionaries.Create(keyToIndexDictionary, values);
    }

    public SampleDictionaryBuilder ClearAllDictionaries()
    {
      foreach (var orderedDictionary in _sampleBuilder.Values)
        orderedDictionary.Clear();
      return this;
    }

    public SampleDictionaryBuilder RemoveFormatKey(string keyToRemove)
    {
      foreach (var orderedDictionary in _sampleBuilder.Values)
        orderedDictionary.Remove(keyToRemove);
      return this;
    }

    public SampleDictionaryBuilder AddFormatKey(string keyToAdd)
    {
      foreach (var orderedDictionary in _sampleBuilder.Values)
        orderedDictionary.Add(keyToAdd, ".");
      return this;
    }

    public SampleDictionaryBuilder RenameFormatKey(string oldKeyName, string newKeyName)
    {
      if (oldKeyName == newKeyName)
        return this;
      foreach (var orderedDictionary in _sampleBuilder.Values)
      {
        orderedDictionary[newKeyName] = orderedDictionary[oldKeyName];
        orderedDictionary.Remove(oldKeyName);
      }
      return this;
    }

    public SampleDictionaryBuilder SetSampleField(
      string sampleName,
      (string key, string value) newValue)
    {
      return SetSampleFieldPrivate(_sampleBuilder[sampleName], newValue);
    }

    public SampleDictionaryBuilder SetSampleField(
      int sampleIndex,
      (string key, string value) newValue)
    {
      return SetSampleFieldPrivate(_sampleBuilder.ElementAtOrException(sampleIndex).GetOrThrow().Value, newValue);
    }

    public SampleDictionaryBuilder SetSampleFieldsAll((string key, string value) newValue)
    {
      foreach (var key in _sampleBuilder.Keys)
        SetSampleField(key, newValue);
      return this;
    }

    private SampleDictionaryBuilder SetSampleFieldPrivate(
      IDictionary<string, string> sampleDict,
      (string key, string value) newValue)
    {
      if (!_sampleBuilder.First().Value.ContainsKey(newValue.key))
        AddFormatKey(newValue.key);
      sampleDict[newValue.key] = newValue.value;
      return this;
    }
  }