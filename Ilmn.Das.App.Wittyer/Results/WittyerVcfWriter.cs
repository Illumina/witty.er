using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using Ilmn.Das.App.Wittyer.Input;
using Ilmn.Das.App.Wittyer.Utilities;
using Ilmn.Das.App.Wittyer.Utilities.Enums;
using Ilmn.Das.App.Wittyer.Vcf.Variants;
using Ilmn.Das.Core.BgZip;
using Ilmn.Das.Core.Tries.Extensions;
using Ilmn.Das.Std.AppUtils.Collections;
using Ilmn.Das.Std.AppUtils.Enums;
using Ilmn.Das.Std.AppUtils.Intervals;
using Ilmn.Das.Std.AppUtils.Misc;
using Ilmn.Das.Std.BioinformaticUtils.GenomicFeatures;
using Ilmn.Das.Std.BioinformaticUtils.Tabix;
using Ilmn.Das.Std.VariantUtils.VariantTypes;
using Ilmn.Das.Std.VariantUtils.Vcf;
using Ilmn.Das.Std.VariantUtils.Vcf.Headers;
using Ilmn.Das.Std.VariantUtils.Vcf.Headers.MetaInfoLines;
using Ilmn.Das.Std.VariantUtils.Vcf.Variants;
using Ilmn.Das.Std.VariantUtils.Vcf.Variants.Samples;
using JetBrains.Annotations;
using static Ilmn.Das.App.Wittyer.Utilities.WittyerConstants.WittyMetaInfoLineKeys;

namespace Ilmn.Das.App.Wittyer.Results
{
    internal class WittyerVcfWriter
    {
        private static WittyerVcfResult _queryResult;
        private static IVcfHeader _queryHeader;
        private static WittyerVcfResult _truthResult;
        private static IVcfHeader _truthHeader;
        private static string _cmdLine;

        internal WittyerVcfWriter(WittyerVcfResult queryResult, IVcfHeader queryHeader, WittyerVcfResult truthResult,
            IVcfHeader truthHeader, string cmdLine)
        {
            _queryResult = queryResult;
            _queryHeader = queryHeader;
            _truthResult = truthResult;
            _truthHeader = truthHeader;
            _cmdLine = cmdLine;

        }

        [NotNull]
        internal FileInfo WriteOutputFile(DirectoryInfo outputDir)
        {
            var outputFile = GenerateOutputFile(outputDir);
            var elements = ProcessVariants(_truthResult, true).Concat(ProcessVariants(_queryResult, false)).ToList();
            elements.Sort(ContigAndPositionComparer.Default);

            GetMergedWittyerVcfHeader().Concat(elements.Select(x => x.ToString())).WriteToCompressedFile(outputFile);

            outputFile.TryEnsureTabixed()
                .DoOnFailure(e => Console.Error.WriteLine($"Proceed with vcf not tabixed: {e}"));

            return outputFile;
        }

        [NotNull]
        private FileInfo GenerateOutputFile([NotNull] FileSystemInfo outDir)
        {
            var truth = $"{_truthResult.SampleName}";
            var query = $"{_queryResult.SampleName}";

            return Path.Combine(outDir.FullName, $"Wittyer.{truth}.Vs.{query}{WittyerConstants.VcfGzSuffix}")
                .ToFileInfo();
        }

        [NotNull]
        private IEnumerable<IVcfVariant> ProcessVariants([NotNull] WittyerVcfResult variants, bool isTruth)
        {

            var result = new List<IVcfVariant>();

            foreach (var variant in variants.NormalVariants)
            {
                result.AddRange(ConvertToVcfVariant(variant, isTruth));
            }

            foreach (var variant in variants.BreakendPairs)
            {
                result.AddRange(ConvertToVcfVariant(variant, isTruth));
            }

            result.AddRange(variants.NonEvaluatedVariants.Select(x => ConvertToUnsupportedVcfVariant(x, isTruth)));

            return result;
        }

        [NotNull]
        private IVcfVariant ConvertToUnsupportedVcfVariant([NotNull] IVcfVariant originalVariant, bool isTruth)
        {
            var sampleBuilder = GetClearedSampleBuilder(originalVariant.Samples[0].SampleDictionary, isTruth ? 0 : 1);
            return originalVariant.ToBuilder().SetSamples(sampleBuilder.Build()).Build();
        }

        [NotNull, ItemNotNull]
        private IEnumerable<IVcfVariant> ConvertToVcfVariant([NotNull] IWittyerSimpleVariant originalVariant, bool isTruth)
        {
            var result = new List<IVcfVariant>();
            var nonWowType = WittyerConstants.NoOverlappingWindowTypes;

            var sampleIndex = isTruth ? 0 : 1;

            //Info tag
            var win = originalVariant.Win.ToString();
            var where = originalVariant.OverlapInfo.Count == 0 ? VcfConstants.MissingValueString 
                : originalVariant.OverlapInfo.Select(x => x.Where.ToString())
                .StringJoin(WittyerConstants.InfoValueDel);
            var who = originalVariant.OverlapInfo.Count == 0 ? VcfConstants.MissingValueString 
                : originalVariant.OverlapInfo.Select(x => x.Who).StringJoin(WittyerConstants.InfoValueDel);
            var wow = nonWowType.Contains(originalVariant.VariantType) || originalVariant.OverlapInfo.Count == 0 
                    ? VcfConstants.MissingValueString : originalVariant.OverlapInfo.Select(x => ToWowString(x.Wow))
                    .StringJoin(WittyerConstants.InfoValueDel);

            var infoDict = new Dictionary<string, string>
            {
                { Win, win},
                {Where, where },
                {Who, who },
                {Wow, wow }
            };

            var updatedInfo = originalVariant.OriginalVariant.Info.ToImmutableDictionary().AddRange(infoDict);
            var sBuilder = GetSampleBuilder();
            var firstVariant = originalVariant.OriginalVariant.ToBuilder().SetInfo(updatedInfo).SetSamples(sBuilder.Build()).Build();
            result.Add(firstVariant);

            if (originalVariant is IWittyerBnd bnd && originalVariant.VariantType.Equals(SvType.TranslocationBreakend))
            {
                var secondVariant = bnd.EndOriginalVariant.ToBuilder()
                    .SetInfo(bnd.EndOriginalVariant.Info.ToImmutableDictionary().AddRange(infoDict))
                    .SetSamples(sBuilder.Build()).Build();
                result.Add(secondVariant);
            }

            SampleDictionaryBuilder GetSampleBuilder()
            {
                var localBuilder =
                    GetClearedSampleBuilder(
                        originalVariant.Sample.GetOriginalSample()?.SampleDictionary,
                        sampleIndex);
               return localBuilder.SetSampleField(sampleIndex, (Wit, originalVariant.Sample.Wit.ToStringDescription()))
                    .SetSampleField(sampleIndex,
                        (Why,
                            originalVariant.Sample.Why.Count == 0 ? FailedReason.NoOverlap.ToStringDescription() 
                                : originalVariant.Sample.Why.Select(x => x.ToStringDescription())
                                .StringJoin(WittyerConstants.SampleValueDel)))
                    .SetSampleField(sampleIndex,
                        (What,
                            originalVariant.Sample.What.Count == 0 ? VcfConstants.MissingValueString
                                : originalVariant.Sample.What.Select(x => x.ToStringDescription())
                                .StringJoin(WittyerConstants.SampleValueDel)));
            }

            return result;
        }


        [NotNull]
        private SampleDictionaryBuilder GetClearedSampleBuilder(
            [CanBeNull] IReadOnlyDictionary<string, string> sampleDict, int sampleIndex)
        {
            var ret = SampleDictionaries.CreateBuilder().AddSample(DefaultTruthSampleName)
                .AddSample(DefaultQuerySampleName).MoveOnToDictionaries();

            if (sampleDict == null)
                return ret;

            foreach (var kvp in sampleDict)
                ret.SetSampleField(sampleIndex, (kvp.Key, kvp.Value));

            return ret;
        }

        [NotNull]
        private static string ToWowString([CanBeNull] IInterval<uint> wow)
        {
            return wow == null ? VcfConstants.MissingValueString : $"{wow.Start}-{wow.Stop}";
        }

        [NotNull]
        private static IEnumerable<string> GetMergedWittyerVcfHeader()
        {
            var mergedMetaLines = _truthHeader.MergeMetaLines(_cmdLine, _queryHeader);
            var pair = SamplePair.Create(_truthResult.SampleName, _queryResult.SampleName);
            return 
                ToWittyBuilder().AddSampleMetaInfo(_truthHeader, pair, _queryHeader, mergedMetaLines).Build().ToStrings();
        }

        [NotNull]
        private static VcfHeader.Builder ToWittyBuilder()
        {
            var builder = VcfHeader.CreateBuilder(_truthHeader.Version)
                .AddSampleColumn(DefaultTruthSampleName).AddSampleColumn(DefaultQuerySampleName)
                .AddOtherLine(
                    BasicMetaLine.Create($"{WittyerConstants.WittyerVersionHeader}",
                        $"witty.erV{WittyerConstants.CurrentVersion}"));

            _truthHeader.ReferenceGenome.DoOnSuccess(r => builder.SetReference(r));

            return builder;
        }



    }
}
