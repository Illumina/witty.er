using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Ilmn.Das.App.Wittyer.Infrastructure;
using Ilmn.Das.App.Wittyer.Input;
using Ilmn.Das.App.Wittyer.Stats.Counts;
using Ilmn.Das.App.Wittyer.Utilities;
using JetBrains.Annotations;

namespace Ilmn.Das.App.Wittyer.Stats
{
    public class SampleMetrics
    {
        public readonly ISamplePair SamplePair;

        public readonly IImmutableDictionary<StatsType, IStatsUnit> OverallStats;

        public readonly IImmutableDictionary<PrimaryCategory, IBenchmarkMetrics<PrimaryCategory>> DetailedStats;

        /// <summary>
        /// Initializes a new instance of the <see cref="SampleMetrics"/> class.
        /// </summary>
        /// <param name="samplePair">The sample pair.</param>
        /// <param name="overallStats">The overall stats.</param>
        /// <param name="detailedStats">The detailed stats.</param>
        private SampleMetrics(ISamplePair samplePair,
            IImmutableDictionary<StatsType, IStatsUnit> overallStats,
            IImmutableDictionary<PrimaryCategory, IBenchmarkMetrics<PrimaryCategory>> detailedStats)
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
        [Pure]
        public static SampleMetrics Create(ISamplePair samplePair,
            IImmutableDictionary<StatsType, IStatsUnit> overallStats,
            IImmutableDictionary<PrimaryCategory, IBenchmarkMetrics<PrimaryCategory>> detailedStats)
            => new(samplePair, overallStats, detailedStats);

        /// <summary>
        /// Gets the Overall Event Level Recall Stats
        /// </summary>
        public IEnumerable<(PrimaryCategory type, double recall)> EventLevelRecallOverall
            => StatsOverall(StatsType.Event, WittyerUtils.GetRecall);

        /// <summary>
        /// Gets the Overall Base Level Recall Stats
        /// </summary>
        public IEnumerable<(PrimaryCategory type, double recall)> BaseLevelRecallOverall
            => StatsOverall(StatsType.Base, WittyerUtils.GetRecall);

        /// <summary>
        /// Gets the Overall Event Level Precision Stats
        /// </summary>
        public IEnumerable<(PrimaryCategory type, double precision)> EventLevelPrecisionOverall
            => StatsOverall(StatsType.Event, WittyerUtils.GetPrecision);

        /// <summary>
        /// Gets the Overall Base Level Precision Stats
        /// </summary>
        public IEnumerable<(PrimaryCategory type, double precision)> BaseLevelPrecisionOverall
            => StatsOverall(StatsType.Base, WittyerUtils.GetPrecision);

        /// <summary>
        /// Gets the Overall Event Level F-score Stats
        /// </summary>
        public IEnumerable<(PrimaryCategory type, double fscore)> EventLevelFscoreOverall
            => StatsOverall(StatsType.Event, WittyerUtils.GetFscore);

        /// <summary>
        /// Gets the Overall Base Level F-score Stats
        /// </summary>
        public IEnumerable<(PrimaryCategory type, double fscore)> BaseLevelFscoreOverall
            => StatsOverall(StatsType.Base, WittyerUtils.GetFscore);

        /// <summary>
        /// Gets the Event Level Recall Stats per bin
        /// </summary>
        public IEnumerable<(PrimaryCategory type, IEnumerable<(uint? binStart, double recall)> perBinRecall)>
            EventLevelRecallPerBin
            => StatsPerBin(StatsType.Event, WittyerUtils.GetRecall);

        /// <summary>
        /// Gets the Base Level Recall Stats per bin
        /// </summary>
        public IEnumerable<(PrimaryCategory type, IEnumerable<(uint? binStart, double recall)> perBinRecall)>
            BaseLevelRecallPerBin
            => StatsPerBin(StatsType.Base, WittyerUtils.GetRecall);

        /// <summary>
        /// Gets the Event Level Precision Stats per bin
        /// </summary>
        public IEnumerable<(PrimaryCategory type, IEnumerable<(uint? binStart, double precision)> perBinRecall)>
            EventLevelPrecisionPerBin
            => StatsPerBin(StatsType.Event, WittyerUtils.GetPrecision);

        /// <summary>
        /// Gets the Base Level Precision Stats per bin
        /// </summary>
        public IEnumerable<(PrimaryCategory type, IEnumerable<(uint? binStart, double precision)> perBinRecall)>
            BaseLevelPrecisionPerBin
            => StatsPerBin(StatsType.Base, WittyerUtils.GetPrecision);

        /// <summary>
        /// Gets the Event Level F-Score per bin
        /// </summary>
        public IEnumerable<(PrimaryCategory type, IEnumerable<(uint? binStart, double fscore)> perBinFscore)>
            EventLevelFscorePerBin
            => StatsPerBin(StatsType.Event, WittyerUtils.GetFscore);

        /// <summary>
        /// Gets the Base Level F-Score per bin
        /// </summary>
        public IEnumerable<(PrimaryCategory type, IEnumerable<(uint? binStart, double fscore)> perBinFscore)>
            BasetLevelFscorePerBin
            => StatsPerBin(StatsType.Base, WittyerUtils.GetFscore);

        private IEnumerable<(PrimaryCategory type, IEnumerable<(uint? binStart, double recall)> perBinRecall)> StatsPerBin(
            StatsType type, Func<IStatsUnit, double> extractionFunc)
        {
            IEnumerable<(uint? Bin, double)> GetPerBinStats(KeyValuePair<PrimaryCategory, IBenchmarkMetrics<PrimaryCategory>> keyValuePair)
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

        private IEnumerable<(PrimaryCategory type, double recall)> StatsOverall(
            StatsType type, Func<IStatsUnit, double> extractionFunc)
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
