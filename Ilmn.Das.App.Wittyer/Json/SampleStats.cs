using System.Collections.Generic;
using System.Linq;
using Ilmn.Das.App.Wittyer.Input;
using Ilmn.Das.App.Wittyer.Stats;
using Ilmn.Das.Std.AppUtils.Collections;
using JetBrains.Annotations;
using Newtonsoft.Json;

namespace Ilmn.Das.App.Wittyer.Json
{
    public class SampleStats : ISamplePair
    {
        private readonly ISamplePair _samplePair;

        public string QuerySampleName => _samplePair.QuerySampleName;

        public string TruthSampleName => _samplePair.TruthSampleName;

        public IReadOnlyList<BasicJsonStats> OverallStats { get; }

        public IReadOnlyList<SvTypeJsonStats> DetailedStats { get; }

        [JsonConstructor]
        private SampleStats([NotNull] ISamplePair samplePair, IReadOnlyList<BasicJsonStats> overallStats,
            IReadOnlyList<SvTypeJsonStats> detailedStats)
        {
            _samplePair = samplePair;
            OverallStats = overallStats;
            DetailedStats = detailedStats;
        }

        [NotNull]
        public static SampleStats Create([NotNull] SampleMetrics benmarkResult)
        {
            var overallStats = benmarkResult.OverallStats.Select(kvp => BasicJsonStats.Create(kvp.Key,
                kvp.Value.TruthStats.TrueCount, kvp.Value.TruthStats.FalseCount, kvp.Value.QueryStats.TrueCount,
                kvp.Value.QueryStats.FalseCount)).ToReadOnlyList();

            var detailedStats = benmarkResult.DetailedStats.Select(kvp => kvp.Value).OrderBy(b => b.Category)
                .Select(SvTypeJsonStats.Create).ToReadOnlyList();

            return new SampleStats(benmarkResult.SamplePair, overallStats, detailedStats);
        }
    }
}