using JetBrains.Annotations;

namespace Ilmn.Das.App.Wittyer.Stats.Counts
{
    /// <summary>
    /// A unit of stats, includes Truth and Query
    /// </summary>
    public interface IStatsUnit
    {
        /// <summary>
        /// Gets the truth stats.
        /// </summary>
        /// <value>
        /// The truth stats.
        /// </value>
        IBasicStatsCount TruthStats { get; }

        /// <summary>
        /// Gets the query stats.
        /// </summary>
        /// <value>
        /// The query stats.
        /// </value>
        IBasicStatsCount QueryStats { get; }
    }

    internal class StatsUnit : IStatsUnit
    {
        public IBasicStatsCount TruthStats { get; }
        public IBasicStatsCount QueryStats { get; }

        private StatsUnit(IBasicStatsCount truthStats, IBasicStatsCount queryStats)
        {
            TruthStats = truthStats;
            QueryStats = queryStats;
        }

        [NotNull]
        public static IStatsUnit Create(IBasicStatsCount truthStats, IBasicStatsCount queryStats) 
            => new StatsUnit(truthStats, queryStats);
    }
}
