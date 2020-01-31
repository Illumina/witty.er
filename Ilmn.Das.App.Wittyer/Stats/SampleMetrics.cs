using System.Collections.Immutable;
using Ilmn.Das.App.Wittyer.Input;
using Ilmn.Das.App.Wittyer.Stats.Counts;
using Ilmn.Das.App.Wittyer.Vcf.Variants;
using JetBrains.Annotations;

namespace Ilmn.Das.App.Wittyer.Stats
{
    public class SampleMetrics
    {
        [NotNull] public readonly ISamplePair SamplePair;

        [NotNull] public readonly IImmutableDictionary<StatsType, IStatsUnit> OverallStats;

        [NotNull]
        public readonly IImmutableDictionary<WittyerVariantType, IBenchmarkMetrics<WittyerVariantType>> DetailedStats;

        /// <summary>
        /// Initializes a new instance of the <see cref="SampleMetrics"/> class.
        /// </summary>
        /// <param name="samplePair">The sample pair.</param>
        /// <param name="overallStats">The overall stats.</param>
        /// <param name="detailedStats">The detailed stats.</param>
        private SampleMetrics([NotNull] ISamplePair samplePair,
            [NotNull] IImmutableDictionary<StatsType, IStatsUnit> overallStats,
            [NotNull] IImmutableDictionary<WittyerVariantType, IBenchmarkMetrics<WittyerVariantType>> detailedStats)
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
            [NotNull] IImmutableDictionary<WittyerVariantType, IBenchmarkMetrics<WittyerVariantType>> detailedStats)
            => new SampleMetrics(samplePair, overallStats, detailedStats);
    }
}