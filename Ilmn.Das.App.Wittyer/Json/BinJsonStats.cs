using System;
using System.Collections.Generic;
using Ilmn.Das.App.Wittyer.Infrastructure;
using Ilmn.Das.App.Wittyer.Stats;
using Ilmn.Das.App.Wittyer.Utilities;
using Ilmn.Das.App.Wittyer.Vcf.Variants;
using Ilmn.Das.Std.AppUtils.Collections;
using Ilmn.Das.Std.AppUtils.Comparers;
using Newtonsoft.Json;

namespace Ilmn.Das.App.Wittyer.Json
{
    /// <inheritdoc />
    /// <summary>
    /// Binned Json stats
    /// </summary>
    /// <seealso cref="T:System.IEquatable`1" />
    public class BinJsonStats : IEquatable<BinJsonStats>
    {
        /// <summary>
        /// Gets the bin.
        /// </summary>
        /// <value>
        /// The bin.
        /// </value>
        public string Bin { get; }

        /// <summary>
        /// Gets the stats.
        /// </summary>
        /// <value>
        /// The stats.
        /// </value>
        public IEnumerable<BasicJsonStats> Stats { get; }

        [JsonConstructor]
        private BinJsonStats(string bin, IEnumerable<BasicJsonStats> stats)
        {
            Bin = bin;
            Stats = stats;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="BinJsonStats"/> class using the given parameters.
        /// </summary>
        public static BinJsonStats Create(IPerBinStats binnedStats, string nextBin, PrimaryCategory variantType)
        {
            var result = new List<BasicJsonStats>();
            var eventStats = binnedStats.Stats[StatsType.Event];
            var eventBasicStats = BasicJsonStats.Create(StatsType.Event, eventStats.TruthStats.TrueCount,
                eventStats.TruthStats.FalseCount, eventStats.QueryStats.TrueCount,
                eventStats.QueryStats.FalseCount);

            result.Add(eventBasicStats);

            if (!binnedStats.Stats.TryGetValue(StatsType.Base, out var beb))
                return new BinJsonStats(GenerateBinString(binnedStats.Bin, nextBin, variantType), result);

            var baseBasicStats = BasicJsonStats.Create(StatsType.Base, beb.TruthStats.TrueCount,
                beb.TruthStats.FalseCount, beb.QueryStats.TrueCount, beb.QueryStats.FalseCount);

            result.Add(baseBasicStats);

            return new BinJsonStats(GenerateBinString(binnedStats.Bin, nextBin, variantType), result);
        }

        private static string GenerateBinString(uint? currentBin, string nextBin, PrimaryCategory variantType)
            => currentBin == null || variantType.Is(WittyerType.TranslocationBreakend)
                ? "NA"
                : nextBin.Equals(WittyerConstants.Json.InfiniteBin)
                    ? currentBin + nextBin
                    : $"[{currentBin}, {nextBin})";

        #region Equality members

        /// <inheritdoc />
        public bool Equals(BinJsonStats? other)
        {
            if (other is null) return false;
            if (ReferenceEquals(this, other)) return true;
            return string.Equals(Bin, other.Bin) && Stats.IsScrambledEquals(other.Stats);
        }

        /// <inheritdoc />
        public override bool Equals(object? obj)
        {
            if (obj is null) return false;
            if (ReferenceEquals(this, obj)) return true;
            return obj is BinJsonStats cast && Equals(cast);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            unchecked
            {
                return (Bin.GetHashCode() * 397) ^ HashCodeUtils.GenerateForEnumerables(Stats, false);
            }
        }

        /// <summary>
        /// Implements the operator ==.
        /// </summary>
        /// <param name="left">The left.</param>
        /// <param name="right">The right.</param>
        /// <returns>
        /// The result of the operator.
        /// </returns>
        public static bool operator ==(BinJsonStats? left, BinJsonStats? right) => Equals(left, right);

        /// <summary>
        /// Implements the operator !=.
        /// </summary>
        /// <param name="left">The left.</param>
        /// <param name="right">The right.</param>
        /// <returns>
        /// The result of the operator.
        /// </returns>
        public static bool operator !=(BinJsonStats? left, BinJsonStats? right) => !Equals(left, right);

        #endregion
    }
}