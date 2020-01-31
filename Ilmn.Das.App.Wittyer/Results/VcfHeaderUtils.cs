using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using Ilmn.Das.App.Wittyer.Input;
using Ilmn.Das.App.Wittyer.Utilities;
using Ilmn.Das.App.Wittyer.Utilities.Enums;
using Ilmn.Das.Core.Tries.Extensions;
using Ilmn.Das.Std.AppUtils.Misc;
using Ilmn.Das.Std.BioinformaticUtils.Contigs;
using Ilmn.Das.Std.VariantUtils.Vcf.Headers;
using Ilmn.Das.Std.VariantUtils.Vcf.Headers.MetaInfoLines;
using JetBrains.Annotations;
using static Ilmn.Das.Std.VariantUtils.Vcf.VcfConstants.Header.MetaInfoLines.Keys;
using static Ilmn.Das.App.Wittyer.Utilities.WittyerConstants.WittyMetaInfoLineKeys;
using static Ilmn.Das.App.Wittyer.Utilities.WittyerConstants.WittyMetaInfoLines;

namespace Ilmn.Das.App.Wittyer.Results
{
    internal static class VcfHeaderUtils
    {

        [NotNull]
        internal static List<IBasicMetaLine> MergeMetaLines([NotNull] this IVcfHeader truthHeader,
            [NotNull] string cmdLine, [NotNull] IVcfHeader queryHeader)
        {
            var ret = new List<IBasicMetaLine>();
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

            ret.AddRange(altLines
                .Concat(filterLines)
                .Concat(contigLines)
                .Concat(sampleMetaLines)
                .Concat(infoLines.Where(line => !IsConflicted(line, WowOverlappingWindowHeader)
                                                && !IsConflicted(line, WhoMatchedEventHeader)
                                                && !IsConflicted(line, WinStratificationHeader)
                                                && !IsConflicted(line, WhereBorderDistanceHeader)))
                .Concat(sampleFormatLines.Where(line => !IsConflicted(line, WitDecisionHeader)
                                                        && !IsConflicted(line, WhatMatchedTypeHeader)
                                                        && !IsConflicted(line, WhyFailedReasonHeader)))
                .Concat(BasicMetaLine.Create(WittyerConstants.WitCmdHeaderKey, cmdLine)
                    .FollowedBy(BasicMetaLine.Create("fileDate", DateTime.Now.ToString("yyyyMMdd")),
                        WowOverlappingWindowHeader, WhoMatchedEventHeader, WinStratificationHeader,
                        WhereBorderDistanceHeader, WitDecisionHeader, WhatMatchedTypeHeader, WhyFailedReasonHeader)));
            return ret;
        }

        private static IEnumerable<IMetaInfoLine> MergeContigLines(
            IImmutableDictionary<string, ContigMetaInfoLine> query,
            IImmutableDictionary<string, ContigMetaInfoLine> truth)
        {
            if(truth.Count == 0)
                throw new InvalidDataException("Contig lines are expected to be in the header!");
            var genomeType = truth.First().Value.Contig.GetGenomeType();
            var contigSet = truth;
            foreach (var kvp in query)
            {
                var newContig = kvp.Value.ConvertGenomeType(genomeType);
                if (!contigSet.Keys.Contains(newContig.Contig.Name))
                   contigSet = contigSet.Add(newContig.Contig.Name, newContig);
            }
            return contigSet.Values.OrderBy(x => x.Contig);
        }

        private static ContigMetaInfoLine ConvertGenomeType([NotNull] this ContigMetaInfoLine line, GenomeType type)
        {
            var contig = line.Contig;
            if (contig.GetGenomeType() != type) // since toGrchStyle()/toUcscStyle() does not preserve length information, this is to best preserve length
            {
                if (type == GenomeType.Grch)
                    contig = contig.ToGrchStyle();
                if (type == GenomeType.Ucsc)
                    contig = contig.ToUcscStyle();
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
            if (truth != null)
                builder.AddOtherLine(MetaInfoLine.Create(OriginalSampleNameLineKey, DefaultTruthSampleName, truth));
            var truthLines = truthHeader.ColumnMetaInfoLines.SampleLines
                .Where(l => truth == null || l.Value.Id == truth).Take(1); // take the first one or first one that matches.

            var query = samplePair?.QuerySampleName ?? queryHeader.SampleNames.FirstOrDefault();
            if (query != null)
                builder.AddOtherLine(MetaInfoLine.Create(OriginalSampleNameLineKey, DefaultQuerySampleName, query));
            var queryLines = queryHeader.ColumnMetaInfoLines.SampleLines
                .Where(l => query == null || l.Value.Id == query).Take(1);

            return MergeMetaInfoLines(truthLines, Sample, queryLines).Concat(mergedMetaLines)
                .Aggregate(builder, (acc, line) => acc.AddLine(line));
        }

        [NotNull]
        private static IEnumerable<IMetaInfoLine> MergeMetaInfoLines(
            [NotNull] this IEnumerable<KeyValuePair<string, IMetaInfoLine>> truthLines,
            [NotNull] string lineKey, [NotNull] IEnumerable<KeyValuePair<string, IMetaInfoLine>> queryLines)
            => MergeMetaLines(truthLines, queryLines, IsDescriptionEqual,
                (t, q) => MetaInfoLine.Create(lineKey, t.Id, MergeDescription(t, q)));

        [NotNull]
        private static IEnumerable<IMetaInfoLine> MergeTypedMetaLines(
            [NotNull] this IEnumerable<KeyValuePair<string, ITypedMetaInfoLine>> truthLines,
            [NotNull] string lineKey, [NotNull] IEnumerable<KeyValuePair<string, ITypedMetaInfoLine>> queryLines)
            => MergeMetaLines(truthLines, queryLines,
                (t, q) => t.Type == q.Type && t.Number == q.Number && IsDescriptionEqual(t, q),
                (t, q) => TypedMetaInfoLine.Create(MetaInfoLine.Create(lineKey, t.Id, MergeDescription(t, q)),
                    t.Type == q.Type ? t.Type : TypeField.String, t.Number == q.Number ? t.Number : NumberField.Any));

        private static bool IsDescriptionEqual([NotNull] IMetaInfoLine t, [NotNull] IMetaInfoLine q)
            => string.Equals(q.Description.Trim(), t.Description.Trim(), StringComparison.InvariantCultureIgnoreCase);

        [NotNull]
        private static string MergeDescription([NotNull] IMetaInfoLine t, [NotNull] IMetaInfoLine q)
            => $"{DefaultTruthSampleName}: {t.Description}        {DefaultQuerySampleName}: {q.Description}";

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
            [NotNull] IEnumerable<KeyValuePair<string, T>> truthMetaLines,
            [NotNull] IEnumerable<KeyValuePair<string, T>> queryMetaLines,
            Func<T, T, bool> equalityTest, Func<T, T, IMetaInfoLine> mergeFunc) where T : IMetaInfoLine
        {
            var query = queryMetaLines.ToList();
            var mergedItem = new List<string>();
            foreach (var line in truthMetaLines)
            {
                var count = 0;
                while (!line.Value.Id.Equals(query[count].Value.Id) && query.Count < count)
                    count++;
                if (line.Value.Id.Equals(query[count].Value.Id))
                {
                    mergedItem.Add(line.Value.Id);
                    if (equalityTest(line.Value, query[count].Value))
                        yield return line.Value;
                    yield return mergeFunc(line.Value, query[count].Value);
                }

                yield return line.Value;

            }

            foreach (var line in query)
            {
                if (!mergedItem.Contains(line.Value.Id))
                    yield return line.Value;
            }

        }

        
    }
}
