using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Linq;
using Ilmn.Das.App.Wittyer.Utilities;
using Ilmn.Das.App.Wittyer.Vcf.Samples;
using Ilmn.Das.App.Wittyer.Vcf.Variants;
using Ilmn.Das.Core.Tries;
using Ilmn.Das.Core.Tries.Extensions;
using Ilmn.Das.Std.AppUtils.Collections;
using Ilmn.Das.Std.AppUtils.Collections.Generic;
using Ilmn.Das.Std.BioinformaticUtils.Contigs;
using Ilmn.Das.Std.BioinformaticUtils.Nucleotides;
using Ilmn.Das.Std.VariantUtils.Vcf;
using Ilmn.Das.Std.VariantUtils.Vcf.Exceptions;
using Ilmn.Das.Std.VariantUtils.Vcf.Variants.Samples;
using JetBrains.Annotations;
using SampleDictionaries = Ilmn.Das.App.Wittyer.Vcf.Samples.SampleDictionaries;
using VcfVariant = Ilmn.Das.App.Wittyer.Vcf.Variants.VcfVariant;

namespace Ilmn.Das.App.Wittyer.Vcf.Readers;

/// <summary>
/// Pasrer for VCFs
/// </summary>
public class VcfVariantParser
{
    private static readonly char[] InfoKvpDelim = "=".ToCharArray();
    private static readonly ConcurrentDictionary<string, ITry<IList<string>>> FormatFieldDict = new();
    private static readonly ITry<IList<string>> Nothing = TryFactory.Nothing<IList<string>>();

    [Pure]
    internal static ITry<IVcfVariant> TryParse(
      string vcfLine,
      VcfVariantParserSettings variantParserSettings)
    {
      var invalidColumnIndexes = new HashSet<VcfColumn>();
      var strArray = vcfLine.Split(new[]
      {
        VcfConstants.ColumnDelimiterChar
      });
      if (strArray.Length != variantParserSettings.ExpectedNumberOfColumns)
        return TryFactory.Failure<IVcfVariant>(new VcfLineWithInvalidNumberOfColumnsException(vcfLine, strArray, variantParserSettings.ExpectedNumberOfColumns, strArray.Length));
      strArray.Take(variantParserSettings.ExpectedNumberOfColumns).Select<string, (string, int)>((value, index) => (value, index)).ForEach(x =>
      {
        if (x.Item1 != string.Empty)
          return;
        invalidColumnIndexes.Add((VcfColumn)x.Item2);
      });
      var str1 = strArray[0];
      var contig = variantParserSettings.ReferenceGenome == null || !variantParserSettings.ReferenceGenome.ContigDictionary.TryGetValue(str1, out var contigInfo) ? ContigInfo.Create(str1) : contigInfo;
      if (!uint.TryParse(strArray[1], out var result1))
        invalidColumnIndexes.Add(VcfColumn.Position);
      var toStrings1 = ParseToStrings(strArray[2], VcfColumn.Id, invalidColumnIndexes, VcfConstants.IdFieldDelimiterChar);
      var str2 = strArray[3];
      var refString = DnaString.Create(str2);
      var toStrings2 = ParseToStrings(strArray[4], VcfColumn.Alts, invalidColumnIndexes, VcfConstants.AltDelimiterChar);
      var s = strArray[5];
      var quality = VcfConstants.MissingValueQuality;
      if (s != ".")
      {
        if (double.TryParse(s, out var result2))
          quality = TryFactory.Success(result2);
        else
          invalidColumnIndexes.Add(VcfColumn.Quality);
      }
      var fieldString = strArray[6];
      IReadOnlyList<string> stringList = WittyerConstants.PassFilterList;
      if (fieldString != "PASS")
      {
        stringList = ParseToStrings(fieldString, VcfColumn.Filter, invalidColumnIndexes, VcfConstants.FilterFieldDelimiterChar);
        if (stringList.Any(filter => filter.Equals("PASS")))
          invalidColumnIndexes.Add(VcfColumn.Filter);
      }
      var infoField = strArray[7];
      IReadOnlyDictionary<string, string> info = ImmutableDictionary<string, string>.Empty;
      if (infoField != string.Empty && infoField.Trim() != ".")
        info = ParseInfo(infoField, invalidColumnIndexes);
      if (FormatFieldDict.Count > 500)
        FormatFieldDict.Clear();
      var samples = ParseSamples(variantParserSettings.SampleToIndexDictionary, strArray, invalidColumnIndexes);
      return invalidColumnIndexes.Count > 0 ? TryFactory.Failure<IVcfVariant>(VcfVariantFormatException.Create(vcfLine, invalidColumnIndexes.ToImmutableHashSet(), strArray)) : TryFactory.Success(new VcfVariant(contig, result1, toStrings1, refString, toStrings2, quality, stringList, info, samples, vcfLine));
    }

    private static IReadOnlyList<string> ParseToStrings(
      string fieldString,
      VcfColumn columnType,
      ISet<VcfColumn> invalidColumnIndexes,
      char delimiter)
    {
      if (fieldString == ".")
        return ImmutableList<string>.Empty;
      List<string> stringList = new();
      var str1 = fieldString;
      var chArray = new char[1]{ delimiter };
      foreach (var str2 in str1.Split(chArray))
      {
        if (str2 != null && str2 != ".")
        {
          stringList.Add(str2);
        }
        else
        {
          invalidColumnIndexes.Add(columnType);
          break;
        }
      }
      return stringList.AsReadOnly();
    }

    private static IReadOnlyDictionary<string, string> ParseInfo(
      string infoField,
      ISet<VcfColumn> invalidColumnIndexes)
    {
      Dictionary<string, string> dictionary = new();
      foreach (var strArray in infoField.Split(new char[1]
               {
                 VcfConstants.InfoFieldDelimiterChar
               }).Select((Func<string, string[]>) (infoItem => infoItem.Split(InfoKvpDelim, 2))))
      {
        var key = strArray[0];
        if (dictionary.ContainsKey(key))
        {
          invalidColumnIndexes.Add(VcfColumn.Info);
          return ImmutableDictionary<string, string>.Empty;
        }
        dictionary.Add(key, strArray.Length > 1 ? strArray[1] : string.Empty);
      }
      return new ReadOnlyDictionary<string, string>(dictionary);
    }

    private static SampleDictionaries ParseSamples(
      IReadOnlyDictionary<string, int> sampleToIndexDictionary,
      IReadOnlyList<string> splitVcfLine,
      ISet<VcfColumn> invalidColumnIndexes)
    {
      if (splitVcfLine.Count <= 8)
        return SampleDictionaries.Empty;
      var key1 = splitVcfLine[8];
      if (!FormatFieldDict.TryGetValue(key1, out ITry<IList<string>> option))
      {
        var strArray = key1.Split(new char[1]
        {
          VcfConstants.SampleFieldDelimiterChar
        });
        HashSet<string> stringSet = new();
        option = TryFactory.Success(strArray);
        for (var index = 0; index < strArray.Length; ++index)
        {
          var str = strArray[index];
          if (stringSet.Contains(str))
          {
            option = Nothing;
            break;
          }
          stringSet.Add(str);
        }
        FormatFieldDict.GetOrAdd(key1, (Func<string, ITry<IList<string>>>) (_ => option));
      }
      if (option is IFailure<IList<string>>)
      {
        invalidColumnIndexes.Add(VcfColumn.SampleFormat);
        return SampleDictionaries.Empty;
      }
      List<IVcfSample> vcfSampleList = new();
      foreach (var (key2, num) in sampleToIndexDictionary)
      {
        var sample = ParseSample(key2, option.GetOrThrow(), splitVcfLine[num + 9]);
        if (sample == null)
        {
          invalidColumnIndexes.Add(VcfColumn.SampleFormat);
          return SampleDictionaries.Empty;
        }
        vcfSampleList.Add(sample);
      }
      return SampleDictionaries.Create(sampleToIndexDictionary, vcfSampleList.AsReadOnly());
    }

    private static IVcfSample? ParseSample(
      string sampleName,
      IList<string> splitFormat,
      string vcfSampleField)
    {
      var strArray = vcfSampleField.Split(new char[1]
      {
        VcfConstants.SampleFieldDelimiterChar
      });
      if (strArray.Length > splitFormat.Count)
        return null;
      var source = OrderedDictionary<string, string>.Create();
      for (var index = 0; index < splitFormat.Count; ++index)
        source.Add(splitFormat[index], index < strArray.Length ? strArray[index] : ".");
      return VcfSample.Create(sampleName, source.ToReadOnlyOrderedDictionary());
    }
}