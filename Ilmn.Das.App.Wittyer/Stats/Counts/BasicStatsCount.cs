using System;
using System.Collections.Generic;
using Ilmn.Das.App.Wittyer.Utilities;
using Ilmn.Das.Std.AppUtils.Intervals;
using Ilmn.Das.Std.BioinformaticUtils.Contigs;
using JetBrains.Annotations;

namespace Ilmn.Das.App.Wittyer.Stats.Counts
{
    public interface IBasicStatsCount : IEquatable<IBasicStatsCount>
    {
        uint TrueCount { get; }

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

        public static IBasicStatsCount Create(uint trueCount, uint falseCount) 
            => new BasicStatsCount(trueCount, falseCount);

        public static IBasicStatsCount Create(
            IReadOnlyDictionary<IContigInfo, IIntervalTree<uint, IInterval<uint>>> trueTrees,
            IReadOnlyDictionary<IContigInfo, IIntervalTree<uint, IInterval<uint>>> falseTrees)
            => Create(trueTrees.GetTotalLength(), falseTrees.GetTotalLength());

        [NotNull, Pure]
        public static BasicStatsCount operator +([NotNull] BasicStatsCount a, [NotNull] BasicStatsCount b)
            => new BasicStatsCount(a.TrueCount + b.TrueCount, a.FalseCount + b.FalseCount);


        public bool Equals(IBasicStatsCount other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return TrueCount == other.TrueCount && FalseCount == other.FalseCount;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((BasicStatsCount) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((int) TrueCount * 397) ^ (int) FalseCount;
            }
        }
    }
}
