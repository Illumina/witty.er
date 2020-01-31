using System.Collections.Generic;
using System.Linq;
using Ilmn.Das.App.Wittyer.Input;
using Ilmn.Das.App.Wittyer.Stats;
using Ilmn.Das.Std.AppUtils.Collections;
using JetBrains.Annotations;
using Newtonsoft.Json;

namespace Ilmn.Das.App.Wittyer.Json
{
    /// <summary>
    /// A data class for <see cref="SampleStats"/>
    /// </summary>
    /// <seealso cref="ISamplePair" />
    public class SampleStats : ISamplePair
    {
        private readonly ISamplePair _samplePair;

        /// <inheritdoc />
        public string QuerySampleName => _samplePair.QuerySampleName;

        /// <inheritdoc />
        public string TruthSampleName => _samplePair.TruthSampleName;

        /// <summary>
        /// Gets the overall stats.
        /// </summary>
        /// <value>
        /// The overall stats.
        /// </value>
        public IReadOnlyList<BasicJsonStats> OverallStats { get; }

        /// <summary>
        /// Gets the detailed stats.
        /// </summary>
        /// <value>
        /// The detailed stats.
        /// </value>
        public IReadOnlyList<SvTypeJsonStats> DetailedStats { get; }

        [JsonConstructor]
        private SampleStats([NotNull] ISamplePair samplePair, IReadOnlyList<BasicJsonStats> overallStats,
            IReadOnlyList<SvTypeJsonStats> detailedStats)
        {
            _samplePair = samplePair;
            OverallStats = overallStats;
            DetailedStats = detailedStats;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SampleStats"/> class from the given benchmarkResults.
        /// </summary>
        [NotNull]
        public static SampleStats Create([NotNull] SampleMetrics benchmarkResults)
        {
            var overallStats = benchmarkResults.OverallStats.Select(kvp => BasicJsonStats.Create(kvp.Key,
                kvp.Value.TruthStats.TrueCount, kvp.Value.TruthStats.FalseCount, kvp.Value.QueryStats.TrueCount,
                kvp.Value.QueryStats.FalseCount)).ToReadOnlyList();

            var detailedStats = benchmarkResults.DetailedStats.Select(kvp => kvp.Value).OrderBy(b => b.Category.Name)
                .Select(SvTypeJsonStats.Create).ToReadOnlyList();

            return new SampleStats(benchmarkResults.SamplePair, overallStats, detailedStats);
        }
    }
}