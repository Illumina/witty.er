using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Ilmn.Das.App.Wittyer.Input;
using Ilmn.Das.App.Wittyer.Stats.Counts;
using Ilmn.Das.App.Wittyer.Utilities;
using Ilmn.Das.App.Wittyer.Vcf.Variants;
using JetBrains.Annotations;

namespace Ilmn.Das.App.Wittyer.Stats
{
    public class SampleMetrics
    {
        [NotNull] public readonly ISamplePair SamplePair;

        [NotNull] public readonly IImmutableDictionary<StatsType, IStatsUnit> OverallStats;

        [NotNull]
        public readonly IImmutableDictionary<WittyerType, IBenchmarkMetrics<WittyerType>> DetailedStats;

        /// <summary>
        /// Initializes a new instance of the <see cref="SampleMetrics"/> class.
        /// </summary>
        /// <param name="samplePair">The sample pair.</param>
        /// <param name="overallStats">The overall stats.</param>
        /// <param name="detailedStats">The detailed stats.</param>
        private SampleMetrics([NotNull] ISamplePair samplePair,
            [NotNull] IImmutableDictionary<StatsType, IStatsUnit> overallStats,
            [NotNull] IImmutableDictionary<WittyerType, IBenchmarkMetrics<WittyerType>> detailedStats)
        {
            SamplePair = samplePair;
            OverallStats = overallStats;
            DetailedStats = detailedStats;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SampleMetrics"/> class.
        /// </summary>
        /// <param name="samplePair">The sample pair.</param>
        /// <param name="overallStats">The overall stats.</param>
        /// <param name="detailedStats">The detailed stats.</param>
        [NotNull]
        [Pure]
        public static SampleMetrics Create([NotNull] ISamplePair samplePair,
            [NotNull] IImmutableDictionary<StatsType, IStatsUnit> overallStats,
            [NotNull] IImmutableDictionary<WittyerType, IBenchmarkMetrics<WittyerType>> detailedStats)
            => new SampleMetrics(samplePair, overallStats, detailedStats);

        /// <summary>
        /// Gets the Overall Event Level Recall Stats
        /// </summary>
        [NotNull]
        public IEnumerable<(WittyerType type, double recall)> EventLevelRecallOverall
            => StatsOverall(StatsType.Event, WittyerUtils.GetRecall);

        /// <summary>
        /// Gets the Overall Base Level Recall Stats
        /// </summary>
        [NotNull]
        public IEnumerable<(WittyerType type, double recall)> BaseLevelRecallOverall
            => StatsOverall(StatsType.Base, WittyerUtils.GetRecall);

        /// <summary>
        /// Gets the Overall Event Level Precision Stats
        /// </summary>
        [NotNull]
        public IEnumerable<(WittyerType type, double precision)> EventLevelPrecisionOverall
            => StatsOverall(StatsType.Event, WittyerUtils.GetPrecision);

        /// <summary>
        /// Gets the Overall Base Level Precision Stats
        /// </summary>
        [NotNull]
        public IEnumerable<(WittyerType type, double precision)> BaseLevelPrecisionOverall
            => StatsOverall(StatsType.Base, WittyerUtils.GetPrecision);

        /// <summary>
        /// Gets the Overall Event Level F-score Stats
        /// </summary>
        [NotNull]
        public IEnumerable<(WittyerType type, double fscore)> EventLevelFscoreOverall
            => StatsOverall(StatsType.Event, WittyerUtils.GetFscore);

        /// <summary>
        /// Gets the Overall Base Level F-score Stats
        /// </summary>
        [NotNull]
        public IEnumerable<(WittyerType type, double fscore)> BaseLevelFscoreOverall
            => StatsOverall(StatsType.Base, WittyerUtils.GetFscore);

        /// <summary>
        /// Gets the Event Level Recall Stats per bin
        /// </summary>
        [NotNull]
        public IEnumerable<(WittyerType type, IEnumerable<(uint binStart, double recall)> perBinRecall)>
            EventLevelRecallPerBin
            => StatsPerBin(StatsType.Event, WittyerUtils.GetRecall);

        /// <summary>
        /// Gets the Base Level Recall Stats per bin
        /// </summary>
        [NotNull]
        public IEnumerable<(WittyerType type, IEnumerable<(uint binStart, double recall)> perBinRecall)>
            BaseLevelRecallPerBin
            => StatsPerBin(StatsType.Base, WittyerUtils.GetRecall);

        /// <summary>
        /// Gets the Event Level Precision Stats per bin
        /// </summary>
        [NotNull]
        public IEnumerable<(WittyerType type, IEnumerable<(uint binStart, double precision)> perBinRecall)>
            EventLevelPrecisionPerBin
            => StatsPerBin(StatsType.Event, WittyerUtils.GetPrecision);

        /// <summary>
        /// Gets the Base Level Precision Stats per bin
        /// </summary>
        [NotNull]
        public IEnumerable<(WittyerType type, IEnumerable<(uint binStart, double precision)> perBinRecall)>
            BaseLevelPrecisionPerBin
            => StatsPerBin(StatsType.Base, WittyerUtils.GetPrecision);

        /// <summary>
        /// Gets the Event Level F-Score per bin
        /// </summary>
        [NotNull]
        public IEnumerable<(WittyerType type, IEnumerable<(uint binStart, double fscore)> perBinFscore)>
            EventLevelFscorePerBin
            => StatsPerBin(StatsType.Event, WittyerUtils.GetFscore);

        /// <summary>
        /// Gets the Base Level F-Score per bin
        /// </summary>
        [NotNull]
        public IEnumerable<(WittyerType type, IEnumerable<(uint binStart, double fscore)> perBinFscore)>
            BasetLevelFscorePerBin
            => StatsPerBin(StatsType.Base, WittyerUtils.GetFscore);

        [NotNull]
        private IEnumerable<(WittyerType type, IEnumerable<(uint binStart, double recall)> perBinRecall)> StatsPerBin(
            StatsType type, [NotNull] Func<IStatsUnit, double> extractionFunc)
        {
            IEnumerable<(uint Bin, double)> GetPerBinStats(KeyValuePair<WittyerType, IBenchmarkMetrics<WittyerType>> keyValuePair)
            {
                foreach (var binStat in keyValuePair.Value.BinnedStats)
                {
                    if (!binStat.Stats.TryGetValue(type, out var stats))
                        continue;
                    yield return (binStat.Bin, extractionFunc(stats));
                }
            }

            return DetailedStats.Select(kvp => (kvp.Key, GetPerBinStats(kvp)));
        }

        [NotNull]
        private IEnumerable<(WittyerType type, double recall)> StatsOverall(
            StatsType type, [NotNull] Func<IStatsUnit, double> extractionFunc)
        {
            foreach (var kvp in DetailedStats)
            {
                var binStat = kvp.Value.OverallStats;
                if (!binStat.TryGetValue(type, out var stats))
                    continue;
                yield return (kvp.Key, extractionFunc(stats));
            }
        }
    }
}
