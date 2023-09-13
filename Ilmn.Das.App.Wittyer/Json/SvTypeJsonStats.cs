using System.Collections.Generic;
using System.Linq;
using Ilmn.Das.App.Wittyer.Infrastructure;
using Ilmn.Das.App.Wittyer.Stats;
using Ilmn.Das.App.Wittyer.Utilities;
using Ilmn.Das.App.Wittyer.Vcf.Variants;
using Ilmn.Das.Std.AppUtils.Collections;
using Newtonsoft.Json;

namespace Ilmn.Das.App.Wittyer.Json
{
    /// <summary>
    /// The JsonStats
    /// </summary>
    public class SvTypeJsonStats
    {
        /// <summary>
        /// Gets the type of the variant.
        /// </summary>
        /// <value>
        /// The type of the variant.
        /// </value>
        public string VariantType { get; }

        /// <summary>
        /// Gets the overall stats.
        /// </summary>
        /// <value>
        /// The overall stats.
        /// </value>
        public IReadOnlyList<BasicJsonStats> OverallStats { get; }

        /// <summary>
        /// Gets the per bin stats.
        /// </summary>
        /// <value>
        /// The per bin stats.
        /// </value>
        public IReadOnlyList<BinJsonStats> PerBinStats { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="SvTypeJsonStats"/> class.
        /// </summary>
        /// <param name="variantType">Type of the variant.</param>
        /// <param name="overallStats">The overall stats.</param>
        /// <param name="perBinStats">The per bin stats.</param>
        [JsonConstructor]
        private SvTypeJsonStats(string variantType, IReadOnlyList<BasicJsonStats> overallStats,
            IReadOnlyList<BinJsonStats> perBinStats)
        {
            VariantType = variantType;
            OverallStats = overallStats;
            PerBinStats = perBinStats;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SvTypeJsonStats"/> class using the given benchmarkMetrics.
        /// </summary>
        public static SvTypeJsonStats Create(IBenchmarkMetrics<PrimaryCategory> benchmarkMetrics)
        {
            if (benchmarkMetrics.Category.Is(WittyerType.TranslocationBreakend))
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
                            WittyerConstants.Json.InfiniteBin, benchmarkMetrics.Category)
                    });
            }

            var binJsonStatsList = new List<BinJsonStats>();
            for (var i = 0; i < benchmarkMetrics.BinnedStats.Count; i++)
            {
                var perBinStatsObject = benchmarkMetrics.BinnedStats[i];
                if (perBinStatsObject.IgnoreForStatsCalculations) continue;

                // Get the string representation of the next bin start, which will be infinity
                // if this is the last bin.
                var nextBinString = i != benchmarkMetrics.BinnedStats.Count - 1 
                    ? benchmarkMetrics.BinnedStats[i + 1].Bin.ToString() 
                    : WittyerConstants.Json.InfiniteBin;
                binJsonStatsList.Add(BinJsonStats.Create(perBinStatsObject, nextBinString, benchmarkMetrics.Category));
            }

            var overallStats = benchmarkMetrics.OverallStats
                .Select(s => BasicJsonStats.Create(s.Key, s.Value.TruthStats.TrueCount,
                    s.Value.TruthStats.FalseCount, s.Value.QueryStats.TrueCount, s.Value.QueryStats.FalseCount))
                .ToReadOnlyList();
            return new SvTypeJsonStats(benchmarkMetrics.Category.ToString(), overallStats, binJsonStatsList);
        }
    }
}