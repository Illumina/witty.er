using System.Collections.Generic;
using System.Linq;
using Ilmn.Das.App.Wittyer.Stats;
using Ilmn.Das.App.Wittyer.Utilities;
using Ilmn.Das.App.Wittyer.Vcf.Variants;
using Ilmn.Das.Std.AppUtils.Collections;
using JetBrains.Annotations;
using Newtonsoft.Json;

namespace Ilmn.Das.App.Wittyer.Json
{
    public class SvTypeJsonStats
    {
        public string VariantType { get; }

        public IReadOnlyList<BasicJsonStats> OverallStats { get; }

        public IReadOnlyList<BinJsonStats> PerBinStats { get; }

        [JsonConstructor]
        private SvTypeJsonStats(string variantType, IReadOnlyList<BasicJsonStats> overallStats,
            IReadOnlyList<BinJsonStats> perBinStats)
        {
            VariantType = variantType;
            PerBinStats = perBinStats;
            OverallStats = overallStats;
        }

        [NotNull]
        public static SvTypeJsonStats Create([NotNull] IBenchmarkMetrics<WittyerVariantType> benchmarkMetrics)
        {
            if (benchmarkMetrics.Category == WittyerVariantType.TranslocationBreakend)
            {
                var stats = benchmarkMetrics.BinnedStats[0];
                var eventStats = stats.Stats[StatsType.Event];
                return new SvTypeJsonStats(benchmarkMetrics.Category.ToString(),
                    new[]
                    { // overall stats are same as bin stats since no bins
                        BasicJsonStats.Create(StatsType.Event,
                            eventStats.TruthStats.TrueCount,
                            eventStats.TruthStats.FalseCount,
                            eventStats.QueryStats.TrueCount,
                            eventStats.QueryStats.FalseCount)
                    }, 
                    new[]
                    {
                        BinJsonStats.Create(stats,
                            WittyerConstants.Json.InfinteBin, benchmarkMetrics.Category)
                    });
            }

            var perBinStats = new List<BinJsonStats>();
            var nextBin = 1;
            foreach (var binnedStats in benchmarkMetrics.BinnedStats)
            {
                var nextBinString = nextBin < benchmarkMetrics.BinnedStats.Count
                    ? benchmarkMetrics.BinnedStats[nextBin].Bin.ToString()
                    : WittyerConstants.Json.InfinteBin;
                perBinStats.Add(BinJsonStats.Create(binnedStats, nextBinString, benchmarkMetrics.Category));
                nextBin++;
            }

            return new SvTypeJsonStats(benchmarkMetrics.Category.ToString(),
                benchmarkMetrics.OverallStats.Select(s => BasicJsonStats.Create(s.Key, s.Value.TruthStats.TrueCount,
                        s.Value.TruthStats.FalseCount, s.Value.QueryStats.TrueCount, s.Value.QueryStats.FalseCount))
                    .ToReadOnlyList(), perBinStats);
        }
    }
}