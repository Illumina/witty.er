using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using Ilmn.Das.App.Wittyer.Stats.Counts;
using Ilmn.Das.Std.AppUtils.Collections;
using JetBrains.Annotations;

namespace Ilmn.Das.App.Wittyer.Stats
{
    public interface IBenchmarkMetrics<out T>
    {
        T Category { get; }

        IReadOnlyDictionary<StatsType, IStatsUnit> OverallStats { get; }

        IReadOnlyList<IPerBinStats> BinnedStats { get; }
    }

    public class BenchmarkMetrics<T> : IBenchmarkMetrics<T>
    {
        /// <inheritdoc />
        public T Category { get; }

        public IImmutableDictionary<StatsType, IStatsUnit> OverallStats { get; }

        /// <inheritdoc />
        IReadOnlyDictionary<StatsType, IStatsUnit> IBenchmarkMetrics<T>.OverallStats => OverallStats;

        /// <inheritdoc />
        public IReadOnlyList<IPerBinStats> BinnedStats { get; }

        private BenchmarkMetrics(T category, IImmutableDictionary<StatsType, IStatsUnit> overallStats,
            IReadOnlyList<IPerBinStats> binnedStats)
        {
            Category = category;
            BinnedStats = binnedStats;
            OverallStats = overallStats;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="BenchmarkMetrics{T}"/> class.
        /// </summary>
        /// <param name="category">The category.</param>
        /// <param name="overallStats">The overall stats.</param>
        /// <param name="binnedStats">The binned stats.</param>
        [NotNull]
        [Pure]
        public static IBenchmarkMetrics<T> Create(T category, IImmutableDictionary<StatsType, IStatsUnit> overallStats,
            IReadOnlyList<IPerBinStats> binnedStats)
            => new BenchmarkMetrics<T>(category, overallStats, binnedStats);

        [NotNull]
        internal static IBenchmarkMetrics<T> Create(T category,
            [NotNull] IImmutableDictionary<StatsType, IStatsUnit> overallBaseStats,
            [NotNull] BinnedDictionary binnedDictionary)
        {
            var binnedStats = new List<IPerBinStats>();
            uint perTypeTruthTp = 0, perTypeTruthFp = 0, perTypeQueryTp = 0, perTypeQueryFp = 0;

            foreach (var (bin, stats) in binnedDictionary)
            {
                var statsDict = ImmutableDictionary<StatsType, IStatsUnit>.Empty;
                if (stats is MutableEventStats me)
                {
                    binnedStats.Add(PerBinStats.Create(bin, statsDict.Add(StatsType.Event,
                        StatsUnit.Create(me.TruthStats, me.QueryStats))));
                    perTypeTruthTp += me.TruthStats.TrueCount;
                    perTypeTruthFp += me.TruthStats.FalseCount;
                    perTypeQueryTp += me.QueryStats.TrueCount;
                    perTypeQueryFp += me.QueryStats.FalseCount;
                }
                else if (stats is MutableEventAndBasesStats meb)
                {
                    binnedStats.Add(PerBinStats.Create(bin, statsDict.Add(StatsType.Event,
                        StatsUnit.Create(meb.TruthStats, meb.QueryStats)).Add(StatsType.Base,
                        StatsUnit.Create(BasicStatsCount.Create(meb.TruthBaseStats.TrueCount,
                                meb.TruthBaseStats.FalseCount),
                            BasicStatsCount.Create(meb.QueryBaseStats.TrueCount, meb.QueryBaseStats.FalseCount)))));
                    perTypeTruthTp += meb.TruthStats.TrueCount;
                    perTypeTruthFp += meb.TruthStats.FalseCount;
                    perTypeQueryTp += meb.QueryStats.TrueCount;
                    perTypeQueryFp += meb.QueryStats.FalseCount;
                }
                else
                    throw new InvalidDataException(
                        $"Found a type {bin.GetType().Name} which cannot be converted to {typeof(BenchmarkMetrics<T>)}");
            }

            return new BenchmarkMetrics<T>(category, overallBaseStats.Add(StatsType.Event,
                StatsUnit.Create(BasicStatsCount.Create(perTypeTruthTp, perTypeTruthFp),
                    BasicStatsCount.Create(perTypeQueryTp, perTypeQueryFp))), binnedStats.ToReadOnlyList());
        }
    }
}