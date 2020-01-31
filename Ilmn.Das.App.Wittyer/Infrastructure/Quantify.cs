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
using Ilmn.Das.Std.AppUtils.Collections;
using Ilmn.Das.Std.BioinformaticUtils.Contigs;
using Ilmn.Das.Std.VariantUtils.SimpleVariants;
using JetBrains.Annotations;

namespace Ilmn.Das.App.Wittyer.Infrastructure
{
    public static class Quantify
    {
        private static readonly IReadOnlyList<MatchEnum> GenotypeMatchTypes
            = new[] {MatchEnum.AlleleAndGenotypeMatch, MatchEnum.LocalAndGenotypeMatch};

        private static readonly ImmutableList<uint> EmptyBins = ImmutableList.Create(WittyerConstants.StartingBin);

        [NotNull]
        public static SampleMetrics GenerateSampleStats([NotNull] WittyerVcfResult truth, [NotNull] WittyerVcfResult query, bool isGenotypeEvaluated,
            [NotNull] IReadOnlyDictionary<WittyerVariantType, InputSpec> inputSpecs)
        {
            var perTypeBinnedDictionary =
                inputSpecs.ToDictionary(kvp => kvp.Key,
                    kvp => new BinnedDictionary(
                        EmptyBins.AddRange(kvp.Value.Bins).Distinct().OrderBy(b => b).ToReadOnlyList(), kvp.Key));

            var (overallQueryBaseStats, perTypeQueryOverall) =
                GenerateStats(perTypeBinnedDictionary, query, isGenotypeEvaluated, WitDecision.FalsePositive,
                    stats => stats.QueryStats, stats => stats.QueryBaseStats);
            var (overallTruthBaseStats, perTypeTruthOverall) =
                GenerateStats(perTypeBinnedDictionary, truth, isGenotypeEvaluated, WitDecision.FalseNegative,
                    stats => stats.TruthStats, stats => stats.TruthBaseStats);

            uint eventTruthTp = 0, eventTruthFp = 0, eventQueryTp = 0, eventQueryFp = 0;
            var benchMarkMetrics = ImmutableDictionary<WittyerVariantType, IBenchmarkMetrics<WittyerVariantType>>.Empty.ToBuilder();
            foreach (var (type, binnedDict) in perTypeBinnedDictionary)
            {
                var metrics = BenchmarkMetrics<WittyerVariantType>.Create(type,
                    perTypeTruthOverall.TryGetValue(type, out var truthStats)
                        ? ImmutableDictionary<StatsType, IStatsUnit>.Empty.Add(StatsType.Base,
                            StatsUnit.Create(truthStats, perTypeQueryOverall[type]))
                        : ImmutableDictionary<StatsType, IStatsUnit>.Empty, binnedDict);
                benchMarkMetrics.Add(type, metrics);
                var overallStat = metrics.OverallStats[StatsType.Event];
                eventTruthTp += overallStat.TruthStats.TrueCount;
                eventTruthFp += overallStat.TruthStats.FalseCount;
                eventQueryTp += overallStat.QueryStats.TrueCount;
                eventQueryFp += overallStat.QueryStats.FalseCount;
            }

            return SampleMetrics.Create(SamplePair.Create(truth.SampleName, query.SampleName),
                ImmutableDictionary<StatsType, IStatsUnit>.Empty.Add(StatsType.Event,
                    StatsUnit.Create(BasicStatsCount.Create(eventTruthTp, eventTruthFp),
                        BasicStatsCount.Create(eventQueryTp, eventQueryFp))).Add(StatsType.Base,
                    StatsUnit.Create(overallTruthBaseStats,
                        overallQueryBaseStats)),
                benchMarkMetrics.ToImmutable());
        }

        private static (IBasicStatsCount overallBaseStats,
            IReadOnlyDictionary<WittyerVariantType, IBasicStatsCount> perTypeOverallBaseStats)
            GenerateStats(
                [NotNull] IDictionary<WittyerVariantType, BinnedDictionary> perTypeBinnedDictionary,
                [NotNull] WittyerVcfResult result, bool isGenotypeEvaluated, WitDecision falseDecision,
                [NotNull] Func<IMutableStats, IMutableEventStatsCount> eventsStatsSelector,
                [NotNull] Func<MutableEventAndBasesStats, IMutableBaseStatsCount> baseStatsSelector)
        {
            // tracks the summary OverallStats total base stats
            var grandTotalDictionary = new ConcurrentDictionary<IContigInfo, MergedIntervalTree<uint>>();
            var grandTotalTpDictionary = new ConcurrentDictionary<IContigInfo, MergedIntervalTree<uint>>();

            // tracks the per type OverallStats for bases
            var perTypeTotalDictionary = new ConcurrentDictionary<WittyerVariantType,
                ConcurrentDictionary<IContigInfo, MergedIntervalTree<uint>>>();
            var perTypeTotalTpDictionary = new ConcurrentDictionary<WittyerVariantType,
                ConcurrentDictionary<IContigInfo, MergedIntervalTree<uint>>>();

            // tracks the Per Type Per bin base stats
            var perTypePerBinTotalDictionary =
                new ConcurrentDictionary<WittyerVariantType, ConcurrentDictionary<uint,
                    ConcurrentDictionary<IContigInfo, MergedIntervalTree<uint>>>>();
            var perTypePerBinTpDictionary =
                new ConcurrentDictionary<WittyerVariantType, ConcurrentDictionary<uint,
                    ConcurrentDictionary<IContigInfo, MergedIntervalTree<uint>>>>();

            foreach (var typeGroup in result.NormalVariants.AsEnumerable<IWittyerSimpleVariant>()
                .Concat(result.BreakendPairs).GroupBy(v => v.VariantType))
            {
                var statsBinnedDictionary = perTypeBinnedDictionary[typeGroup.Key];
                var perBinTotalDictionary = perTypePerBinTotalDictionary.GetOrAdd(typeGroup.Key,
                    _ => new ConcurrentDictionary<uint, ConcurrentDictionary<IContigInfo, MergedIntervalTree<uint>>>());
                var perBinTpDictionary = perTypePerBinTpDictionary.GetOrAdd(typeGroup.Key,
                    _ => new ConcurrentDictionary<uint, ConcurrentDictionary<IContigInfo, MergedIntervalTree<uint>>>());
                var typeTotalTrees = GetOrAddGenomeTree(in perTypeTotalDictionary, in typeGroup);
                var typeTotalTpTrees = GetOrAddGenomeTree(in perTypeTotalTpDictionary, typeGroup);
                var shouldCalculateBaseStats = WittyerConstants.BaseLevelStatsTypes.Contains(typeGroup.Key);

                foreach (var binGroup in typeGroup.GroupBy(v => v.Win.Start))
                {
                    // choosing the "GenomeIntervalTree" to use for this bin
                    var mutableStats = statsBinnedDictionary[binGroup.Key];
                    var eventStats = eventsStatsSelector(mutableStats);
                    var perTypePerBinTotalTrees = GetOrAddGenomeTree(in perBinTotalDictionary, in binGroup);
                    var perTypePerBinTpTrees = GetOrAddGenomeTree(in perBinTpDictionary, in binGroup);

                    foreach (var variant in binGroup)
                    {
                        //Event level stats
                        if (variant.Sample.Wit == WitDecision.TruePositive)
                            eventStats.AddTrueEvent();
                        else if (variant.Sample.Wit == falseDecision)
                            eventStats.AddFalseEvent();
                        else
                            throw new InvalidDataException(
                                $"Something else is going into the variant {variant.OriginalVariant.ToShortString()} as WIT {variant.Sample.Wit}");

                        if (!shouldCalculateBaseStats) continue;
                        
                        //Base level stats
                        if (mutableStats is MutableEventAndBasesStats mbt)
                        {
                            GetOrAddTree(in perTypePerBinTotalTrees, in variant).Add(variant);
                            GetOrAddTree(in typeTotalTrees, in variant).Add(variant);
                            GetOrAddTree(in grandTotalDictionary, in variant).Add(variant);

                            var baseStats = baseStatsSelector(mbt);

                            foreach (var annotation in variant.OverlapInfo)
                            {
                                if (annotation.Wow == null)
                                {
                                    // sanitation check
                                    if (annotation.What != MatchEnum.Unmatched)
                                        throw new InvalidDataException(
                                            $"Somehow, {nameof(annotation.Wow)} annotation was {annotation.Wow} " +
                                            $"when {nameof(annotation.What)} was {annotation.What}!");

                                    continue;
                                }

                                if (GenotypeMatchTypes.Contains(annotation.What) // was it lgm or agm?
                                    || !isGenotypeEvaluated) // if not genotype match, is it simple counting?
                                {
                                    baseStats.AddTrueCount(variant.Contig, annotation.Wow);
                                    GetOrAddTree(in perTypePerBinTpTrees, in variant).Add(annotation.Wow);
                                    GetOrAddTree(in typeTotalTpTrees, in variant).Add(annotation.Wow);
                                    GetOrAddTree(in grandTotalTpDictionary, in variant).Add(annotation.Wow);
                                }
                            }
                        }
                        else
                            throw new InvalidDataException(
                                $"Not sure why {variant.OriginalVariant} with {variant.VariantType} cannot generate base level stats. Check with witty.er developer! (or debug yourself...?)");
                    }

                    if (!shouldCalculateBaseStats) continue;

                    IMutableBaseStatsCount stats = null;
                    foreach (var chr in perTypePerBinTotalTrees.Keys)
                    {
                        var tree = perTypePerBinTotalTrees[chr];
                        if (stats == null)
                            stats = baseStatsSelector((MutableEventAndBasesStats) mutableStats);
                        if (perTypePerBinTpTrees.TryGetValue(chr, out var tpTree))
                        {
                            foreach (var interval in tree)
                            {
                                var leftovers = tpTree.Search(interval).ToList();
                                if (leftovers.Count == 0) // no overlaps
                                    stats.AddFalseCount(chr, interval);
                                else
                                    foreach (var leftover in interval.Subtract(leftovers))
                                        stats.AddFalseCount(chr, leftover);
                            }
                        } // chromosome not found in tp trees at all, so add all false counts.
                        else
                            foreach (var interval in tree)
                                stats.AddFalseCount(chr, interval);

                    }

                    // This is here as sanity check code, we can remove later if we want.

                    //if (stats == null) // means TotalTree has no intervals
                    //    return;

                    //// eventually get rid of this by replacing with actually keeping track of totals in the stats
                    //// and after outputting stats, we should do sanity check and crash if not equal.
                    //var fpTotal = stats.FalseCount.Select(kvp => kvp.Value.GetTotalMergedLength()).Sum();
                    //var tpTotal = stats.TrueCount.Select(kvp => kvp.Value.GetTotalMergedLength()).Sum();
                    //var expectedTotal = totalTrees.Select(kvp => kvp.Value.GetTotalMergedLength()).Sum();
                    //var actualTotal = fpTotal + tpTotal;

                    //if (actualTotal != expectedTotal)
                    //    throw new InvalidDataException(
                    //        $"Expected total bases to be {expectedTotal}, but got {actualTotal}!");
                }
            }

            var grandTotalTpCount = grandTotalTpDictionary.GetTotalLength();

            var typedOverBases = WittyerConstants.BaseLevelStatsTypes.ToDictionary(type => type, type =>
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

            MergedIntervalTree<uint> GetOrAddTree(in ConcurrentDictionary<IContigInfo, MergedIntervalTree<uint>> dict,
                in IWittyerSimpleVariant variant)
                => dict.GetOrAdd(variant.Contig, _ => MergedIntervalTree.Create<uint>());

            ConcurrentDictionary<IContigInfo, MergedIntervalTree<uint>> GetOrAddGenomeTree<T>(
                in ConcurrentDictionary<T, ConcurrentDictionary<IContigInfo, MergedIntervalTree<uint>>>
                    perBinTpDictionary, in IGrouping<T, IWittyerSimpleVariant> binGroup)
                => perBinTpDictionary.GetOrAdd(binGroup.Key,
                    _ => new ConcurrentDictionary<IContigInfo, MergedIntervalTree<uint>>());
        }
    }
}