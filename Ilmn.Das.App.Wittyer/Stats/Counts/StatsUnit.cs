namespace Ilmn.Das.App.Wittyer.Stats.Counts
{
    public interface IStatsUnit
    {
        IBasicStatsCount TruthStats { get; }

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

        public static IStatsUnit Create(IBasicStatsCount truthStats, IBasicStatsCount queryStats) 
            => new StatsUnit(truthStats, queryStats);
    }
}
