using System;
using System.Collections.Generic;
using JetBrains.Annotations;

namespace Ilmn.Das.App.Wittyer.Stats.Counts
{
    internal interface IMutableStats : IEquatable<IMutableStats>
    {
        IMutableEventStatsCount TruthStats { get; }

        IMutableEventStatsCount QueryStats { get; }
    }

    internal class MutableEventStats : IMutableStats
    {
        private MutableEventStats(IMutableEventStatsCount query, IMutableEventStatsCount truth)
        {
            TruthStats = truth;
            QueryStats = query;
        }

        [NotNull]
        public static MutableEventStats Create(IMutableEventStatsCount query, IMutableEventStatsCount truth) 
            => new MutableEventStats(query, truth);

        [NotNull]
        public static MutableEventStats Create() 
            => Create(MutableEventStatsCount.Create(), MutableEventStatsCount.Create());

        public IMutableEventStatsCount TruthStats { get; }
        public IMutableEventStatsCount QueryStats { get; }

        public bool Equals([NotNull] IMutableStats other) =>
            Equals(TruthStats, other.TruthStats) && Equals(QueryStats, other.QueryStats);

        public override bool Equals([CanBeNull] object obj)
        {
            if (obj is null) return false;
            if (ReferenceEquals(this, obj)) return true;
            return obj is MutableEventStats cast && Equals(cast);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((TruthStats != null ? TruthStats.GetHashCode() : 0) * 397) ^
                       (QueryStats != null ? QueryStats.GetHashCode() : 0);
            }
        }
    }


    internal class MutableEventAndBasesStats : IMutableStats, IEquatable<MutableEventAndBasesStats>
    {
        private readonly IMutableStats _eventStatsCount;
        internal IMutableBaseStatsCount TruthBaseStats { get; }

        internal IMutableBaseStatsCount QueryBaseStats { get; }

        private MutableEventAndBasesStats(IMutableBaseStatsCount truthBaseCount, IMutableBaseStatsCount queryBaseCount,
            IMutableStats eventCount)
        {
            TruthBaseStats = truthBaseCount;
            QueryBaseStats = queryBaseCount;
            _eventStatsCount = eventCount;
        }

        [NotNull]
        internal static MutableEventAndBasesStats Create(IMutableBaseStatsCount truthBaseCount,
            IMutableBaseStatsCount queryBaseCount, IMutableStats eventCount)
            => new MutableEventAndBasesStats(truthBaseCount, queryBaseCount, eventCount);

        [NotNull]
        internal static MutableEventAndBasesStats Create()
            => Create(MutableBaseStatsCount.Create(), MutableBaseStatsCount.Create(),
                MutableEventStats.Create());


        public bool Equals(IMutableStats other) => _eventStatsCount.Equals(other);

        public bool Equals(MutableEventAndBasesStats other) 
            => _eventStatsCount.Equals(other) 
               && TruthBaseStats.Equals(other.TruthBaseStats) && QueryBaseStats.Equals(other.QueryBaseStats);

        public override bool Equals([CanBeNull] object obj)
        {
            if (obj is null) return false;
            if (ReferenceEquals(this, obj)) return true;
            return obj is MutableEventAndBasesStats cast && Equals(cast);
        }

        public override int GetHashCode()
        {
            var hashCode = 1134726403;
            hashCode = hashCode * -1521134295 + EqualityComparer<IMutableStats>.Default.GetHashCode(_eventStatsCount);
            hashCode = hashCode * -1521134295 + EqualityComparer<IMutableBaseStatsCount>.Default.GetHashCode(TruthBaseStats);
            hashCode = hashCode * -1521134295 + EqualityComparer<IMutableBaseStatsCount>.Default.GetHashCode(QueryBaseStats);
            return hashCode;
        }

        public IMutableEventStatsCount TruthStats => _eventStatsCount.TruthStats;
        public IMutableEventStatsCount QueryStats => _eventStatsCount.QueryStats;
    }
}