using System;
using System.Collections.Generic;
using Ilmn.Das.App.Wittyer.Utilities;
using Ilmn.Das.Std.AppUtils.Intervals;
using Ilmn.Das.Std.BioinformaticUtils.Contigs;
using JetBrains.Annotations;

namespace Ilmn.Das.App.Wittyer.Stats.Counts
{
    /// <inheritdoc />
    /// <summary>
    /// The most basic of stats count units.
    /// </summary>
    /// <seealso cref="T:Ilmn.Das.App.Wittyer.Stats.Counts.IBasicStatsCount" />
    public interface IBasicStatsCount : IEquatable<IBasicStatsCount>
    {
        /// <summary>
        /// Gets the true count.
        /// </summary>
        /// <value>
        /// The true count.
        /// </value>
        uint TrueCount { get; }

        /// <summary>
        /// Gets the false count.
        /// </summary>
        /// <value>
        /// The false count.
        /// </value>
        uint FalseCount { get; }
    }


    internal class BasicStatsCount :  IBasicStatsCount
    {
        public uint TrueCount { get; }

        public uint FalseCount { get; }

        private BasicStatsCount(uint trueCount, uint falseCount)
        {
            TrueCount = trueCount;
            FalseCount = falseCount;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="BasicStatsCount"/> class.
        /// </summary>
        /// <param name="trueCount">The true count.</param>
        /// <param name="falseCount">The false count.</param>
        [NotNull]
        [Pure]
        public static IBasicStatsCount Create(uint trueCount, uint falseCount) 
            => new BasicStatsCount(trueCount, falseCount);

        [NotNull]
        internal static IBasicStatsCount Create(
            [NotNull] IReadOnlyDictionary<IContigInfo, IIntervalTree<uint, IInterval<uint>>> trueTrees,
            [NotNull] IReadOnlyDictionary<IContigInfo, IIntervalTree<uint, IInterval<uint>>> falseTrees)
            => Create(trueTrees.GetTotalLength(), falseTrees.GetTotalLength());

        /// <summary>
        /// Implements the operator +.
        /// </summary>
        /// <param name="a">a.</param>
        /// <param name="b">The b.</param>
        /// <returns>
        /// The result of the operator.
        /// </returns>
        [NotNull, Pure]
        public static BasicStatsCount operator +([NotNull] BasicStatsCount a, [NotNull] BasicStatsCount b)
            => new BasicStatsCount(a.TrueCount + b.TrueCount, a.FalseCount + b.FalseCount);

        /// <inheritdoc />
        public bool Equals([CanBeNull] IBasicStatsCount other)
        {
            if (other is null) return false;
            if (ReferenceEquals(this, other)) return true;
            return TrueCount == other.TrueCount && FalseCount == other.FalseCount;
        }

        /// <inheritdoc />
        public override bool Equals([CanBeNull] object obj) 
            => ReferenceEquals(this, obj) || obj is IBasicStatsCount cast && Equals(cast);

        /// <inheritdoc />
        public override int GetHashCode()
        {
            unchecked
            {
                return ((int) TrueCount * 397) ^ (int) FalseCount;
            }
        }
    }
}
