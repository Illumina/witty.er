using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Ilmn.Das.App.Wittyer.Infrastructure;
using Ilmn.Das.App.Wittyer.Input;
using Ilmn.Das.App.Wittyer.Results;
using Ilmn.Das.App.Wittyer.Stats;
using Ilmn.Das.Std.AppUtils.Collections;
using JetBrains.Annotations;
using Newtonsoft.Json;

namespace Ilmn.Das.App.Wittyer.Json
{
    public class GeneralStats
    {
        public string Command { get; }

        public double EventPrecision { get; }

        public double EventRecall { get; }

        public IReadOnlyList<SampleStats> PerSampleStats { get; }

        [JsonConstructor]
        private GeneralStats(string cmd, IReadOnlyList<SampleStats> perSampleStats, double precision, double recall)
        {
            Command = cmd;
            PerSampleStats = perSampleStats;
            EventPrecision = precision;
            EventRecall = recall;
        }

        [NotNull]
        public static GeneralStats Create(
            [NotNull] IReadOnlyDictionary<ISamplePair, (WittyerVcfResult query, WittyerVcfResult truth)> items, 
            IWittyerSettings settings, string cmd)
        {
            var benchmarkStats = items.ToDictionary(kvp => kvp.Key,
                kvp => Quantify.GenerateSampleStats(kvp.Value.truth,
                    kvp.Value.query, settings.Mode.Equals(EvaluationMode.Default), settings.InputSpecs)).ToImmutableDictionary();
            return Create(cmd, benchmarkStats);
        }
        
        [NotNull]
        internal static GeneralStats Create(string cmd, 
            [NotNull] IReadOnlyDictionary<ISamplePair, SampleMetrics> benchmarkResults)
        {
            var perSampleStats = benchmarkResults.Select(kvp => SampleStats.Create(kvp.Value)).ToList();

            uint truthTp = 0, truthFn = 0, queryTp = 0, queryFp = 0;
            foreach (var sample in perSampleStats)
            {
                var perSampleEventStats = sample.OverallStats.Single(x => x.StatsType.Equals(StatsType.Event));
                truthTp += perSampleEventStats.TruthTpCount;
                truthFn += perSampleEventStats.TruthFnCount;
                queryTp += perSampleEventStats.QueryTpCount;
                queryFp += perSampleEventStats.QueryFpCount;
            }

            var precision = (double) queryTp / (queryFp + queryTp);
            var recall = (double) truthTp / (truthTp + truthFn);
            return new GeneralStats(cmd, perSampleStats.ToReadOnlyList(), precision, recall);
        }
    }
}
