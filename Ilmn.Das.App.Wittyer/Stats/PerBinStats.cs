using System.Collections.Generic;
using Ilmn.Das.App.Wittyer.Stats.Counts;
using JetBrains.Annotations;

namespace Ilmn.Das.App.Wittyer.Stats
{
    public interface IPerBinStats
    {
        uint Bin { get; }

        [NotNull] IReadOnlyDictionary<StatsType, IStatsUnit> Stats { get; }
    }

    public class PerBinStats : IPerBinStats
    {
        private PerBinStats(uint bin, [NotNull] IReadOnlyDictionary<StatsType, IStatsUnit> stats)
        {
            Bin = bin;
            Stats = stats;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="PerBinStats"/> class.
        /// </summary>
        /// <param name="bin">The bin.</param>
        /// <param name="stats">The stats.</param>
        [NotNull]
        [Pure]
        public static IPerBinStats Create(uint bin, [NotNull] IReadOnlyDictionary<StatsType, IStatsUnit> stats)
            => new PerBinStats(bin, stats);

        #region Implementation of IBinnedStats

        /// <inheritdoc />
        public uint Bin { get; }

        /// <inheritdoc />
        public IReadOnlyDictionary<StatsType, IStatsUnit> Stats { get; }

        #endregion
    }
}