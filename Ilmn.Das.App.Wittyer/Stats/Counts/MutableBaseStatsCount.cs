using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Ilmn.Das.Std.AppUtils.Comparers;
using Ilmn.Das.Std.AppUtils.Intervals;
using Ilmn.Das.Std.BioinformaticUtils.Contigs;

namespace Ilmn.Das.App.Wittyer.Stats.Counts
{
    internal interface IMutableBaseStatsCount : IEquatable<IMutableBaseStatsCount>
    {
        IReadOnlyDictionary<IContigInfo, IIntervalTree<uint, IInterval<uint>>> TrueCount { get; }

        IReadOnlyDictionary<IContigInfo, IIntervalTree<uint, IInterval<uint>>> FalseCount { get; }

        void AddTrueCount(IContigInfo contig, IInterval<uint> interval);

        void AddFalseCount(IContigInfo contig, IInterval<uint> interval);
        
    }

    internal class MutableBaseStatsCount : IMutableBaseStatsCount
    {
        private MutableBaseStatsCount(ConcurrentDictionary<IContigInfo, IIntervalTree<uint, IInterval<uint>>> trueCount,
            ConcurrentDictionary<IContigInfo, IIntervalTree<uint, IInterval<uint>>> falseCount)
        {
            _trueCount = trueCount;
            _falseCount = falseCount;
        }

        internal static IMutableBaseStatsCount Create(ConcurrentDictionary<IContigInfo, IIntervalTree<uint, IInterval<uint>>> trueCount,
            ConcurrentDictionary<IContigInfo, IIntervalTree<uint, IInterval<uint>>> falseCount) 
            => new MutableBaseStatsCount(trueCount, falseCount);

        public static IMutableBaseStatsCount Create() 
            => Create(new ConcurrentDictionary<IContigInfo, IIntervalTree<uint, IInterval<uint>>>(), 
            new ConcurrentDictionary<IContigInfo, IIntervalTree<uint, IInterval<uint>>>());

        public bool Equals(IMutableBaseStatsCount other) 
            => TrueCount.Equals(other.TrueCount) && FalseCount.Equals(other.FalseCount);

        private readonly ConcurrentDictionary<IContigInfo, IIntervalTree<uint, IInterval<uint>>> _trueCount;

        public IReadOnlyDictionary<IContigInfo, IIntervalTree<uint, IInterval<uint>>> TrueCount => _trueCount;

        private readonly ConcurrentDictionary<IContigInfo, IIntervalTree<uint, IInterval<uint>>> _falseCount;

        public IReadOnlyDictionary<IContigInfo, IIntervalTree<uint, IInterval<uint>>> FalseCount => _falseCount;

        public void AddTrueCount(IContigInfo contig, IInterval<uint> interval) 
            => _trueCount.GetOrAdd(contig, _ => new IntervalTree<uint>()).Add(interval);

        public void AddFalseCount(IContigInfo contig, IInterval<uint> interval)
            => _falseCount.GetOrAdd(contig, _ => new IntervalTree<uint>()).Add(interval);

        public override int GetHashCode()
        {
            var hashCode = -882152508;
            hashCode = HashCodeUtils.AggregateAll(hashCode, HashCodeUtils.GenerateForKvps(TrueCount));
            hashCode = HashCodeUtils.AggregateAll(hashCode, HashCodeUtils.GenerateForKvps(FalseCount));
            return hashCode;
        }
    }
}
