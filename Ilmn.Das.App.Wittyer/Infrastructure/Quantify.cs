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
using Ilmn.Das.App.Wittyer.Vcf.Samples;
using Ilmn.Das.App.Wittyer.Vcf.Variants;
using Ilmn.Das.App.Wittyer.Vcf.Variants.Annotations;
using Ilmn.Das.Core.Tries.Extensions;
using Ilmn.Das.Std.AppUtils.Collections;
using Ilmn.Das.Std.AppUtils.Enums;
using Ilmn.Das.Std.AppUtils.Intervals;
using Ilmn.Das.Std.AppUtils.Misc;
using Ilmn.Das.Std.BioinformaticUtils.Contigs;
using Ilmn.Das.Std.BioinformaticUtils.GenomicFeatures;
using Ilmn.Das.Std.VariantUtils.Vcf;

namespace Ilmn.Das.App.Wittyer.Infrastructure
{
    internal static class Quantify
    {
        private static readonly ImmutableList<(uint size, bool skip)> EmptyBins =
            ImmutableList.Create((WittyerConstants.StartingBin, false));

        internal static readonly Category DelAndCnTrLossCategory =
            Category.Create(WittyerType.Deletion, WittyerType.CopyNumberTandemRepeat);

        internal static Category DelAndCnLossCategory =
            Category.Create(WittyerType.Deletion, WittyerType.CopyNumberLoss);

        internal static Category DupAndCnGainCategory =
            Category.Create(WittyerType.Duplication, WittyerType.CopyNumberGain);

        internal static readonly Category InsAndCnTrGainCategory =
            Category.Create(WittyerType.Insertion, WittyerType.CopyNumberTandemRepeat);
        
        internal static readonly Category RefAndCnTrefCategory = 
            Category.Create(WittyerType.CopyNumberReference, WittyerType.CopyNumberTandemReference);

        internal static readonly IImmutableDictionary<WittyerType, IReadOnlyCollection<Category>> CrossTypeCategories =
            ImmutableDictionary<WittyerType, IReadOnlyCollection<Category>>.Empty
                .Add(WittyerType.Duplication, new[]
                {
                    // TODO: If we add DUPs to CN:TR crosstype, make sure to add here.
                    DupAndCnGainCategory,
                })
                .Add(WittyerType.Deletion, new[]
                {
                    DelAndCnLossCategory,
                    DelAndCnTrLossCategory,
                })
                .Add(WittyerType.Insertion, new[] { InsAndCnTrGainCategory })
                .Add(WittyerType.CopyNumberGain, new[]
                {
                    // TODO: If we add DUPs to CN:TR crosstype, make sure to add here.
                    DupAndCnGainCategory,
                })
                .Add(WittyerType.CopyNumberLoss, new[]
                {
                    DelAndCnLossCategory,
                })
                .Add(WittyerType.CopyNumberReference, new[]
                {
                    RefAndCnTrefCategory,
                })
                .Add(WittyerType.CopyNumberTandemRepeat,
                    new[]
                    {
                        InsAndCnTrGainCategory,
                        DelAndCnTrLossCategory,
                        RefAndCnTrefCategory,
                    });

        /// <summary>
        /// Generates the sample stats given the results and settings.
        /// </summary>
        /// <param name="truth">The truth.</param>
        /// <param name="query">The query.</param>
        /// <param name="inputSpecs">The input specs.</param>
        /// <param name="isCrossType"></param>
        /// <returns></returns>
        public static Dictionary<(MatchEnum what, bool isGenotypeEvaluated), SampleMetrics> GenerateSampleStats(
            IWittyerResult truth, IWittyerResult query, IReadOnlyDictionary<WittyerType, InputSpec> inputSpecs,
            bool isCrossType)
        {
            var matchEnums = EnumUtils.GetValues<MatchEnum>().Remove(MatchEnum.Unmatched).Remove(MatchEnum.Genotype);

            var perTypeBinnedDictionary =
                matchEnums.SelectMany(it => new[] { true, false }.Select(boo => (it, boo))).ToDictionary(it => it,
                    _ => CreatePerTypeBinnedDictionary(inputSpecs, isCrossType));

            var queryStatsPerMatchEnum =
                GenerateStats(inputSpecs, perTypeBinnedDictionary, query,
                    stats => stats.QueryStats, stats => stats.QueryBaseStats, isCrossType);
            var truthStatsPerMatchEnum =
                GenerateStats(inputSpecs, perTypeBinnedDictionary, truth,
                    stats => stats.TruthStats, stats => stats.TruthBaseStats, isCrossType);
            var ret = new Dictionary<(MatchEnum what, bool isGenotypeEvaluated), SampleMetrics>();
            foreach (var (what, pTypeBinnedDictionary) in perTypeBinnedDictionary)
            {
                uint eventTruthTp = 0, eventTruthFp = 0, eventQueryTp = 0, eventQueryFp = 0;
                var benchMarkMetrics = ImmutableDictionary<PrimaryCategory, IBenchmarkMetrics<PrimaryCategory>>.Empty.ToBuilder();
                var (overallQueryBaseStats, perTypeQueryOverallBaseStats) = queryStatsPerMatchEnum[what];
                var (overallTruthBaseStats, perTypeTruthOverallBaseStats) = truthStatsPerMatchEnum[what];
                foreach (var (type, binnedDict) in pTypeBinnedDictionary)
                {
                    var overallBaseStats = ImmutableDictionary<StatsType, IStatsUnit>.Empty;
                    if (perTypeTruthOverallBaseStats.TryGetValue(type, out var truthStats))
                        overallBaseStats = overallBaseStats.Add(StatsType.Base,
                            StatsUnit.Create(truthStats, perTypeQueryOverallBaseStats[type]));
                    var metrics = BenchmarkMetrics<PrimaryCategory>.Create(type, overallBaseStats, binnedDict);
                    benchMarkMetrics.Add(type, metrics);

                    if (type is Category) continue;
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
                ret[what] = SampleMetrics.Create(SamplePair.Create(truth.SampleName, query.SampleName), overallStats,
                    benchMarkMetrics.ToImmutable());
            }

            return ret;
        }

        private static IDictionary<PrimaryCategory, BinnedDictionary> CreatePerTypeBinnedDictionary(
            IReadOnlyDictionary<WittyerType, InputSpec> inputSpecs, bool isCrossType)
        {
            IReadOnlyList<(uint size, bool skip)> GetBins(IEnumerable<(uint size, bool skip)> binSizes)
            {
                var sorted = binSizes.OrderBy(it => it.size).ThenBy(it => it.skip ? 0 : 1).Distinct();
                (uint size, bool skip)? lastBin = null;
                var results = new List<(uint size, bool skip)>();
                foreach (var bin in sorted)
                {
                    if (lastBin == null)
                    {
                        lastBin = bin;
                        results.Add(bin);
                        continue;
                    }

                    if (lastBin.Value.size != bin.size)
                        results.Add(bin);
                    lastBin = bin;
                }

                if (results.Count > 0 && results[0].size != 0 && results[0].size != 1)
                    results.Insert(0, (WittyerConstants.StartingBin, results[0].skip));

                return results;
            }

            var perTypeBinnedDictionary =
                inputSpecs.ToDictionary(kvp => Category.Create(kvp.Key),
                    kvp => new BinnedDictionary(GetBins(kvp.Value.BinSizes),
                        kvp.Key.HasBaseLevelStats, kvp.Key.HasLengths));

            if (isCrossType)
            {
                foreach (var categories in CrossTypeCategories.Values)
                foreach (var category in categories)
                    if (!perTypeBinnedDictionary.ContainsKey(category))
                        if (inputSpecs.TryGetValue(category.MainType, out var keyInputSpec) &&
                            inputSpecs.ContainsKey(category.SecondaryType))
                        {
                            perTypeBinnedDictionary[category] = new BinnedDictionary(
                                GetBins(keyInputSpec.BinSizes),
                                category.HasBaseLevelStats,
                                category.HasLengths);
                        }
            }

            return perTypeBinnedDictionary;
        }

        private static Dictionary<(MatchEnum what, bool isGenotypeEvaluated), (IBasicStatsCount overallBaseStats,
            IReadOnlyDictionary<PrimaryCategory, IBasicStatsCount> perTypeOverallBaseStats)>
            GenerateStats(IReadOnlyDictionary<WittyerType, InputSpec> inputSpecs,
                IDictionary<(MatchEnum what, bool isGenotypeMatched), IDictionary<PrimaryCategory, BinnedDictionary>> perTypeBinnedDictionary,
                IWittyerResult result,
                Func<IMutableStats, IMutableEventStatsCount> eventsStatsSelector,
                Func<MutableEventAndBasesStats, IMutableBaseStatsCount> baseStatsSelector,
                bool isCrossType)
        {
            // tracks the summary OverallStats total base stats
            var grandTotalDictionary = new ConcurrentDictionary<IContigInfo, List<IInterval<uint>>>();
            var grandTotalTpDictionary =
                new ConcurrentDictionary<(MatchEnum what, bool isGenotypeEvaluated),
                    ConcurrentDictionary<IContigInfo, List<IInterval<uint>>>>();

            // tracks the per type OverallStats for bases
            var perTypeTotalDictionary =
                new ConcurrentDictionary<PrimaryCategory, ConcurrentDictionary<IContigInfo, List<IInterval<uint>>>>();
            var perTypeTotalTpDictionary = new ConcurrentDictionary<(MatchEnum what, bool isGenotypeEvaluated), ConcurrentDictionary<PrimaryCategory,
                ConcurrentDictionary<IContigInfo, List<IInterval<uint>>>>>();

            // tracks the Per Type Per bin base stats
            var perTypePerBinTotalDictionary =
                new ConcurrentDictionary<PrimaryCategory, ConcurrentDictionary<uint,
                    ConcurrentDictionary<IContigInfo, List<IInterval<uint>>>>>();
            var perTypePerBinTpDictionary =
                new ConcurrentDictionary<(MatchEnum what, bool isGenotypeEvaluated), ConcurrentDictionary<PrimaryCategory, ConcurrentDictionary<uint,
                    ConcurrentDictionary<IContigInfo, List<IInterval<uint>>>>>>();

            foreach (var whatT in perTypeBinnedDictionary.Keys)
            {
                grandTotalTpDictionary[whatT] = new ConcurrentDictionary<IContigInfo, List<IInterval<uint>>>();
                perTypeTotalTpDictionary[whatT] =
                    new ConcurrentDictionary<PrimaryCategory,
                        ConcurrentDictionary<IContigInfo, List<IInterval<uint>>>>();
                perTypePerBinTpDictionary[whatT] =
                    new ConcurrentDictionary<PrimaryCategory, ConcurrentDictionary<uint,
                        ConcurrentDictionary<IContigInfo, List<IInterval<uint>>>>>();
            }

            var firstPerTypeBinnedDictionary = perTypeBinnedDictionary.First().Value;
            foreach (var (type, variants) in result.Variants
                         .Select(kvp => (kvp.Key, kvp.Value.AsEnumerable<IWittyerSimpleVariant>()))
                         .Concat(result.BreakendPairsAndInsertions
                             .Select(kvp => (kvp.Key, kvp.Value.AsEnumerable<IWittyerSimpleVariant>()))))
            {
                var bedRegion = inputSpecs[type].IncludedRegions?.IntervalTree;
                var baseType = Category.Create(type);
                var categories = new HashSet<PrimaryCategory> { baseType };
                if (isCrossType && CrossTypeCategories.TryGetValue(type, out var extraCategories))
                    foreach (var category in extraCategories)
                    {
                        if ((category.MainType == type || category.SecondaryType == type)
                            && firstPerTypeBinnedDictionary.ContainsKey(category))
                            categories.Add(category);
                    }

                foreach (var variant in variants)
                {
                    var length = variant.SvLenInterval?.GetLength();

                    if (type == WittyerType.CopyNumberTandemRepeat && (length == null || !isCrossType))
                        // for CNTR, if isCrossType, we use RefLength as backup, otherwise, we always use RefLength.
                        length = variant.GetRefLength();

                    var categoriesToSkip = new HashSet<Category>();
                    if (variant.VariantType == WittyerType.CopyNumberTandemRepeat)
                    {
                        var sample = (IWittyerGenotypedCopyNumberSample)((IWittyerVariant)variant).Sample;
                        var sampleCn = sample.Cn;
                        if (sampleCn == null)
                        {
                            categoriesToSkip.Add(DelAndCnTrLossCategory);
                            categoriesToSkip.Add(RefAndCnTrefCategory);
                            categoriesToSkip.Add(InsAndCnTrGainCategory);
                        }
                        else
                        {
                            decimal gtCount = sample.Gt.GenotypeIndices.Count;
                            if (variant.OriginalVariant.Info.TryGetValue(VcfConstants.CnSampleFieldKey,
                                    out var cnString))
                            {
                                var cnSplit = cnString.Split(VcfConstants.InfoFieldValueDelimiter);
                                var variantIndex = sample.Gt.GenotypeIndices.FirstOrDefault(it => it != "0" && it != VcfConstants.MissingValueString);
                                if (variantIndex != null
                                    && int.TryParse(variantIndex, out var i)
                                    && decimal.TryParse(cnSplit[i - 1], out var cn))
                                {
                                    sampleCn = cn;
                                    if (sampleCn < 0) // might cause problems, but for now, we'll be robust
                                        sampleCn = -sampleCn;
                                    gtCount = 1M;
                                }
                            }

                            if (sampleCn > gtCount)
                            {
                                categoriesToSkip.Add(DelAndCnTrLossCategory);
                                categoriesToSkip.Add(RefAndCnTrefCategory);
                            }
                            else if (sampleCn < gtCount)
                            {
                                categoriesToSkip.Add(InsAndCnTrGainCategory);
                                categoriesToSkip.Add(RefAndCnTrefCategory);
                            }
                            else // somehow ref?
                            {
                                categoriesToSkip.Add(DelAndCnTrLossCategory);
                                categoriesToSkip.Add(InsAndCnTrGainCategory);
                            }
                        }
                    }

                    var categoryToBinMap = new Dictionary<PrimaryCategory, (uint size, bool skip)?>();
                    
                    FilterCategoriesToNotSkip(
                        firstPerTypeBinnedDictionary, categories, length, baseType, categoryToBinMap, categoriesToSkip);

                    if (categoryToBinMap.Count == 0)
                        continue; // means there's only the base type and it's skip!  if there's 2+ it means one of them is not skip.

                    //Event level stats
                    var assessed = true;
                    foreach (var (category, bin) in categoryToBinMap)
                    {
                        if (bin?.skip ?? categoriesToSkip.Contains(category)) continue;
                        foreach (var whatT in perTypeBinnedDictionary.Keys)
                        {
                            var (what, isGenotyped) = whatT;
                            var binnedDictionary = perTypeBinnedDictionary[whatT][category];
                            var mutableStats = binnedDictionary[bin?.size];
                            var eventStats = eventsStatsSelector(mutableStats);
                            if (variant.Sample.Wit == WitDecision.NotAssessed)
                                assessed = false;
                            else if (
                                variant.Sample.What.Any(it =>
                                    (!isGenotyped || it.Contains(MatchEnum.Genotype))
                                    && (what <= MatchEnum.Length
                                        ? it.Contains(what)
                                        : it.Any(x =>
                                            x >= (WittyerConstants.SequenceComparable.Contains(
                                                variant.VariantType)
                                                ? what
                                                : MatchEnum.Length)))
                                )
                            )
                                eventStats.AddTrueEvent();
                            else
                                eventStats.AddFalseEvent();
                        }
                    }

                    if (!type.HasBaseLevelStats) continue;
                    
                    // if there's actually a bed region, but the bed region lacks this contig, we should skip this part
                    IIntervalTree<uint, IContigAndInterval>? bedTree = null;
                    if (bedRegion != null && !bedRegion.TryGetValue(variant.Contig, out bedTree)) continue;
                    var foundOverlap = true;
                    if (assessed)
                    {
                        foundOverlap = false;
                        foreach (var overlapped in bedTree?.Search(variant) ??
                                                   new IContigAndInterval[] { variant })
                        {
                            foundOverlap = true;
                            var actualOverlap = ReferenceEquals(variant, overlapped)
                                ? variant
                                : overlapped.TryGetOverlap(variant).GetOrThrow();
                            foreach (var (category, bin) in categoryToBinMap)
                            {
                                if (bin == null
                                    || bin.Value.skip
                                    || !category.HasBaseLevelStats
                                    || categoriesToSkip.Contains(category))
                                    continue;

                                var perBinTotalDictionary = perTypePerBinTotalDictionary.GetOrAdd(category,
                                    _ => new ConcurrentDictionary<uint,
                                        ConcurrentDictionary<IContigInfo, List<IInterval<uint>>>>());
                                var perTypePerBinTotalTrees =
                                    GetOrAddGenomeTree(in perBinTotalDictionary, bin.Value.size);
                                GetOrAddTree(in perTypePerBinTotalTrees, in variant).Add(actualOverlap);
                                var typeTotalTrees = perTypeTotalDictionary.GetOrAdd(category,
                                    _ => new ConcurrentDictionary<IContigInfo, List<IInterval<uint>>>());
                                GetOrAddTree(in typeTotalTrees, in variant).Add(actualOverlap);
                                if (category == baseType)
                                    GetOrAddTree(in grandTotalDictionary, in variant).Add(actualOverlap);
                            }
                        }
                    }

                    if (!foundOverlap) continue;
                    
                    foreach (var annotation in variant.OverlapInfo)
                    {
                        var wow = annotation.Wow;
                        if (wow == null)
                            continue;
                        // ignore genotype matching for now, and process as if all positive
                        var overlaps = bedTree?.Search(wow)
                                           .Select(overlap =>
                                               overlap.TryGetOverlap(wow).GetOrThrow()).ToList() ??
                                       new List<IInterval<uint>> { wow };
                        foreach (var (category, bin) in categoryToBinMap)
                        {
                            if (bin == null
                                || bin.Value.skip
                                || !category.HasBaseLevelStats
                                || categoriesToSkip.Contains(category))
                                continue;
                            foreach (var whatT in perTypeBinnedDictionary.Keys)
                            {
                                var isTp = !whatT.isGenotypeMatched || annotation.What.Contains(MatchEnum.Genotype);
                                var binnedDictionary = perTypeBinnedDictionary[whatT][category];
                                var mutableStats = binnedDictionary[bin.Value.size];
                                foreach (var overlap in overlaps)
                                {
                                    // search the bedtree for all the overlapping bed regions,
                                    //   then extract the actual overlapping coordinates (exclude non-overlapping)
                                    if (mutableStats is MutableEventAndBasesStats mbt)
                                    {
                                        var baseStats = baseStatsSelector(mbt);
                                        if (isTp)
                                        {
                                            baseStats.AddTrueCount(variant.Contig, overlap);
                                            var perBinTpDictionary = perTypePerBinTpDictionary
                                                .GetOrAdd(whatT,
                                                    _ => new ConcurrentDictionary<PrimaryCategory, ConcurrentDictionary<
                                                        uint
                                                        , ConcurrentDictionary<IContigInfo, List<IInterval<uint>>>>>())
                                                .GetOrAdd(category,
                                                    _ => new ConcurrentDictionary<uint, ConcurrentDictionary<IContigInfo
                                                        ,
                                                        List<IInterval<uint>>>>());
                                            var perTypePerBinTpTrees =
                                                GetOrAddGenomeTree(in perBinTpDictionary, bin.Value.size);
                                            GetOrAddTree(in perTypePerBinTpTrees, in variant).Add(overlap);

                                            var typeTotalTpTrees = perTypeTotalTpDictionary
                                                .GetOrAdd(whatT,
                                                    _ => new ConcurrentDictionary<PrimaryCategory,
                                                        ConcurrentDictionary<IContigInfo, List<IInterval<uint>>>>())
                                                .GetOrAdd(category,
                                                    _ => new ConcurrentDictionary<IContigInfo,
                                                        List<IInterval<uint>>>());
                                            GetOrAddTree(in typeTotalTpTrees, in variant).Add(overlap);
                                            var gTotalTpDictionary = grandTotalTpDictionary.GetOrAdd(whatT,
                                                _ => new ConcurrentDictionary<IContigInfo, List<IInterval<uint>>>());
                                            if (category == baseType)
                                                GetOrAddTree(in gTotalTpDictionary, in variant).Add(overlap);
                                        }

                                        if (assessed) continue;
                                        // for not assessed variants, we haven't counted their bases to total trees yet
                                        var perBinTotalDictionary = perTypePerBinTotalDictionary.GetOrAdd(
                                            category,
                                            _ => new ConcurrentDictionary<uint,
                                                ConcurrentDictionary<IContigInfo,
                                                    List<IInterval<uint>>>>());
                                        var perTypePerBinTotalTrees =
                                            GetOrAddGenomeTree(in perBinTotalDictionary, bin.Value.size);
                                        GetOrAddTree(in perTypePerBinTotalTrees, in variant)
                                            .Add(overlap);
                                        var typeTotalTrees = perTypeTotalDictionary.GetOrAdd(category,
                                            _ => new ConcurrentDictionary<IContigInfo,
                                                List<IInterval<uint>>>());
                                        GetOrAddTree(in typeTotalTrees, in variant).Add(overlap);
                                        if (category == baseType)
                                            GetOrAddTree(in grandTotalDictionary, in variant)
                                                .Add(overlap);
                                    }
                                    else
                                        throw new InvalidDataException(
                                            $"Not sure why {variant.OriginalVariant} with {variant.VariantType} cannot generate base level stats. Check with witty.er developer! (or debug yourself...?)");
                                }
                            }
                        }
                    }
                }

                if (!type.HasBaseLevelStats) continue;

                foreach (var category in categories)
                {
                    foreach (var whatT in perTypeBinnedDictionary.Keys)
                    {
                        var pTypeBinnedDictionary = perTypeBinnedDictionary[whatT];
                        foreach (var bin in pTypeBinnedDictionary[category].Bins)
                        {
                            if (bin.skip || !type.HasBaseLevelStats) continue;
                            var perBinTotalDictionary = perTypePerBinTotalDictionary.GetOrAdd(
                                category,
                                _ => new ConcurrentDictionary<uint,
                                    ConcurrentDictionary<IContigInfo,
                                        List<IInterval<uint>>>>());
                            var perTypePerBinTotalTrees =
                                GetOrAddGenomeTree(in perBinTotalDictionary, bin.size);

                            var binnedDictionary = pTypeBinnedDictionary[category];
                            var mutableStats = binnedDictionary[bin.size];
                            IMutableBaseStatsCount? stats = null;
                            foreach (var chr in perTypePerBinTotalTrees.Keys)
                            {
                                var totalTree = perTypePerBinTotalTrees[chr];
                                stats ??= baseStatsSelector((MutableEventAndBasesStats)mutableStats);
                                var perBinTpDictionary = perTypePerBinTpDictionary[whatT].GetOrAdd(
                                    category,
                                    _ => new ConcurrentDictionary<uint,
                                        ConcurrentDictionary<IContigInfo,
                                            List<IInterval<uint>>>>());
                                var perTypePerBinTpTrees =
                                    GetOrAddGenomeTree(in perBinTpDictionary, bin.size);
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
                }
            }

            var grandTotalTpCount = grandTotalTpDictionary.ToDictionary(
                it => it.Key,
                it => it.Value.GetTotalLength());

            var categoriesToTotal = WittyerType.AllTypes.Where(type => type.HasBaseLevelStats).Select(Category.Create);
            if (isCrossType)
                categoriesToTotal = categoriesToTotal.SelectMany(it =>
                    CrossTypeCategories.TryGetValue(it.MainType, out var additionalCategories)
                        ? it.FollowedBy(additionalCategories)
                        : it.FollowedBy()).Distinct();
            var grandTotalBases = grandTotalDictionary.GetTotalLength();
            return perTypeTotalTpDictionary.ToDictionary(kvp => kvp.Key,
                kvp =>
                    (BasicStatsCount.Create(grandTotalTpCount[kvp.Key], grandTotalBases - grandTotalTpCount[kvp.Key]),
                        // must filter out keys that don't have base level stats for better cleanliness in case we want to not output Json stats etc for these
                        (IReadOnlyDictionary<PrimaryCategory, IBasicStatsCount>)categoriesToTotal.ToDictionary(
                            type => type,
                            type =>
                            {
                                var tpCount = kvp.Value.TryGetValue(type, out var tree)
                                    ? tree.GetTotalLength()
                                    : 0U;
                                var totalCount = perTypeTotalDictionary.TryGetValue(type, out tree)
                                    ? tree.GetTotalLength()
                                    : 0;
                                if (tpCount > totalCount)
                                    throw new InvalidDataException(
                                        $"Somehow, got {nameof(tpCount)} ({tpCount}) to be greater than {nameof(totalCount)} ({totalCount})");
                                var falseCount = totalCount - tpCount;
                                return BasicStatsCount.Create(tpCount, falseCount);
                            }
                        )));

            List<IInterval<uint>> GetOrAddTree(in ConcurrentDictionary<IContigInfo, List<IInterval<uint>>> dict,
                in IWittyerSimpleVariant variant)
                => dict.GetOrAdd(variant.Contig, _ => new List<IInterval<uint>>());

            ConcurrentDictionary<IContigInfo, List<IInterval<uint>>> GetOrAddGenomeTree<T>(
                in ConcurrentDictionary<T, ConcurrentDictionary<IContigInfo, List<IInterval<uint>>>>
                    perBinTpDictionary, T size) where T : notnull => perBinTpDictionary.GetOrAdd(size,
                    _ => new ConcurrentDictionary<IContigInfo, List<IInterval<uint>>>());
        }

        private static void FilterCategoriesToNotSkip(
            IDictionary<PrimaryCategory, BinnedDictionary> perTypeBinnedDictionary, HashSet<PrimaryCategory> categories, uint? length,
            PrimaryCategory baseCategory, IDictionary<PrimaryCategory, (uint size, bool skip)?> categoryToBinMap,
            IReadOnlyCollection<Category> categoriesToSkip)
        {
            // possibilities:
            // 1. baseCategory == Deletion (primary), other categories = CNTR+Deletion, CNGain+Deletion (basically they all act together since all bins are based on primary)
            //    a. baseCategory and other categories == skip, so skip the whole thing, should be empty.
            //    b. baseCategory and CNTR == skip, CNGain = don't skip. skip everything,
            //       should be empty since this base type should not contribute to CNGain and
            //       the combo bins are the same as base so if that skips, they automatically all skip
            //    c. baseCategory != skip, others cannot be skip!.  report to baseCategory and other categories'
            // 2. baseCategory == CNGain, other categories == CNGain+Deletion
            //    a. everything skip means skip
            //    b. baseCategory == skip but other category != skip.  should contain base and other category
            //    c. baseCategory != skip but other category == skip.  should contain base only.
            //    d. baseCategory != skip but other category != skip.  should contain both
            (uint size, bool skip)? baseBin = null;
            foreach (var category in categories)
            {
                if (categoriesToSkip.Contains(category))
                    continue;
                var binnedDictionary = perTypeBinnedDictionary[category];
                (uint size, bool skip)? bin = null;
                if (length != null)
                {
                    var binIndex = Winner.GetBinIndex(binnedDictionary.Bins, length.Value);
                    bin = binnedDictionary.Bins[binIndex];
                    if (category == baseCategory)
                        baseBin = bin;
                }
                else if (baseCategory.HasLengths)
                    throw new InvalidDataException(
                        $"Somehow we got to a state with {baseCategory} having no detected length!");

                if (bin?.skip ?? false) continue;
                categoryToBinMap[category] = bin;
            }

            if (categoryToBinMap.Count != 0 && baseBin != null)
                categoryToBinMap[baseCategory] = baseBin.Value;
        }
    }
}
