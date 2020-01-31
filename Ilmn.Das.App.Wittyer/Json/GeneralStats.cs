using System.Collections.Generic;
using System.Linq;
using Ilmn.Das.App.Wittyer.Infrastructure;
using Ilmn.Das.App.Wittyer.Input;
using Ilmn.Das.App.Wittyer.Results;
using Ilmn.Das.App.Wittyer.Stats;
using Ilmn.Das.App.Wittyer.Stats.Counts;
using Ilmn.Das.App.Wittyer.Utilities;
using Ilmn.Das.App.Wittyer.Vcf.Variants;
using JetBrains.Annotations;
using Newtonsoft.Json;

namespace Ilmn.Das.App.Wittyer.Json
{
    /// <summary>
    /// The data class that holds the high level stats.
    /// </summary>
    public class GeneralStats
    {
        /// <summary>
        /// Gets the command used to invoke Witty.er.
        /// </summary>
        /// <value>
        /// The command.
        /// </value>
        [NotNull]
        public string Command { get; }

        /// <summary>
        /// Gets the Overall Event Precision.
        /// </summary>
        /// <value>
        /// The event precision.
        /// </value>
        public double EventPrecision { get; }

        /// <summary>
        /// Gets the Overall Event Recall.
        /// </summary>
        /// <value>
        /// The event recall.
        /// </value>
        public double EventRecall { get; }

        /// <summary>
        /// Gets the Overall Event F-score.
        /// </summary>
        /// <value>
        /// The event F-score.
        /// </value>
        public double EventFscore { get; }

        /// <summary>
        /// Gets the stats per <see cref="ISamplePair"/>.
        /// </summary>
        /// <value>
        /// The per sample stats.
        /// </value>
        [NotNull]
        public IReadOnlyList<SampleStats> PerSampleStats { get; }

        [JsonConstructor]
        private GeneralStats([CanBeNull] string cmd, [NotNull] IReadOnlyList<SampleStats> perSampleStats, double precision, double recall, double fscore)
        {
            Command = cmd ?? string.Empty;
            PerSampleStats = perSampleStats;
            EventPrecision = precision;
            EventRecall = recall;
            EventFscore = fscore;
        }

        /// <summary>
        /// Creates the <see cref="GeneralStats"/> given the required objects.
        /// </summary>
        /// <param name="items">The items.</param>
        /// <param name="isGenotypeEvaluated">if set to <c>true</c> [is genotype evaluated].</param>
        /// <param name="inputSpecs">The input specs.</param>
        /// <param name="cmd">The command.</param>
        /// <returns></returns>
        [NotNull]
        public static GeneralStats Create(
            [NotNull] IEnumerable<(ISamplePair samplePair, IWittyerResult query, IWittyerResult truth)> items,
            bool isGenotypeEvaluated, [NotNull] IReadOnlyDictionary<WittyerType, InputSpec> inputSpecs,
            [CanBeNull] string cmd)
        {
            var benchmarkStats = items.Select(tuple => MainLauncher.GenerateSampleMetrics(tuple.truth,
                    tuple.query, isGenotypeEvaluated, inputSpecs));
            return Create(benchmarkStats, cmd);
        }

        [NotNull]
        internal static GeneralStats Create([NotNull] IEnumerable<SampleMetrics> benchmarkResults, [CanBeNull] string cmd)
        {
            var perSampleStats = benchmarkResults.Select(SampleStats.Create).ToList();

            uint truthTp = 0, truthFn = 0, queryTp = 0, queryFp = 0;
            foreach (var sample in perSampleStats)
            {
                var perSampleEventStats = sample.OverallStats.Single(x => x.StatsType.Equals(StatsType.Event));
                truthTp += perSampleEventStats.TruthTpCount;
                truthFn += perSampleEventStats.TruthFnCount;
                queryTp += perSampleEventStats.QueryTpCount;
                queryFp += perSampleEventStats.QueryFpCount;
            }

            var stats = StatsUnit.Create(BasicStatsCount.Create(truthTp, truthFn), BasicStatsCount.Create(queryTp, queryFp));
            return new GeneralStats(cmd, perSampleStats.AsReadOnly(), stats.GetPrecision(), stats.GetRecall(), stats.GetFscore());
        }
    }
}