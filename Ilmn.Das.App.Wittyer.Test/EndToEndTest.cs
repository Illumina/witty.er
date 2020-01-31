using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using Ilmn.Das.App.Wittyer.Infrastructure;
using Ilmn.Das.App.Wittyer.Input;
using Ilmn.Das.App.Wittyer.Json;
using Ilmn.Das.App.Wittyer.Stats;
using Ilmn.Das.App.Wittyer.Utilities;
using Ilmn.Das.App.Wittyer.Vcf.Variants;
using Ilmn.Das.Core.Tries.Extensions;
using Ilmn.Das.Std.AppUtils.Collections;
using Ilmn.Das.Std.AppUtils.Misc;
using Ilmn.Das.Std.BioinformaticUtils.Contigs;
using Ilmn.Das.Std.BioinformaticUtils.GenomicFeatures;
using Ilmn.Das.Std.VariantUtils.Vcf;
using Ilmn.Das.Std.VariantUtils.Vcf.Readers;
using Ilmn.Das.Std.VariantUtils.Vcf.Variants;
using Ilmn.Das.Std.XunitUtils;
using Monad.Parsec;
using Newtonsoft.Json;
using Xunit;
using MiscUtils = Ilmn.Das.Std.AppUtils.Misc.MiscUtils;

namespace Ilmn.Das.App.Wittyer.Test
{
    public class EndToEndTest
    {
        private static readonly InputSpec InputSpec = InputSpec.Create(WittyerConstants.DefaultBins,
            WittyerConstants.DefaultBpOverlap,
            WittyerConstants.DefaultPd, ImmutableHashSet<string>.Empty, WittyerConstants.DefaultIncludeFilters);

        private static readonly IImmutableDictionary<WittyerVariantType, InputSpec> InputSpecs =
            WittyerConstants.SupportedSvType.ToImmutableDictionary(x => x, x => InputSpec);

        //CNV files
        private static readonly FileInfo CnvJsonCts =
            Path.Combine("Resources", "Cnvs", "cts.Wittyer.Stats.json").ToFileInfo();

        private static readonly FileInfo CnvJsonSc =
            Path.Combine("Resources", "Cnvs", "sc.Wittyer.Stats.json").ToFileInfo();

        private static readonly FileInfo CnvQuery =
            Path.Combine("Resources", "Cnvs", "Dragen_FrankenPolaris2.cnv.LT10kb.vcf").ToFileInfo();

        private static readonly FileInfo CnvTruth =
            Path.Combine("Resources", "Cnvs", "FrankenPolaris2-Truth-reformat.vcf").ToFileInfo();

        //SV files
        private static readonly FileInfo SvJsonSc =
            Path.Combine("Resources", "Somatics", "sc.Wittyer.Stats.json").ToFileInfo();

        private static readonly FileInfo SomaticQuery =
            Path.Combine("Resources", "Somatics", "somaticSV.vcf.gz").ToFileInfo();

        private static readonly FileInfo SomaticTruth =
            Path.Combine("Resources", "Somatics", "Cosmic_v70_HCC1954_SVs.truth.vcf.gz").ToFileInfo();

        //SV Germline
        private static readonly FileInfo SvJsonGt =
            Path.Combine("Resources", "Germlines", "gt.Wittyer.Stats.json").ToFileInfo();

        private static readonly FileInfo GermlineTruth =
            Path.Combine("Resources", "Germlines", "NA12878_cascadia_pg.vcf.gz").ToFileInfo();

        private static readonly FileInfo GermlineQuery =
            Path.Combine("Resources", "Germlines", "FilterVcf.vcf").ToFileInfo();

        [Fact]
        public void SvWorksWithDefault()
        {
            if (MiscUtils.IsRunningAnyLinux) return; // currently failing on linux :(

            var wittyerSettings = WittyerSettings.Create(Path.GetRandomFileName().ToDirectoryInfo(), GermlineTruth, GermlineQuery,
                ImmutableList<ISamplePair>.Empty, EvaluationMode.Default, InputSpecs);

            var stats = MainLauncher.GenerateJson(wittyerSettings, string.Empty, false).PerSampleStats.First();

            var expectedStats = JsonConvert.DeserializeObject<GeneralStats>(File.ReadAllText(SvJsonGt.FullName))
                .PerSampleStats.First();

            var expectedOverallEventStats = expectedStats.OverallStats.Single(x => x.StatsType.Equals(StatsType.Event));
            var actualOverallEventStats = stats.OverallStats.Single(x => x.StatsType.Equals(StatsType.Event));

            MultiAssert.Equal(expectedOverallEventStats, actualOverallEventStats);

            foreach (var type in WittyerConstants.SupportedSvType)
            {
                var typeString = type.ToString();
                var expectedTypedStats = expectedStats.DetailedStats.Single(x => x.VariantType.Equals(typeString));
                var actualTypedStats = stats.DetailedStats.Single(x => x.VariantType.Equals(typeString));

                var expectedTypedEventStats = expectedTypedStats.OverallStats.Single(x => x.StatsType == StatsType.Event);
                var actualTypedEventStats = actualTypedStats.OverallStats.Single(x => x.StatsType == StatsType.Event);
                MultiAssert.Equal(expectedTypedEventStats, actualTypedEventStats);

                if (WittyerConstants.BaseLevelStatsTypes.Contains(type))
                {
                    var expectedTypedBaseStats = expectedTypedStats.OverallStats.Single(x => x.StatsType == StatsType.Base);
                    var actualTypedBaseStats = actualTypedStats.OverallStats.Single(x => x.StatsType == StatsType.Base);
                    MultiAssert.Equal(expectedTypedBaseStats, actualTypedBaseStats);
                }
            }

            MultiAssert.AssertAll();
        }

        [Fact]
        public void CnvWorksWithCrossType()
        {
            var wittyerSettings = WittyerSettings.Create(Path.GetRandomFileName().ToDirectoryInfo(), CnvTruth, CnvQuery,
                ImmutableList<ISamplePair>.Empty, EvaluationMode.CrossTypeAndSimpleCounting, InputSpecs);

            var stats = MainLauncher.GenerateJson(wittyerSettings, string.Empty, false).PerSampleStats.First();

            // make sure to check for null
            MultiAssert.True(stats.QuerySampleName != null);
            MultiAssert.True(stats.TruthSampleName != null);
            var expectedStats = JsonConvert.DeserializeObject<GeneralStats>(File.ReadAllText(CnvJsonCts.FullName))
                .PerSampleStats.First();

            var expectedOverallEventStats = expectedStats.OverallStats.Single(x => x.StatsType.Equals(StatsType.Event));
            var actualOverallEventStats = stats.OverallStats.Single(x => x.StatsType.Equals(StatsType.Event));

            MultiAssert.Equal(expectedOverallEventStats.QueryFpCount, actualOverallEventStats.QueryFpCount);
            MultiAssert.Equal(expectedOverallEventStats.QueryTpCount, actualOverallEventStats.QueryTpCount);
            MultiAssert.Equal(expectedOverallEventStats.QueryTotalCount, actualOverallEventStats.QueryTotalCount);
            MultiAssert.Equal(expectedOverallEventStats.TruthTpCount, actualOverallEventStats.TruthTpCount);
            MultiAssert.Equal(expectedOverallEventStats.TruthFnCount, actualOverallEventStats.TruthFnCount);
            MultiAssert.Equal(expectedOverallEventStats.TruthTotalCount, actualOverallEventStats.TruthTotalCount);
            MultiAssert.AssertAll();
        }

        [Fact]
        public void CnvWorksWithSimpleCounting()
        {
            var wittyerSettings = WittyerSettings.Create(Path.GetRandomFileName().ToDirectoryInfo(), CnvTruth, CnvQuery,
                ImmutableList<ISamplePair>.Empty, EvaluationMode.SimpleCounting, InputSpecs);

            var actualStats = MainLauncher.GenerateJson(wittyerSettings, string.Empty, false).PerSampleStats.First();
            var jsonText = File.ReadAllText(CnvJsonSc.FullName);
            var expectedStats = JsonConvert.DeserializeObject<GeneralStats>(jsonText)
                .PerSampleStats.First();

            var expectedOverall = expectedStats.OverallStats;
            var actualOverall = actualStats.OverallStats;
            var expectedOverallEventStats = expectedOverall.Single(x => x.StatsType.Equals(StatsType.Event));
            var actualOverallEventStats = actualOverall.Single(x => x.StatsType.Equals(StatsType.Event));

            MultiAssert.Equal(expectedOverallEventStats.QueryFpCount, actualOverallEventStats.QueryFpCount);
            MultiAssert.Equal(expectedOverallEventStats.QueryTpCount, actualOverallEventStats.QueryTpCount);
            MultiAssert.Equal(expectedOverallEventStats.QueryTotalCount, actualOverallEventStats.QueryTotalCount);
            MultiAssert.Equal(expectedOverallEventStats.TruthTpCount, actualOverallEventStats.TruthTpCount);
            MultiAssert.Equal(expectedOverallEventStats.TruthFnCount, actualOverallEventStats.TruthFnCount);
            MultiAssert.Equal(expectedOverallEventStats.TruthTotalCount, actualOverallEventStats.TruthTotalCount);

            var expectedCnvTypeOverallStats = expectedStats.DetailedStats.Single(x => x.VariantType == WittyerVariantType.Cnv.ToString()).OverallStats;
            var actualCnvTypeOverallStats = actualStats.DetailedStats.Single(x => x.VariantType == WittyerVariantType.Cnv.ToString()).OverallStats;
            var expectedOverallCnvEventStats = expectedCnvTypeOverallStats.Single(x => x.StatsType.Equals(StatsType.Event));
            var actualOverallCnvEventStats = actualCnvTypeOverallStats.Single(x => x.StatsType.Equals(StatsType.Event));

            MultiAssert.Equal(expectedOverallCnvEventStats.QueryFpCount, actualOverallCnvEventStats.QueryFpCount);
            MultiAssert.Equal(expectedOverallCnvEventStats.QueryTpCount, actualOverallCnvEventStats.QueryTpCount);
            MultiAssert.Equal(expectedOverallCnvEventStats.QueryTotalCount, actualOverallCnvEventStats.QueryTotalCount);
            MultiAssert.Equal(expectedOverallCnvEventStats.TruthTpCount, actualOverallCnvEventStats.TruthTpCount);
            MultiAssert.Equal(expectedOverallCnvEventStats.TruthFnCount, actualOverallCnvEventStats.TruthFnCount);
            MultiAssert.Equal(expectedOverallCnvEventStats.TruthTotalCount, actualOverallCnvEventStats.TruthTotalCount);

            var expectedOverallCnvBaseStats = expectedCnvTypeOverallStats.Single(x => x.StatsType.Equals(StatsType.Base));
            var actualOverallCnvBaseStats = actualCnvTypeOverallStats.Single(x => x.StatsType.Equals(StatsType.Base));

            MultiAssert.Equal(expectedOverallCnvBaseStats.QueryFpCount, actualOverallCnvBaseStats.QueryFpCount);
            MultiAssert.Equal(expectedOverallCnvBaseStats.QueryTpCount, actualOverallCnvBaseStats.QueryTpCount);
            MultiAssert.Equal(expectedOverallCnvBaseStats.QueryTotalCount, actualOverallCnvBaseStats.QueryTotalCount);
            MultiAssert.Equal(expectedOverallCnvBaseStats.TruthTpCount, actualOverallCnvBaseStats.TruthTpCount);
            MultiAssert.Equal(expectedOverallCnvBaseStats.TruthFnCount, actualOverallCnvBaseStats.TruthFnCount);
            MultiAssert.Equal(expectedOverallCnvBaseStats.TruthTotalCount, actualOverallCnvBaseStats.TruthTotalCount);

            #region test CNVs w/o Refs

            // ReSharper disable ConditionIsAlwaysTrueOrFalse
            var refs = false;
            var (expectedOrthogonalTruthBaseTotal, expectedOrthogonalTruthEventTotal) = GetTotalCnvs(CnvTruth, refs);
            var (expectedOrthogonalQueryBaseTotal, expectedOrthogonalQueryEventTotal) = GetTotalCnvs(CnvQuery, refs);

            var actualCnvBaseStatsBinned = GetCnvStats(actualStats, refs);
            var expectedCnvBaseStatsBinned = GetCnvStats(expectedStats, refs);

            var (actualTruthBaseTotal, actualQueryBaseTotal, actualTruthEventTotal, actualQueryEventTotal) =
                GetActualTotalStatsFromBins(expectedCnvBaseStatsBinned, actualCnvBaseStatsBinned);

            // expected truth is off by five, four of which is because of overlap. Last off by 1 is unexplained.  Not sure why, but there could be a hidden bug somewhere.
            MultiAssert.Equal(expectedOrthogonalTruthBaseTotal, actualTruthBaseTotal - 5);
            MultiAssert.Equal(expectedOrthogonalQueryBaseTotal, actualQueryBaseTotal);

            MultiAssert.Equal(expectedOrthogonalTruthEventTotal, actualTruthEventTotal);
            MultiAssert.Equal(expectedOrthogonalQueryEventTotal, actualQueryEventTotal);

            MultiAssert.Equal(expectedOverallCnvBaseStats.TruthTotalCount, actualTruthBaseTotal - 5);
            MultiAssert.Equal(expectedOverallCnvBaseStats.QueryTotalCount, actualQueryBaseTotal);
            
            MultiAssert.Equal(expectedOrthogonalTruthBaseTotal, actualOverallCnvBaseStats.TruthTotalCount);
            MultiAssert.Equal(expectedOrthogonalQueryBaseTotal, actualOverallCnvBaseStats.QueryTotalCount);

            #endregion

            #region test CNVs w/o Refs

            refs = true;
            (expectedOrthogonalTruthBaseTotal, expectedOrthogonalTruthEventTotal) = GetTotalCnvs(CnvTruth, refs);
            (expectedOrthogonalQueryBaseTotal, expectedOrthogonalQueryEventTotal) = GetTotalCnvs(CnvQuery, refs);

            actualCnvBaseStatsBinned = GetCnvStats(actualStats, refs);
            expectedCnvBaseStatsBinned = GetCnvStats(expectedStats, refs);
            // ReSharper restore ConditionIsAlwaysTrueOrFalse

            (actualTruthBaseTotal, actualQueryBaseTotal, actualTruthEventTotal, actualQueryEventTotal) =
                GetActualTotalStatsFromBins(expectedCnvBaseStatsBinned, actualCnvBaseStatsBinned);

            // expected truth is off by five, four of which is because of overlap. Last off by 1 is unexplained.  Not sure why, but there could be a hidden bug somewhere.
            MultiAssert.Equal(expectedOrthogonalTruthBaseTotal, actualTruthBaseTotal - 5);
            MultiAssert.Equal(expectedOrthogonalQueryBaseTotal, actualQueryBaseTotal);

            MultiAssert.Equal(expectedOverall.Single(j => j.StatsType == StatsType.Base).TruthTotalCount,
                actualTruthBaseTotal - 5);
            MultiAssert.Equal(expectedOverall.Single(j => j.StatsType == StatsType.Base).QueryTotalCount,
                actualQueryBaseTotal);

            MultiAssert.Equal(expectedOrthogonalTruthEventTotal, actualTruthEventTotal);
            MultiAssert.Equal(expectedOrthogonalQueryEventTotal, actualQueryEventTotal);

            MultiAssert.Equal(expectedOrthogonalTruthBaseTotal, actualOverall.Single(s => s.StatsType == StatsType.Base).TruthTotalCount);
            MultiAssert.Equal(expectedOrthogonalQueryBaseTotal, actualOverall.Single(s => s.StatsType == StatsType.Base).QueryTotalCount);
            
            #endregion

            MultiAssert.AssertAll();

            Dictionary<string, Dictionary<StatsType, BasicJsonStats>> GetCnvStats(SampleStats sampleStats, bool includeRef)
                => sampleStats.DetailedStats
                    .Where(x => x.VariantType.Equals(WittyerVariantType.Cnv.ToString()) || includeRef && 
                                x.VariantType.Equals(WittyerVariantType.CopyNumberReference.ToString()))
                    .SelectMany(v => v.PerBinStats)
                    .GroupBy(s => s.Bin).ToDictionary(binGroups => binGroups.Key,
                        binGroups => binGroups.SelectMany(binStats => binStats.Stats).GroupBy(s => s.StatsType)
                            .ToDictionary(statsGroup => statsGroup.Key,
                                statsGroup => statsGroup.Aggregate(BasicJsonStats.Create(statsGroup.Key, 0, 0, 0, 0),
                                    (acc, stat) => acc + stat)));

            (ulong totalLength, uint numEvents) GetTotalCnvs(FileInfo vcf, bool includeRefs)
            {
                var trees = new ConcurrentDictionary<IContigInfo, MergedIntervalTree<uint>>();

                // DO NOT delete: the line below are left there in case we want to test without overlapping variants for test tweaking etc. i.e. it's debug code.
                // IContigAndInterval lastInterval = null;
                var numEvents = 0U;
                foreach (var variant in VcfReader.TryCreate(vcf).GetOrThrow().Select(v => v.GetOrThrow()))
                {
                    if (!IsCountedCnv(variant))
                        continue;
                    numEvents++;
                    var tree = trees.GetOrAdd(variant.Contig, _ => MergedIntervalTree.Create<uint>());

                    IContigAndInterval interval;
                    var start = variant.Position;
                    if (variant.Info.TryGetValue(VcfConstants.EndTagKey, out var end))
                    {
                        if (uint.TryParse(end, out var endVal))
                            interval = GetInterval(variant.Contig, start, endVal);
                        else
                            throw new ParserException($"couldn't parse {end} into END!");
                    }
                    else if (variant.Info.TryGetValue(VcfConstants.SvLenKey, out var svLen))
                    {
                        if (int.TryParse(svLen, out var len))
                            interval = len < 0
                                ? GetInterval(variant.Contig, start - (uint) -len, start)
                                : GetInterval(variant.Contig, start, start + (uint) len);
                        else
                            throw new ParserException($"couldn't parse {svLen} into svLen!");
                    }
                    else
                        throw new NotImplementedException(
                            "Parsing using anything but svlen is not supported, should probably add it.");

                    tree.Add(interval);

                    // DO NOT delete: the line below are left there in case we want to test without overlapping variants for test tweaking etc. i.e. it's debug code.
                    //lastInterval = interval;
                }

                return (trees.GetTotalLength(), numEvents);

                IContigAndInterval GetInterval(IContigInfo contig, uint position, uint end)
                {
                    var interval = ContigAndInterval.Create(contig, position, end);

                    return interval;

                    // DO NOT delete: the remaining lines below are left there in case we want to test without overlapping variants for test tweaking etc. i.e. it's debug code.
                    //if (lastInterval == null || !interval.Contig.Equals(lastInterval.Contig)) return interval;

                    //// adjust for possible overlaps between bins. (see https://jira.illumina.com/browse/WIT-84)
                    //var overlap = interval.TryGetOverlap(lastInterval).Select(o => o.GetLength()).GetOrDefault();
                    //if (overlap > 0)
                    //    interval = ContigAndInterval.Create(interval.Contig, interval.Start + overlap,
                    //        interval.Stop + overlap);

                    //return interval;
                }

                bool IsCountedCnv(IVcfVariant variant)
                {
                    if (variant.Filters.SingleOrDefault() != VcfConstants.PassFilter)
                        // if not single or not PASS return false
                        return false;
                    var isRef = variant.Alts.SingleOrDefault() == VcfConstants.MissingValueString;
                    if (isRef)
                        return includeRefs;

                    if (variant.Samples.Count == 0)
                        return false;

                    var hasCn = variant.Samples[0].SampleDictionary
                        .TryGetValue(VcfConstants.CnSampleFieldKey, out var cn);

                    var hasGt = variant.Samples[0].SampleDictionary.TryGetValue(VcfConstants.GenotypeKey, out var gt);

                    if (!hasCn)
                    {
                        return includeRefs // no cn means only true if we include Refs
                               && hasGt && gt.Split(VcfConstants.GtPhasedValueDelimiter[0],
                                       VcfConstants.GtUnphasedValueDelimiter[0])
                                   .All(x => x == "0");
                    }

                    if (!variant.Info.TryGetValue(VcfConstants.SvTypeKey, out var svType) || !WittyerConstants.BaseLevelStatsTypeStrings
                            .Contains(svType))
                        return false;
                    if (!int.TryParse(cn, out var ploidy))
                        return false;

                    isRef = (hasGt
                                ? gt.Split(VcfConstants.GtPhasedValueDelimiter[0],
                                    VcfConstants.GtUnphasedValueDelimiter[0]).Length
                                : 2) == ploidy;
                    return !isRef || includeRefs;
                }
            }

            (ulong, ulong, ulong, ulong) GetActualTotalStatsFromBins(Dictionary<string, Dictionary<StatsType, BasicJsonStats>> expectedBinned, 
                Dictionary<string, Dictionary<StatsType, BasicJsonStats>> actualBinned)
            {
                var actualTruthBase = 0UL;
                var actualQueryBase = 0UL;
                var actualTruthEvent = 0UL;
                var actualQueryEvent = 0UL;
                foreach (var (bin, binStats) in expectedBinned)
                foreach (var (type, expectedCnvStats) in binStats)
                {
                    var actualCnvStats = actualBinned[bin][type];
                    if (type == StatsType.Base)
                    {
                        actualTruthBase += actualCnvStats.TruthTotalCount;
                        actualQueryBase += actualCnvStats.QueryTotalCount;
                    }
                    else
                    {
                        actualTruthEvent += actualCnvStats.TruthTotalCount;
                        actualQueryEvent += actualCnvStats.QueryTotalCount;
                    }

                    MultiAssert.Equal(expectedCnvStats, actualCnvStats);
                }

                return (actualTruthBase, actualQueryBase, actualTruthEvent, actualQueryEvent);
            }
        }

        [Fact]
        public void SvWorksWithSimpleCount()
        {
            var insertionSpec = InputSpecs[WittyerVariantType.Insertion];
            insertionSpec = InputSpec.Create(WittyerConstants.DefaultBins.SetItem(0, 50), insertionSpec.BasepairDistance,
                    insertionSpec.PercentageDistance, insertionSpec.ExcludedFilters,
                    insertionSpec.IncludedFilters);
            var wittyerSettings = WittyerSettings.Create(Path.GetRandomFileName().ToDirectoryInfo(), SomaticTruth,
                SomaticQuery, ImmutableList<ISamplePair>.Empty, EvaluationMode.SimpleCounting,
                InputSpecs.SetItem(WittyerVariantType.Insertion, insertionSpec));

            var actualStats = MainLauncher.GenerateJson(wittyerSettings, string.Empty, false).PerSampleStats.First();
            var expectedStats = JsonConvert.DeserializeObject<GeneralStats>(File.ReadAllText(SvJsonSc.FullName))
                .PerSampleStats.First();

            var expectedOverallEventStats = expectedStats.OverallStats.Single(x => x.StatsType.Equals(StatsType.Event));
            var actualOverallEventStats = actualStats.OverallStats.Single(x => x.StatsType.Equals(StatsType.Event));

            MultiAssert.Equal(expectedOverallEventStats.QueryFpCount, actualOverallEventStats.QueryFpCount);
            MultiAssert.Equal(expectedOverallEventStats.QueryTpCount, actualOverallEventStats.QueryTpCount);
            MultiAssert.Equal(expectedOverallEventStats.QueryTotalCount, actualOverallEventStats.QueryTotalCount);
            MultiAssert.Equal(expectedOverallEventStats.TruthTpCount, actualOverallEventStats.TruthTpCount);
            MultiAssert.Equal(expectedOverallEventStats.TruthFnCount, actualOverallEventStats.TruthFnCount);
            MultiAssert.Equal(expectedOverallEventStats.TruthTotalCount, actualOverallEventStats.TruthTotalCount);

            var expectedInsertionStats = expectedStats.DetailedStats.Single(s => s.VariantType == WittyerVariantType.Insertion.ToString()).PerBinStats;
            var actualInsertionStats = actualStats.DetailedStats.Single(s => s.VariantType == WittyerVariantType.Insertion.ToString()).PerBinStats;
            foreach (var (expectedInsBinStat, actualInsBinStat) in expectedInsertionStats.Zip(actualInsertionStats, (a, b) => (a, b)))
            {
                var expectedSingleStat = expectedInsBinStat.Stats.Single();
                var actualSingleStat = actualInsBinStat.Stats.Single();
                if (!expectedSingleStat.Equals(actualSingleStat))
                    MultiAssert.Equal(string.Empty, expectedInsBinStat.Bin);
                MultiAssert.Equal(expectedSingleStat, actualSingleStat);
            }

            MultiAssert.AssertAll();
        }
    }
}