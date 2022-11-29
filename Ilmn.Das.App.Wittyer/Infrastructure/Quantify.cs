using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using Ilmn.Das.App.Wittyer.Input;
using Ilmn.Das.App.Wittyer.Results;
using Ilmn.Das.App.Wittyer.Stats;
using Ilmn.Das.App.Wittyer.Stats.Counts;
using Ilmn.Das.App.Wittyer.Utilities;
using Ilmn.Das.App.Wittyer.Utilities.Enums;
using Ilmn.Das.App.Wittyer.Vcf.Variants;
using Ilmn.Das.Core.Tries.Extensions;
using Ilmn.Das.Std.AppUtils.Collections;
using Ilmn.Das.Std.AppUtils.Comparers;
using Ilmn.Das.Std.AppUtils.Intervals;
using Ilmn.Das.Std.AppUtils.Misc;
using Ilmn.Das.Std.BioinformaticUtils.Contigs;
using Ilmn.Das.Std.BioinformaticUtils.GenomicFeatures;
using Ilmn.Das.Std.VariantUtils.SimpleVariants;

namespace Ilmn.Das.App.Wittyer.Infrastructure
{
    internal static class Quantify
    {
        private static readonly IReadOnlyList<MatchEnum> GenotypeMatchTypes
            = new[] {MatchEnum.AlleleAndGenotypeMatch, MatchEnum.LocalAndGenotypeMatch};

        private static readonly ImmutableList<(uint size, bool skip)> EmptyBins =
            ImmutableList.Create((WittyerConstants.StartingBin, false));

        /// <summary>
        /// Generates the sample stats given the results and settings.
        /// </summary>
        /// <param name="truth">The truth.</param>
        /// <param name="query">The query.</param>
        /// <param name="isGenotypeEvaluated">if set to <c>true</c> [is genotype evaluated].</param>
        /// <param name="inputSpecs">The input specs.</param>
        /// <returns></returns>
        public static SampleMetrics GenerateSampleStats(IWittyerResult truth,
            IWittyerResult query, bool isGenotypeEvaluated,
            IReadOnlyDictionary<WittyerType, InputSpec> inputSpecs)
        {
            var perTypeBinnedDictionary =
                inputSpecs.ToDictionary(kvp => kvp.Key,
                    kvp => new BinnedDictionary(GetBins(kvp.Value.BinSizes), kvp.Key));

            var (overallQueryBaseStats, perTypeQueryOverall) =
                GenerateStats(inputSpecs, perTypeBinnedDictionary, query, isGenotypeEvaluated,
                    WitDecision.FalsePositive,
                    stats => stats.QueryStats, stats => stats.QueryBaseStats);
            var (overallTruthBaseStats, perTypeTruthOverall) =
                GenerateStats(inputSpecs, perTypeBinnedDictionary, truth, isGenotypeEvaluated,
                    WitDecision.FalseNegative,
                    stats => stats.TruthStats, stats => stats.TruthBaseStats);

            uint eventTruthTp = 0, eventTruthFp = 0, eventQueryTp = 0, eventQueryFp = 0;
            var benchMarkMetrics = ImmutableDictionary<WittyerType, IBenchmarkMetrics<WittyerType>>.Empty.ToBuilder();
            foreach (var (type, binnedDict) in perTypeBinnedDictionary)
            {
                var overallBaseStats = ImmutableDictionary<StatsType, IStatsUnit>.Empty;
                if (perTypeTruthOverall.TryGetValue(type, out var truthStats))
                    overallBaseStats = overallBaseStats.Add(StatsType.Base,
                        StatsUnit.Create(truthStats, perTypeQueryOverall[type]));
                var metrics = BenchmarkMetrics<WittyerType>.Create(type, overallBaseStats, binnedDict);
                benchMarkMetrics.Add(type, metrics);

                var overallStat = metrics.OverallStats[StatsType.Event];
                eventTruthTp += overallStat.TruthStats.TrueCount;
                eventTruthFp += overallStat.TruthStats.FalseCount;
                eventQueryTp += overallStat.QueryStats.TrueCount;
                eventQueryFp += overallStat.QueryStats.FalseCount;
            }

            var overallTruthEventStats = BasicStatsCount.Create(eventTruthTp, eventTruthFp);
            var overallQueryEventStats = BasicStatsCount.Create(eventQueryTp, eventQueryFp);
            var overallEventStatsUnit = StatsUnit.Create(overallTruthEventStats, overallQueryEventStats);

            var overallBaseStatsUnit = StatsUnit.Create(overallTruthBaseStats, overallQueryBaseStats);

            var overallStats = ImmutableDictionary<StatsType, IStatsUnit>.Empty
                .Add(StatsType.Event, overallEventStatsUnit)
                .Add(StatsType.Base, overallBaseStatsUnit);

            return SampleMetrics.Create(SamplePair.Create(truth.SampleName, query.SampleName), overallStats,
                benchMarkMetrics.ToImmutable());

            IReadOnlyList<(uint size, bool skip)> GetBins(IImmutableList<(uint size, bool skip)> binSizes)
            {
                if (binSizes.Count == 0) return EmptyBins;

                var sorted =
                    (binSizes as ImmutableList<(uint size, bool skip)>)?.Sort(
                        new CustomComparer<(uint size, bool skip)>((t1, t2) => t1.size.CompareTo(t2.size))) ??
                    binSizes.OrderBy(it => it.size).ToImmutableList();
                if (sorted[0].size != 0 && sorted[0].size != 1)
                    sorted = sorted.Insert(0, (WittyerConstants.StartingBin, false));

                return sorted;
            }
        }

        private static (IBasicStatsCount overallBaseStats,
            IReadOnlyDictionary<WittyerType, IBasicStatsCount> perTypeOverallBaseStats)
            GenerateStats(IReadOnlyDictionary<WittyerType, InputSpec> inputSpecs,
                IDictionary<WittyerType, BinnedDictionary> perTypeBinnedDictionary,
                IWittyerResult result, bool isGenotypeEvaluated, WitDecision falseDecision,
                Func<IMutableStats, IMutableEventStatsCount> eventsStatsSelector,
                Func<MutableEventAndBasesStats, IMutableBaseStatsCount> baseStatsSelector)
        {
            // tracks the summary OverallStats total base stats
            var grandTotalDictionary = new ConcurrentDictionary<IContigInfo, List<IInterval<uint>>>();
            var grandTotalTpDictionary = new ConcurrentDictionary<IContigInfo, List<IInterval<uint>>>();

            // tracks the per type OverallStats for bases
            var perTypeTotalDictionary = new ConcurrentDictionary<WittyerType,
                ConcurrentDictionary<IContigInfo, List<IInterval<uint>>>>();
            var perTypeTotalTpDictionary = new ConcurrentDictionary<WittyerType,
                ConcurrentDictionary<IContigInfo, List<IInterval<uint>>>>();

            // tracks the Per Type Per bin base stats
            var perTypePerBinTotalDictionary =
                new ConcurrentDictionary<WittyerType, ConcurrentDictionary<uint,
                    ConcurrentDictionary<IContigInfo, List<IInterval<uint>>>>>();
            var perTypePerBinTpDictionary =
                new ConcurrentDictionary<WittyerType, ConcurrentDictionary<uint,
                    ConcurrentDictionary<IContigInfo, List<IInterval<uint>>>>>();

            foreach (var (type, variants) in result.Variants
                .Select(kvp => (kvp.Key, kvp.Value.AsEnumerable<IWittyerSimpleVariant>()))
                .Concat(result.BreakendPairsAndInsertions
                    .Select(kvp => (kvp.Key, kvp.Value.AsEnumerable<IWittyerSimpleVariant>()))))
            {
                var bedRegion = inputSpecs[type].IncludedRegions?.IntervalTree;
                var statsBinnedDictionary = perTypeBinnedDictionary[type];
                var perBinTotalDictionary = perTypePerBinTotalDictionary.GetOrAdd(type,
                    _ => new ConcurrentDictionary<uint, ConcurrentDictionary<IContigInfo, List<IInterval<uint>>>>());
                var perBinTpDictionary = perTypePerBinTpDictionary.GetOrAdd(type,
                    _ => new ConcurrentDictionary<uint, ConcurrentDictionary<IContigInfo, List<IInterval<uint>>>>());
                var typeTotalTrees = perTypeTotalDictionary.GetOrAdd(type,
                    _ => new ConcurrentDictionary<IContigInfo, List<IInterval<uint>>>());
                var typeTotalTpTrees = perTypeTotalTpDictionary.GetOrAdd(type,
                    _ => new ConcurrentDictionary<IContigInfo, List<IInterval<uint>>>());

                foreach (var binGroup in variants
                    .GroupBy(v => v.Win.Start))
                {
                    var binStart = binGroup.Key;

                    // choosing the "GenomeIntervalTree" to use for this bin
                    var mutableStats = statsBinnedDictionary[binStart];
                    // If the bin of this bin group was marked to be skipped, don't calculate stats for it.
                    if (statsBinnedDictionary.Bins.First(sizeSkipTuple => sizeSkipTuple.size == binStart).skip)
                        continue;

                    var eventStats = eventsStatsSelector(mutableStats);
                    var perTypePerBinTotalTrees = GetOrAddGenomeTree(in perBinTotalDictionary, in binGroup);
                    var perTypePerBinTpTrees = GetOrAddGenomeTree(in perBinTpDictionary, in binGroup);

                    foreach (var variant in binGroup)
                    {
                        //Event level stats
                        var assessed = true;
                        if (variant.Sample.Wit == WitDecision.TruePositive)
                            eventStats.AddTrueEvent();
                        else if (variant.Sample.Wit == falseDecision)
                            eventStats.AddFalseEvent();
                        else if (variant.Sample.Wit == WitDecision.NotAssessed)
                            assessed = false;
                        else
                            throw new InvalidDataException(
                                $"Unexpected {nameof(WitDecision)} value ({variant.Sample.Wit}) for variant: "
                                + variant.OriginalVariant.ToShortString());

                        if (!type.HasBaseLevelStats) continue;

                        //Base level stats
                        if (mutableStats is MutableEventAndBasesStats mbt)
                        {
                            IIntervalTree<uint, IContigAndInterval> bedTree = null;
                            // if there's actually a bed region, but the bed region lacks this contig, we should skip this part
                            if (bedRegion != null && !bedRegion.TryGetValue(variant.Contig, out bedTree))
                                continue;
                            var foundOverlap = true;
                            if (assessed)
                            {
                                foundOverlap = false;
                                foreach (var overlapped in bedTree?.Search(variant) ??
                                                           new IContigAndInterval[] { variant })
                                {
                                    var actualOverlap = ReferenceEquals(variant, overlapped)
                                        ? variant
                                        : overlapped.TryGetOverlap(variant).GetOrThrow();
                                    GetOrAddTree(in perTypePerBinTotalTrees, in variant).Add(actualOverlap);
                                    GetOrAddTree(in typeTotalTrees, in variant).Add(actualOverlap);
                                    GetOrAddTree(in grandTotalDictionary, in variant).Add(actualOverlap);
                                    foundOverlap = true;
                                }
                            }

                            if (!foundOverlap)
                                continue;

                            var baseStats = baseStatsSelector(mbt);

                            foreach (var annotation in variant.OverlapInfo)
                            {
                                var wow = annotation.Wow;
                                if (wow == null)
                                    continue;

                                if (!GenotypeMatchTypes.Contains(annotation.What) // was it lgm or agm?
                                    && isGenotypeEvaluated) // if not genotype match, is it simple counting?
                                    continue;

                                // search the bedtree for all the overlapping bed regions,
                                //   then extract the actual overlapping coordinates (exclude non-overlapping)
                                var overlaps = bedTree?.Search(wow)
                                                   .Select(overlap => overlap.TryGetOverlap(wow).GetOrThrow()) ??
                                               wow.FollowedBy();
                                foreach (var overlap in overlaps)
                                {
                                    baseStats.AddTrueCount(variant.Contig, overlap);
                                    GetOrAddTree(in perTypePerBinTpTrees, in variant).Add(overlap);
                                    GetOrAddTree(in typeTotalTpTrees, in variant).Add(overlap);
                                    GetOrAddTree(in grandTotalTpDictionary, in variant).Add(overlap);
                                    
                                    if (assessed) continue;
                                    // for not assessed variants, we haven't counted their bases to total trees yet
                                    GetOrAddTree(in perTypePerBinTotalTrees, in variant).Add(overlap);
                                    GetOrAddTree(in typeTotalTrees, in variant).Add(overlap);
                                    GetOrAddTree(in grandTotalDictionary, in variant).Add(overlap);
                                }
                            }
                        }
                        else
                            throw new InvalidDataException(
                                $"Not sure why {variant.OriginalVariant} with {variant.VariantType} cannot generate base level stats. Check with witty.er developer! (or debug yourself...?)");
                    }

                    if (!type.HasBaseLevelStats) continue;

                    IMutableBaseStatsCount stats = null;
                    foreach (var chr in perTypePerBinTotalTrees.Keys)
                    {
                        var totalTree = perTypePerBinTotalTrees[chr];
                        if (stats == null)
                            stats = baseStatsSelector((MutableEventAndBasesStats) mutableStats);
                        if (perTypePerBinTpTrees.TryGetValue(chr, out var tpTreeUnmerged))
                        {
                            var tpTree = tpTreeUnmerged.ToMergedIntervalTree();
                            foreach (var wholeInterval in totalTree)
                            {
                                var overlaps = tpTree.Search(wholeInterval).ToList();
                                if (overlaps.Count == 0) // no overlaps
                                    stats.AddFalseCount(chr, wholeInterval);
                                else
                                    foreach (var leftover in wholeInterval.Subtract(overlaps))
                                        stats.AddFalseCount(chr, leftover);
                            }
                        } // chromosome not found in tp trees at all, so add all false counts.
                        else
                            foreach (var interval in totalTree)
                                stats.AddFalseCount(chr, interval);
                    }
                }
            }

            var grandTotalTpCount = grandTotalTpDictionary.GetTotalLength();

            var typedOverBases = WittyerType.AllTypes.Where(type => type.HasBaseLevelStats).ToDictionary(type => type,
                type =>
                {
                    var tpCount = perTypeTotalTpDictionary.TryGetValue(type, out var tree) ? tree.GetTotalLength() : 0U;
                    var totalCount = perTypeTotalDictionary.TryGetValue(type, out tree)
                        ? tree.GetTotalLength()
                        : 0;
                    if (tpCount > totalCount)
                        throw new InvalidDataException(
                            $"Somehow, got {nameof(tpCount)} ({tpCount}) to be greater than {nameof(totalCount)} ({totalCount})");
                    var falseCount = totalCount - tpCount;
                    return BasicStatsCount.Create(tpCount, falseCount);
                });

            return (BasicStatsCount.Create(
                    grandTotalTpCount, grandTotalDictionary.GetTotalLength() - grandTotalTpCount),
                // must filter out keys that don't have base level stats for better cleanliness in case we want to not output Json stats etc for these
                typedOverBases);

            List<IInterval<uint>> GetOrAddTree(in ConcurrentDictionary<IContigInfo, List<IInterval<uint>>> dict,
                in IWittyerSimpleVariant variant)
                => dict.GetOrAdd(variant.Contig, _ => new List<IInterval<uint>>());

            ConcurrentDictionary<IContigInfo, List<IInterval<uint>>> GetOrAddGenomeTree<T>(
                in ConcurrentDictionary<T, ConcurrentDictionary<IContigInfo, List<IInterval<uint>>>>
                    perBinTpDictionary, in IGrouping<T, IWittyerSimpleVariant> binGroup)
                => perBinTpDictionary.GetOrAdd(binGroup.Key,
                    _ => new ConcurrentDictionary<IContigInfo, List<IInterval<uint>>>());
        }
    }
}
