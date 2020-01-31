using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using Ilmn.Das.App.Wittyer.Input;
using Ilmn.Das.App.Wittyer.Utilities;
using Ilmn.Das.App.Wittyer.Utilities.Enums;
using Ilmn.Das.App.Wittyer.Vcf.Variants;
using Ilmn.Das.Std.AppUtils.Collections;
using Ilmn.Das.Std.AppUtils.Comparers;
using Ilmn.Das.Std.AppUtils.Enums;
using Ilmn.Das.Std.AppUtils.Intervals;
using Ilmn.Das.Std.BioinformaticUtils.Contigs;
using Ilmn.Das.Std.BioinformaticUtils.GenomicFeatures;
using Ilmn.Das.Std.VariantUtils.Vcf.Headers;
using Ilmn.Das.Std.VariantUtils.Vcf.Variants;
using Ilmn.Das.Std.VariantUtils.Vcf.Variants.Samples;
using JetBrains.Annotations;
using static Ilmn.Das.App.Wittyer.Utilities.WittyerConstants.WittyerMetaInfoLineKeys;
using static Ilmn.Das.Std.VariantUtils.Vcf.VcfConstants;

namespace Ilmn.Das.App.Wittyer.Results
{
    internal static class WittyerVcfWriter
    {
        private static readonly string[] DefaultSampleNamesPair = new[] {DefaultTruthSampleName, DefaultQuerySampleName};
        private static readonly string NoOverlapString = FailedReason.NoOverlap.ToString();

        internal static IEnumerable<string> GenerateVcfStrings([CanBeNull] IWittyerResult queryResult, [CanBeNull] IWittyerResult truthResult,
            [CanBeNull] string cmdLine)
        {
            if (truthResult == null && queryResult == null)
                throw new InvalidOperationException(
                    $"called {nameof(GenerateVcfStrings)} when both {nameof(truthResult)} & {nameof(queryResult)} was null!");

            if (truthResult?.IsTruth == false || queryResult?.IsTruth == true)
                throw new InvalidDataException(
                    $"Passed in the wrong {nameof(IWittyerResult)} for {nameof(truthResult)} or {nameof(queryResult)}!");

            IEnumerable<(IVcfVariant variant, bool? isTruth)> elements = ImmutableList<(IVcfVariant, bool?)>.Empty;
            
            if (truthResult != null)
            {
                var isTruthValue = queryResult == null ? null : (bool?) true;
                elements = elements.Concat(ProcessVariants(truthResult, isTruthValue).Select(v => (v, isTruthValue)));
            }

            if (queryResult != null)
            {
                var isTruthValue = truthResult == null ? null : (bool?) false;
                elements = elements.Concat(ProcessVariants(queryResult, isTruthValue).Select(v => (v, isTruthValue)));
            }

            var count = 0U;
            var comparer = CreateComparer(queryResult?.Contigs, truthResult?.Contigs);
            foreach (var line in GetMergedWittyerVcfHeaderLocal())
                yield return line;

            foreach (var line in elements.OrderBy(x => x.variant, comparer)
                .Select(x => ToString(x.variant, x.isTruth)))
            {
                count++;
                yield return line;
            }

            if (truthResult is IMutableWittyerResult truth
                && queryResult is IMutableWittyerResult query
                && count != truth.NumEntries + query.NumEntries)
                throw new InvalidDataException(
                    "Final VCF entry count was not the same as expected. Please contact witty.er developer!");

            IEnumerable<string> GetMergedWittyerVcfHeaderLocal()
            {
                if (truthResult == null)
                    return ToSingleSampleHeader(queryResult);

                if (queryResult == null)
                    return ToSingleSampleHeader(truthResult);

                return truthResult.VcfHeader.MergedWith(queryResult.VcfHeader,
                    SamplePair.Create(truthResult.SampleName, queryResult.SampleName), cmdLine);
                
                IEnumerable<string> ToSingleSampleHeader(IWittyerResult result)
                {
                    var builder = result.VcfHeader.ToBuilder();

                    var wittyerLines = VcfHeaderUtils.GenerateWittyerLines(
                        result.VcfHeader.ColumnMetaInfoLines.InfoLines.Values,
                        result.VcfHeader.ColumnMetaInfoLines.SampleFormatLines.Values, cmdLine);

                    foreach (var line in wittyerLines)
                        builder.AddLine(line);

                    return builder.Build().ToStrings().Select(line =>
                        line.StartsWith(Header.MetaPrefix)
                            ? line
                            : Header.Prefix +
                              Header.ColumnNames.MinimumRequiredPlusSampleFormat
                                  .FollowedWith(result.SampleName)
                                  .StringJoin(ColumnDelimiter));
                }
            }
        }
        
        [NotNull]
        internal static string ToString([NotNull] IVcfVariant variant, bool? isTruth)
        {
            var ret = variant.ToStrings().Take(FormatIndex).ToList();

            // order the info fields
            if (ret[InfoIndex] != MissingValueString)
                ret[InfoIndex] = variant.Info.OrderBy(kvp => kvp.Key)
                    .Select(kvp => kvp.Value.IsNullOrEmpty()
                        ? kvp.Key
                        : $"{kvp.Key}{InfoFieldKeyValueDelimiter}{kvp.Value}")
                    .StringJoin(InfoFieldDelimiter);

            // add format column
            ret.Add(variant.Samples[0].SampleDictionary.Keys.StringJoin(SampleFieldDelimiter));

            if (isTruth == null)
            {
                var values = variant.Samples[0].SampleDictionary.Values;
                ret.Add(values.All(it => string.IsNullOrEmpty(it) || it == MissingValueString)
                    ? MissingValueString
                    : values.StringJoin(SampleFieldDelimiter));
            }
            else if (isTruth.Value)
            {
                ret.Add(variant.Samples[0].SampleDictionary.Values.StringJoin(SampleFieldDelimiter));
                ret.Add(MissingValueString);
            }
            else
            {
                ret.Add(MissingValueString);
                ret.Add(variant.Samples[1].SampleDictionary.Values.StringJoin(SampleFieldDelimiter));
            }

            return ret.StringJoin(ColumnDelimiter);
        }

        internal static IEnumerable<IVcfVariant> ProcessVariants([NotNull] IWittyerResult result, bool? isTruth)
        {
            var sampleIndex = isTruth == false ? 1 : 0;

            foreach (var variants in result.Variants.Values)
            foreach (var variant in variants)
            foreach (var ret in ConvertToVcfVariant(variant))
                yield return ret;

            foreach (var variants in result.BreakendPairsAndInsertions.Values)
            foreach (var variant in variants)
            foreach (var ret in ConvertToVcfVariant(variant))
                yield return ret;

            foreach (var ret in result.NotAssessedVariants.Select(ConvertToUnsupportedVcfVariant))
                yield return ret;

            IVcfVariant ConvertToUnsupportedVcfVariant(IVcfVariant originalVariant)
                => isTruth == null
                    ? originalVariant
                    : originalVariant.ToBuilder().SetSamples(
                            GetClearedSampleBuilder(originalVariant.Samples[0].SampleDictionary,
                                DefaultTruthSampleName, DefaultQuerySampleName).Build())
                        .Build();

            IEnumerable<IVcfVariant> ConvertToVcfVariant(IWittyerSimpleVariant originalVariant)
            {
                //Info tag
                var win = originalVariant.Win.ToString();
                var annotations = originalVariant.OverlapInfo;
                if (annotations.Count > WittyerConstants.MaxNumberOfAnnotations)
                    annotations = annotations.Take(WittyerConstants.MaxNumberOfAnnotations).ToList();

                var where = annotations.Count == 0
                    ? MissingValueString
                    : annotations.Select(x => x.Where.ToString())
                        .StringJoin(WittyerConstants.InfoValueDel);
                var who = annotations.Count == 0
                    ? MissingValueString
                    : annotations.Select(x => x.Who).StringJoin(WittyerConstants.InfoValueDel);
                var wow = !originalVariant.VariantType.HasOverlappingWindows ||
                          annotations.Count == 0
                    ? MissingValueString
                    : annotations.Select(x => ToWowString(x.Wow))
                        .StringJoin(WittyerConstants.InfoValueDel);

                var infoDict = new Dictionary<string, string>
                    {
                        {Win, win},
                        {Where, where},
                        {Who, who},
                        {Wow, wow}
                    };

                var samples = AddWitTags(originalVariant.Sample.GetOriginalSample()?.SampleDictionary, isTruth == null
                    ? new[] {originalVariant.Sample.GetOriginalSample()?.SampleName ?? "SAMPLE"}
                    : DefaultSampleNamesPair);

                var updatedInfo = originalVariant.OriginalVariant.Info.ToImmutableDictionary().SetItems(infoDict);
                var firstVariant = originalVariant.OriginalVariant.ToBuilder().SetInfo(updatedInfo).SetSamples(samples);


                yield return firstVariant.Build();

                // insertions are secretly two breakends repeated.
                if (originalVariant is IWittyerBnd bnd && !ReferenceEquals(bnd.OriginalVariant, bnd.EndOriginalVariant))
                {
                    var sample = bnd.EndOriginalVariant.Samples.Values.FirstOrDefault();
                    samples = AddWitTags(sample?.SampleDictionary, isTruth == null
                        ? new[] { sample?.SampleName ?? "SAMPLE" }
                        : DefaultSampleNamesPair);
                    yield return bnd.EndOriginalVariant.ToBuilder()
                        .SetInfo(bnd.EndOriginalVariant.Info.ToImmutableDictionary().SetItems(infoDict)).SetSamples(samples).Build();}

                string ToWowString(IInterval<uint> interval)
                    => interval == null ? MissingValueString : $"{interval.Start}-{interval.Stop}";

                SampleDictionaries AddWitTags(IReadOnlyDictionary<string, string> sampleDict, string[] sampleNames)
                    => GetClearedSampleBuilder(sampleDict,
                            sampleNames)
                        .SetSampleField(sampleIndex,
                            (Wit, originalVariant.Sample.Wit.ToStringDescription()))
                        .SetSampleField(sampleIndex,
                            (Why,
                                originalVariant.Sample.Why.Count == 0
                                    ? NoOverlapString
                                    : originalVariant.Sample.Why.Select(x => x.ToStringDescription())
                                        .StringJoin(WittyerConstants.SampleValueDel)))
                        .SetSampleField(sampleIndex,
                            (What,
                                originalVariant.Sample.What.Count == 0
                                    ? MissingValueString
                                    : originalVariant.Sample.What.Select(x => x.ToStringDescription())
                                        .StringJoin(WittyerConstants.SampleValueDel))).Build();
            }

            SampleDictionaryBuilder GetClearedSampleBuilder(IReadOnlyDictionary<string, string> sampleDict, params string[] sampleNames)
            {
                var builder = SampleDictionaries.CreateBuilder();
                foreach (var sampleName in sampleNames)
                    builder.AddSample(sampleName);
                var ret = builder.MoveOnToDictionaries();

                if (sampleDict == null)
                    return ret;

                foreach (var kvp in sampleDict)
                    ret.SetSampleField(sampleIndex, (kvp.Key, kvp.Value));

                return ret;
            }
        }

        [NotNull]
        [Pure]
        internal static CustomClassComparer<IVcfVariant> CreateComparer([CanBeNull] IReadOnlyList<IContigInfo> queryContigs, 
            [CanBeNull] IReadOnlyList<IContigInfo> truthContigs)
        {
            IReadOnlyCollection<IContigInfo> less = ImmutableList<IContigInfo>.Empty, more;
            if (queryContigs == null)
                more = truthContigs ?? ImmutableList<IContigInfo>.Empty;
            else if (truthContigs == null)
                more = queryContigs;
            else if (truthContigs.Count > queryContigs.Count)
            {
                less = queryContigs;
                more = truthContigs;
            }
            else
            {
                less = truthContigs;
                more = queryContigs;
            }

            var dict = more.Select((c, i) => (c.Name, i)).ToDictionary(t => t.Name, t => t.i);
            if (less.Count == 0 || less.All(contig => dict.ContainsKey(contig.Name)))
                return new CustomClassComparer<IVcfVariant>((v1, v2) => Compare(in v1, dict, in v2));

            // not superset, so try to make a reasonable dictionary based on if it ends with a number.
            var numberDict = new Dictionary<string, int>();
            var stringSet = new HashSet<string>();
            foreach (var contig in more.Concat(less).Select(c => c.Name))
            {
                if (stringSet.Contains(contig) || numberDict.ContainsKey(contig))
                    continue;

                var ind = GetChrInt(contig);

                if (ind < 0)
                    stringSet.Add(contig);
                else
                    numberDict.Add(contig, ind);
            }

            if (numberDict.Count == 0)
                return new CustomClassComparer<IVcfVariant>((v1, v2)
                    => CompareStrings(in v1, ContigAndPositionComparer.Default.Compare(v1, v2), in v2));

            var j = 0;
            dict.Clear();
            // ReSharper disable once LoopCanBeConvertedToQuery // closed capture warning if changed to linq.
            foreach (var group in numberDict
                    .GroupBy(kvp => kvp.Value) // in case there are repeated ints like chrom11 and chr11
                    .OrderBy(x => x.Key)) // order the group by the numbers
                                          // within the groups, ordery by the names, like chr11 comes before chrom11
                foreach (var name in group.OrderBy(kvp => kvp.Key).Select(kvp => kvp.Key))
                    dict.Add(name, j++);

            foreach (var name in stringSet.OrderBy(x => x))
                dict.Add(name, j++);

            return new CustomClassComparer<IVcfVariant>((v1, v2) => Compare(in v1, dict, in v2));

            int Compare(in IVcfVariant v1, IReadOnlyDictionary<string, int> lookup, in IVcfVariant v2)
            {
                if (!lookup.TryGetValue(v1.Contig.Name, out var ind1) ||
                    !lookup.TryGetValue(v2.Contig.Name, out var ind2))
                {
                    var comp = string.Compare(v1.Contig.Name, v2.Contig.Name, StringComparison.Ordinal);
                    if (comp != 0) return comp;
                    comp = v1.Position.CompareTo(v2.Position);

                    return CompareStrings(in v1, comp, in v2);
                }

                var compare = ind1.CompareTo(ind2);
                return compare != 0 ? compare : CompareStrings(in v1, v1.Position.CompareTo(v2.Position), in v2);
            }

            int CompareStrings(in IVcfVariant v1, int compare, in IVcfVariant v2)
                => compare != 0
                    ? compare
                    : string.Compare(v1.ToString(), v2.ToString(),
                        StringComparison.Ordinal);

            // this only supports ints that are at the end of a string, like chr11
            int GetChrInt(string contig)
            {
                var i = contig.Length;
                while (i > 0 && char.IsDigit(contig[i - 1]))
                    i--;

                if (i == contig.Length) // no digits.
                    return int.MinValue;

                if (i != 0 && contig[i - 1] == '.')
                    return int.MinValue; // decoy contigs

                return i <= 1 ? int.Parse(contig) : int.Parse(contig.Substring(i));
            }
        }
    }
}