using System;
using System.Collections.Generic;
using Ilmn.Das.App.Wittyer.Stats;
using Ilmn.Das.App.Wittyer.Utilities;
using Ilmn.Das.App.Wittyer.Vcf.Variants;
using Ilmn.Das.Std.AppUtils.Collections;
using Ilmn.Das.Std.AppUtils.Comparers;
using JetBrains.Annotations;
using Newtonsoft.Json;

namespace Ilmn.Das.App.Wittyer.Json
{
    public class BinJsonStats : IEquatable<BinJsonStats>
    {
        [NotNull] public string Bin { get; }

        [NotNull, ItemNotNull] public IEnumerable<BasicJsonStats> Stats { get; }

        [JsonConstructor]
        private BinJsonStats([NotNull] string bin, [NotNull] IEnumerable<BasicJsonStats> stats)
        {
            Bin = bin;
            Stats = stats;
        }

        [NotNull]
        public static BinJsonStats Create([NotNull] IPerBinStats binnedStats, string nextBin, WittyerVariantType variantType)
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

        [NotNull]
        private static string GenerateBinString(uint currentBin, string nextBin, WittyerVariantType variantType)
            => variantType == WittyerVariantType.TranslocationBreakend
                ? "NA"
                : (nextBin.Equals(WittyerConstants.Json.InfinteBin)
                    ? currentBin + nextBin
                    : $"[{currentBin}, {nextBin})");

        #region Equality members

        /// <inheritdoc />
        public bool Equals([CanBeNull] BinJsonStats other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return string.Equals(Bin, other.Bin) && Stats.IsScrambledEquals(other.Stats);
        }

        /// <inheritdoc />
        public override bool Equals([CanBeNull] object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
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
        public static bool operator ==([CanBeNull] BinJsonStats left, [CanBeNull] BinJsonStats right) => Equals(left, right);

        /// <summary>
        /// Implements the operator !=.
        /// </summary>
        /// <param name="left">The left.</param>
        /// <param name="right">The right.</param>
        /// <returns>
        /// The result of the operator.
        /// </returns>
        public static bool operator !=([CanBeNull] BinJsonStats left, [CanBeNull] BinJsonStats right) => !Equals(left, right);

        #endregion
    }
}