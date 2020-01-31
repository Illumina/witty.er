using System;
using System.IO;
using System.Linq;
using Ilmn.Das.App.Wittyer.Infrastructure;
using Ilmn.Das.App.Wittyer.Input;
using Ilmn.Das.App.Wittyer.Stats;
using Newtonsoft.Json;

namespace Wittyer
{
    public class Program
    {
        public static void Main(string[] args)
        {
            if (args.Length == 0)
                LaunchWittyerMain(new[] {"-h"});

            if (args.Length == 1 && args[0] == "logo")
            {
                Console.WriteLine(File.ReadAllText("Logo.txt"));
                Environment.Exit(0);
            }

            LaunchWittyerMain(args);
        }

        private static void LaunchWittyerMain(string[] args)
        {
            var settings = WittyerSettings.Parser.Parse(args);

            var cmd = Environment.CommandLine;
            var result = MainLauncher.GenerateJson(settings, cmd);

            using (var sw = new StreamWriter(Path.Combine(settings.OutputDirectory.FullName, "Wittyer.Stats.json")))
                sw.Write(JsonConvert.SerializeObject(result, Formatting.Indented));

            Console.WriteLine("Overall Stats:");
            Console.WriteLine($"Overall EventPrecision: {result.EventPrecision:P3}");
            Console.WriteLine($"Overall EventRecall: {result.EventRecall:P3}");
            Console.WriteLine("--------------------------------");
            Console.WriteLine("QuerySample\tTruthSample\tQueryTotal\tQueryTp\tQueryFp\tPrecision\tTruthTotal\tTruthTp\tTruthFn\tRecall\t" +
                              "BaseQueryTotal\tBaseQueryTp\tBaseQueryFp\tBasePrecision\tBaseTruthTotal\tBaseTruthTp\tBaseTruthFn\tBaseRecall\n");
            foreach (var stats in result.PerSampleStats)
            {
                var overallEventStats = stats.OverallStats.Single(x => x.StatsType.Equals(StatsType.Event));
                var overallBaseStats = stats.OverallStats.Single(x => x.StatsType.Equals(StatsType.Base));

                Console.WriteLine(
                    $"{stats.QuerySampleName}\t{stats.TruthSampleName}\t{overallEventStats.QueryTotalCount}\t{overallEventStats.QueryTpCount}\t" +
                    $"{overallEventStats.QueryFpCount}\t{overallEventStats.Precision:P3}\t{overallEventStats.TruthTotalCount}\t" +
                    $"{overallEventStats.TruthTpCount}\t{overallEventStats.TruthFnCount}\t{overallEventStats.Recall:P3}\t" +
                    $"{overallBaseStats.QueryTotalCount}\t{overallBaseStats.QueryTpCount}\t" +
                    $"{overallBaseStats.QueryFpCount}\t{overallBaseStats.Precision:P3}\t{overallBaseStats.TruthTotalCount}\t" +
                    $"{overallBaseStats.TruthTpCount}\t{overallBaseStats.TruthFnCount}\t{overallBaseStats.Recall:P3}");
            }

        }
    }
}