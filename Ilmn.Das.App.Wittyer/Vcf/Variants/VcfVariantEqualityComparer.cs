using Ilmn.Das.Std.VariantUtils.Vcf.Variants.Samples;

namespace Ilmn.Das.App.Wittyer.Vcf.Variants;

using Core.Tries.Extensions;
using Std.AppUtils.Collections;
using Std.AppUtils.Comparers;
using Std.VariantUtils.SimpleVariants;
using System;
using System.Collections.Generic;
using System.Linq;

public class VcfVariantEqualityComparer : IEqualityComparer<IVcfVariant>
  {
    public static readonly VcfVariantEqualityComparer Instance = new();
    private static readonly CustomEqualityComparer<IVcfSample> SampleEqualityComparer = new((Func<IVcfSample, IVcfSample, bool>) ((e1, e2) => e1.SampleName == e2.SampleName && e1.SampleDictionary.CompareContent<string, string>(e2.SampleDictionary)), (Func<IVcfSample, int>) (e => HashCodeUtils.GenerateForKvps<string, string>(e.SampleDictionary)));

    private VcfVariantEqualityComparer()
    {
    }

    public bool Equals(IVcfVariant? x, IVcfVariant? y)
    {
      var nullable = ComparerUtils.HandleNullEqualityComparison(x, y);
      if (nullable.HasValue)
        return nullable.GetValueOrDefault();
      return SimpleVariantEqualityComparer.Instance.Equals(x, y) && x.Ids.IsScrambledEquals(y.Ids) && x.Quality.Equals(y.Quality) && x.Filters.IsScrambledEquals(y.Filters) && x.Info.CompareContent(y.Info) && x.Samples.CompareContent<string, IVcfSample>(y.Samples, SampleEqualityComparer);
    }

    public int GetHashCode(IVcfVariant vcfVariant) => ((((SimpleVariantEqualityComparer.Instance.GetHashCode(vcfVariant) * 397 ^ HashCodeUtils.GenerateForEnumerables(vcfVariant.Ids, false)) * 397 ^ vcfVariant.Quality.Select(q => q.GetHashCode()).GetOrElse(0)) * 397 ^ HashCodeUtils.GenerateForEnumerables(vcfVariant.Filters, false)) * 397 ^ HashCodeUtils.GenerateForKvps(vcfVariant.Info)) * 397 ^ GetKvpEnumerableOfDictionaryHashcode(vcfVariant.Samples);

    private static int GetKvpEnumerableOfDictionaryHashcode(
      IEnumerable<KeyValuePair<string, IVcfSample>> kvpEnumerable)
    {
      return kvpEnumerable.Aggregate(0, (current, kvp) => current + (kvp.Key.GetHashCode() * 397 ^ HashCodeUtils.GenerateForKvps<string, string>(kvp.Value.SampleDictionary)));
    }
  }