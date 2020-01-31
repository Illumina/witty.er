using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using Ilmn.Das.App.Wittyer.Input;
using Ilmn.Das.App.Wittyer.Utilities;
using Ilmn.Das.App.Wittyer.Utilities.Enums;
using Ilmn.Das.Core.Tries.Extensions;
using Ilmn.Das.Std.AppUtils.Collections;
using Ilmn.Das.Std.BioinformaticUtils.Contigs;
using Ilmn.Das.Std.VariantUtils.Vcf.Headers;
using Ilmn.Das.Std.VariantUtils.Vcf.Headers.MetaInfoLines;
using JetBrains.Annotations;
using static Ilmn.Das.Std.VariantUtils.Vcf.VcfConstants.Header.MetaInfoLines.Keys;
using static Ilmn.Das.App.Wittyer.Utilities.WittyerConstants.WittyerMetaInfoLineKeys;
using static Ilmn.Das.App.Wittyer.Utilities.WittyerConstants.WittyerMetaInfoLines;

namespace Ilmn.Das.App.Wittyer.Results
{
    internal static class VcfHeaderUtils
    {
        internal static IEnumerable<string> MergedWith([NotNull] this IVcfHeader truthHeader, 
            [NotNull] IVcfHeader queryHeader, [NotNull] ISamplePair pair, [CanBeNull] string cmdLine)
        {
            var mergedMetaLines = truthHeader.MergeMetaLines(cmdLine, queryHeader);
            return ToWittyBuilder()
                .AddSampleMetaInfo(truthHeader, pair, queryHeader, mergedMetaLines)
                .Build().ToStrings();

            VcfHeader.Builder ToWittyBuilder()
            {
                var builder = VcfHeader.CreateBuilder(truthHeader.Version)
                    .AddSampleColumn(DefaultTruthSampleName).AddSampleColumn(DefaultQuerySampleName);

                truthHeader.ReferenceGenome.DoOnSuccess(r => builder.SetReference(r));

                return builder;
            }
        }
        
        [NotNull]
        internal static IEnumerable<IBasicMetaLine> MergeMetaLines([NotNull] this IVcfHeader truthHeader,
            [CanBeNull] string cmdLine, [NotNull] IVcfHeader queryHeader)
        {
            var truthMetaLines = truthHeader.ColumnMetaInfoLines;

            var queryMetaLines = queryHeader.ColumnMetaInfoLines;

            var altLines = MergeMetaInfoLines(truthMetaLines.AltLines, Alt, queryMetaLines.AltLines);

            var filterLines = MergeMetaInfoLines(truthMetaLines.FilterLines, Filter, queryMetaLines.FilterLines);

            var contigLines = MergeContigLines(queryMetaLines.ContigLines, truthMetaLines.ContigLines);

            var infoLines = MergeTypedMetaLines(truthMetaLines.InfoLines, Info, queryMetaLines.InfoLines);

            var sampleFormatLines =
                MergeTypedMetaLines(truthMetaLines.SampleFormatLines, SampleFormat, queryMetaLines.SampleFormatLines);

            var sampleMetaLines = MergeTypedMetaLines(truthMetaLines.SampleMetaLines, Meta, queryMetaLines.SampleMetaLines);

            var builder = VcfHeader.CreateBuilder(truthHeader.Version)
                .AddSampleColumn(DefaultTruthSampleName).AddSampleColumn(DefaultQuerySampleName);

            truthHeader.ReferenceGenome.DoOnSuccess(r => builder.SetReference(r));

            foreach (var line in altLines.Concat(filterLines).Concat(contigLines).Concat(sampleMetaLines)
                .Concat(GenerateWittyerLines(infoLines, sampleFormatLines, cmdLine)))
                yield return line;
        }

        internal static IEnumerable<IBasicMetaLine> GenerateWittyerLines([NotNull] IEnumerable<IMetaInfoLine> infoLines,
            [NotNull] IEnumerable<IMetaInfoLine> sampleFormatLines, [CanBeNull] string cmdLine)
        {
            foreach (var line in infoLines
                .FollowedWith(WowOverlappingWindowHeader, WhoMatchedEventHeader, WinStratificationHeader, WhereBorderDistanceHeader))
                if (IsConflicted(line, WowOverlappingWindowHeader))
                    yield return WowOverlappingWindowHeader;
                else if (IsConflicted(line, WhoMatchedEventHeader))
                    yield return WhoMatchedEventHeader;
                else if (IsConflicted(line, WinStratificationHeader))
                    yield return WinStratificationHeader;
                else if (IsConflicted(line, WhereBorderDistanceHeader))
                    yield return WinStratificationHeader;
                else
                    yield return line;

            foreach (var line in sampleFormatLines.FollowedWith(WitDecisionHeader, WhatMatchedTypeHeader, WhyFailedReasonHeader))
                if (IsConflicted(line, WitDecisionHeader))
                    yield return WitDecisionHeader;
                else if (IsConflicted(line, WhatMatchedTypeHeader))
                    yield return WhatMatchedTypeHeader;
                else if (IsConflicted(line, WhyFailedReasonHeader))
                    yield return WhyFailedReasonHeader;
                else
                    yield return line;

            yield return BasicMetaLine.Create("fileDate", DateTime.Now.ToString("yyyyMMdd"));
            yield return BasicMetaLine.Create($"{WittyerConstants.WittyerVersionHeader}",
                $"witty.erV{WittyerConstants.CurrentVersion}");

            if (!string.IsNullOrWhiteSpace(cmdLine))
                yield return BasicMetaLine.Create(WittyerConstants.WitCmdHeaderKey, cmdLine);
        }

        [NotNull]
        private static IEnumerable<IMetaInfoLine> MergeContigLines(
            [NotNull] IImmutableDictionary<string, ContigMetaInfoLine> query,
            [NotNull] IImmutableDictionary<string, ContigMetaInfoLine> truth)
        {
            if(truth.Count == 0)
                throw new InvalidDataException("Contig lines are expected to be in the header!");
            var genomeType = truth.First().Value.Contig.GetGenomeType();
            var contigSet = truth;
            foreach (var kvp in query)
            {
                var newContig = kvp.Value.ConvertGenomeType(genomeType);
                if (!contigSet.ContainsKey(newContig.Contig.Name))
                   contigSet = contigSet.Add(newContig.Contig.Name, newContig);
            }
            return contigSet.Values.OrderBy(x => x.Contig);
        }

        [NotNull]
        private static ContigMetaInfoLine ConvertGenomeType([NotNull] this ContigMetaInfoLine line, GenomeType type)
        {
            var contig = line.Contig;
            if (contig.GetGenomeType() == type)
                return ContigMetaInfoLine.Create(contig);

            switch (type)
            {
                // since toGrchStyle()/toUcscStyle() does not preserve length information, this is to best preserve length
                case GenomeType.Grch:
                    contig = contig.ToGrchStyle();
                    break;
                case GenomeType.Ucsc:
                    contig = contig.ToUcscStyle();
                    break;
            }

            return ContigMetaInfoLine.Create(contig);
        }

        internal static VcfHeader.Builder AddSampleMetaInfo([NotNull] this VcfHeader.Builder builder,
            [NotNull] IVcfHeader truthHeader, [CanBeNull] ISamplePair samplePair,
            [NotNull] IVcfHeader queryHeader, [NotNull] IEnumerable<IBasicMetaLine> mergedMetaLines)
        {
            // todo: for vcf 4.3, this has ids, so should be merged via AddSampleMetaInfo.
            // get rid of pedigree lines since we don't have all the sample columns
            //builder = truthHeader.PedigreeLines.Concat(queryHeader.PedigreeLines)
            //    .Aggregate(builder, (acc, line) => acc.AddPedigreeLine(line));

            var truth = samplePair?.TruthSampleName ?? truthHeader.SampleNames.FirstOrDefault();
            var query = samplePair?.QuerySampleName ?? queryHeader.SampleNames.FirstOrDefault();

            if (truth != query && truth != null) // only add sample lines that are not the same sample names otherwise, error out.
            {
                AddSampleLine(truth, DefaultTruthSampleName);
                AddSampleLine(query, DefaultQuerySampleName);
            }

            return mergedMetaLines
                .Aggregate(builder, (acc, line) => acc.AddLine(line));

            void AddSampleLine(string sampleName, string defaultSampleName)
            {
                if (sampleName == null) return;
                builder.AddOtherLine(MetaInfoLine.Create(OriginalSampleNameLineKey, defaultSampleName, sampleName));
                var truthLine = truthHeader.ColumnMetaInfoLines.SampleLines
                    .FirstOrException(l => sampleName.Equals(l.Value.Id)).GetOrNull()?.Value;
                if (truthLine != null)
                    builder.AddLine(truthLine);
            }
        }

        [NotNull]
        private static IEnumerable<IMetaInfoLine> MergeMetaInfoLines(
            [NotNull] this IReadOnlyDictionary<string, IMetaInfoLine> truthLines,
            [NotNull] string lineKey, [NotNull] IReadOnlyDictionary<string, IMetaInfoLine> queryLines)
            => MergeMetaLines(truthLines, queryLines, IsDescriptionEqual,
                (t, q) => MetaInfoLine.Create(lineKey, t.Id, MergeDescription(t, q)));

        [NotNull]
        private static IEnumerable<IMetaInfoLine> MergeTypedMetaLines(
            [NotNull] this IReadOnlyDictionary<string, ITypedMetaInfoLine> truthLines,
            [NotNull] string lineKey, [NotNull] IReadOnlyDictionary<string, ITypedMetaInfoLine> queryLines)
            => MergeMetaLines(truthLines, queryLines,
                (t, q) => t.Type == q.Type && t.Number == q.Number && IsDescriptionEqual(t, q),
                (t, q) => TypedMetaInfoLine.Create(MetaInfoLine.Create(lineKey, t.Id, MergeDescription(t, q)),
                    t.Type == q.Type ? t.Type : TypeField.String, t.Number == q.Number ? t.Number : NumberField.Any));

        private static bool IsDescriptionEqual([NotNull] IMetaInfoLine t, [NotNull] IMetaInfoLine q)
            => string.Equals(q.Description.Trim(), t.Description.Trim(), StringComparison.InvariantCultureIgnoreCase);

        [NotNull]
        private static string MergeDescription([NotNull] IMetaInfoLine t, [NotNull] IMetaInfoLine q)
            => MergeDescription(t.Description, q.Description);

        [NotNull]
        private static string MergeDescription([NotNull] string t, [NotNull] string q)
            => $"{DefaultTruthSampleName}: {t}        {DefaultQuerySampleName}: {q}";

        private static bool IsConflicted([NotNull] IMetaInfoLine line, [NotNull] IMetaInfoLine target)
        {
            if (line.Id != target.Id)
                return false;
            if (line.Description == target.Description)
                return false;
            Console.WriteLine(
                $"WARNING: Existing {line.Id} with different description! Overwriting {line.Description} with {target.Description}...");
            return true;
        }

        [NotNull, Pure, ItemNotNull]
        private static IEnumerable<IMetaInfoLine> MergeMetaLines<T>(
            [NotNull] IReadOnlyDictionary<string, T> truthMetaLines,
            [NotNull] IReadOnlyDictionary<string, T> queryMetaLines,
            Func<T, T, bool> equalityTest, Func<T, T, IMetaInfoLine> mergeFunc) where T : IMetaInfoLine
        {
            var mergedItem = Enumerable.ToHashSet(queryMetaLines.Keys);
            foreach (var (_, line) in truthMetaLines)
            {
                var queryContains = queryMetaLines.TryGetValue(line.Id, out var queryLine);
                if (queryContains)
                    mergedItem.Remove(line.Id);
                if (queryContains && !equalityTest(line, queryLine))
                    yield return mergeFunc(line, queryLine);
                else
                    yield return line;
            }

            foreach (var key in mergedItem)
                yield return queryMetaLines[key];
        }
    }
}