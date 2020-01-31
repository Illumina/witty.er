using System.Collections.Generic;
using Ilmn.Das.App.Wittyer.Stats.Counts;
using JetBrains.Annotations;

namespace Ilmn.Das.App.Wittyer.Stats
{
    /// <summary>
    /// Interface for a container for a bin size and accompanying event and base type stats.
    /// </summary>
    public interface IPerBinStats
    {
        /// <summary>
        /// The start of the bin the stats are for.
        /// </summary>
        uint Bin { get; }

        /// <summary>
        /// Whether this bin was marked to be skipped in the stats calculations.
        /// </summary>
        bool IgnoreForStatsCalculations { get; }

        /// <summary>
        /// Event and base type stats by stats type.
        /// </summary>
        [NotNull] IReadOnlyDictionary<StatsType, IStatsUnit> Stats { get; }
    }

    /// <summary>
    /// A container for a bin size and accompanying event and base type stats.
    /// </summary>
    public class PerBinStats : IPerBinStats
    {
        private PerBinStats(uint bin, bool skipped, [NotNull] IReadOnlyDictionary<StatsType, IStatsUnit> stats)
        {
            Bin = bin;
            IgnoreForStatsCalculations = skipped;
            Stats = stats;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="PerBinStats"/> class.
        /// </summary>
        /// <param name="bin">The bin.</param>
        /// /// <param name="skipped">Whether this bin was marked to be skipped in the stats calculations..</param>
        /// <param name="stats">The stats.</param>
        [NotNull]
        [Pure]
        public static IPerBinStats Create(uint bin, bool skipped, [NotNull] IReadOnlyDictionary<StatsType, IStatsUnit> stats)
            => new PerBinStats(bin, skipped, stats);

        #region Implementation of IBinnedStats

        /// <inheritdoc />
        public uint Bin { get; }


        /// <inheritdoc />
        public bool IgnoreForStatsCalculations { get; }

        /// <inheritdoc />
        public IReadOnlyDictionary<StatsType, IStatsUnit> Stats { get; }

        #endregion
    }
}