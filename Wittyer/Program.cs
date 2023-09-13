using System;
using System.IO;
using System.Linq;
using Ilmn.Das.App.Wittyer.Infrastructure;
using Ilmn.Das.App.Wittyer.Input;
using Ilmn.Das.App.Wittyer.Results;
using Ilmn.Das.App.Wittyer.Stats;
using Ilmn.Das.App.Wittyer.Utilities;
using Ilmn.Das.App.Wittyer.Utilities.Enums;
using Ilmn.Das.Core.BgZip;
using Ilmn.Das.Core.Tries.Extensions;
using Ilmn.Das.Std.AppUtils.Misc;
using Ilmn.Das.Std.BioinformaticUtils.Tabix;
using Newtonsoft.Json;

namespace Wittyer
{
    public class Program
    {
        public static int Main(string[] args)
        {
            if (args.Length == 0)
                LaunchWittyerMain(new[] {"-h"});

            if (args.Length == 1 && args[0] == "logo")
            {
                Console.WriteLine(File.ReadAllText("Logo.txt"));
                Environment.Exit(0);
            }

            try
            {
                LaunchWittyerMain(args);
            }
            catch (Exception e)
            {
                Console.Error.WriteLine("witty.er failed!");
                Console.Error.WriteLine(e.ToString());
                return -1;
            }

            return 0;
        }

        private static void LaunchWittyerMain(string[] args)
        {
            var settings = WittyerSettings.Parser.Parse(args);

            var outputDir = settings.OutputDirectory;
            if (!outputDir.ExistsNow())
                outputDir.Create();
            else if (outputDir.EnumerateFileSystemInfosSafe().Any())
            {
                Console.Error.WriteLine(
                    $"Specified a output directory that's not empty: {settings.OutputDirectory.FullName}\n, witty.er requires an empty and clean output folder!");
                Environment.Exit(1);
            }

            var cmd = Environment.CommandLine;
            var results = MainLauncher.GenerateResults(settings).Select(r =>
            {
                var tuple = r.GetOrThrow();
                var file = Path.Combine(settings.OutputDirectory.FullName, GenerateOutputFile(tuple.query, tuple.truth))
                    .ToFileInfo();
                MainLauncher.GenerateMergedVcfStrings(tuple.query, tuple.truth, cmd).WriteToCompressedFile(
                    file);
                var _ = file.TryEnsureTabixed().GetOrDefault();
                return tuple;
            });
            var resultsPerMatchEnum = MainLauncher.GenerateJson(settings, results, cmd);

            var mainKey = (Length: MatchEnum.Allele, settings.Mode == EvaluationMode.GenotypeMatching);
            foreach (var (whatT, r) in resultsPerMatchEnum)
            {
                var txt = "";
                if (whatT != mainKey)
                {
                    txt = $".{whatT.what}";
                    if (whatT.isGenotypeEvaluated)
                        txt += $"+{MatchEnum.Genotype}";
                }
                using var sw = new StreamWriter(Path.Combine(settings.OutputDirectory.FullName, $"Wittyer{txt}.Stats.json"));
                sw.Write(JsonConvert.SerializeObject(r, Formatting.Indented));
            }

            var result = resultsPerMatchEnum[mainKey]; 

            using (var sw =
                   new StreamWriter(Path.Combine(settings.OutputDirectory.FullName, "Wittyer.ConfigFileUsed.json")))
                sw.Write(settings.InputSpecs.Values.SerializeToString());

            Console.WriteLine("--------------------------------");
            Console.WriteLine("Overall Stats:");
            Console.WriteLine($"Overall EventPrecision: {result.EventPrecision:P3}");
            Console.WriteLine($"Overall EventRecall: {result.EventRecall:P3}");
            Console.WriteLine($"Overall EventFscore: {result.EventFscore:P3}");
            if (settings.SimilarityThreshold > 0.0)
            {
                var resultSeq =
                    resultsPerMatchEnum[(MatchEnum.Sequence, settings.Mode == EvaluationMode.GenotypeMatching)];
                if (Math.Abs(resultSeq.EventPrecision - result.EventPrecision) > 0.000005 ||
                    Math.Abs(resultSeq.EventRecall - result.EventRecall) > 0.000005)
                {
                    Console.WriteLine("--------------------------------");
                    Console.WriteLine("Overall Stats with similarityThreshold:");
                    Console.WriteLine(
                        $"Overall EventPrecision with similarityThreshold: {resultSeq.EventPrecision:P3}");
                    Console.WriteLine($"Overall EventRecall with similarityThreshold: {resultSeq.EventRecall:P3}");
                    Console.WriteLine($"Overall EventFscore with similarityThreshold: {resultSeq.EventFscore:P3}");
                    Console.WriteLine(
                        $"For more detailed stats with similarityThreshold, check the {MatchEnum.Sequence} and {MatchEnum.PartialSequence} and {MatchEnum.Length} Wittyer.Stats.jsons.");
                }
            }

            Console.WriteLine("--------------------------------");
            Console.WriteLine(
                "QuerySample\tTruthSample\tQueryTotal\tQueryTp\tQueryFp\tPrecision\tTruthTotal\tTruthTp\tTruthFn\tRecall\tFscore\t" +
                "BaseQueryTotal\tBaseQueryTp\tBaseQueryFp\tBasePrecision\tBaseTruthTotal\tBaseTruthTp\tBaseTruthFn\tBaseRecall\tBaseFscore\n");
            foreach (var stats in result.PerSampleStats)
            {
                var overallEventStats = stats.OverallStats.Single(x => x.StatsType.Equals(StatsType.Event));
                var overallBaseStats = stats.OverallStats.Single(x => x.StatsType.Equals(StatsType.Base));

                Console.WriteLine(
                    $"{stats.QuerySampleName}\t{stats.TruthSampleName}\t{overallEventStats.QueryTotalCount}\t{overallEventStats.QueryTpCount}\t" +
                    $"{overallEventStats.QueryFpCount}\t{overallEventStats.Precision:P3}\t{overallEventStats.TruthTotalCount}\t" +
                    $"{overallEventStats.TruthTpCount}\t{overallEventStats.TruthFnCount}\t{overallEventStats.Recall:P3}\t" +
                    $"{overallEventStats.Fscore:P3}\t" +
                    $"{overallBaseStats.QueryTotalCount}\t{overallBaseStats.QueryTpCount}\t" +
                    $"{overallBaseStats.QueryFpCount}\t{overallBaseStats.Precision:P3}\t{overallBaseStats.TruthTotalCount}\t" +
                    $"{overallBaseStats.TruthTpCount}\t{overallBaseStats.TruthFnCount}\t{overallBaseStats.Recall:P3}\t" +
                    $"{overallBaseStats.Fscore:P3}");

                Console.WriteLine();
                Console.WriteLine();
                
                foreach (var type in stats.DetailedStats)
                {
                    Console.WriteLine("--------------------------------");
                    Console.WriteLine(type.VariantType);
                    Console.WriteLine("--------------------------------");
                    Console.WriteLine(
                        "QuerySample\tTruthSample\tQueryTotal\tQueryTp\tQueryFp\tPrecision\tTruthTotal\tTruthTp\tTruthFn\tRecall\tFscore\t");
                    overallEventStats = type.OverallStats.Single(x => x.StatsType.Equals(StatsType.Event));
                    Console.WriteLine(
                        $"{stats.QuerySampleName}\t{stats.TruthSampleName}\t{overallEventStats.QueryTotalCount}\t{overallEventStats.QueryTpCount}\t" +
                        $"{overallEventStats.QueryFpCount}\t{overallEventStats.Precision:P3}\t{overallEventStats.TruthTotalCount}\t" +
                        $"{overallEventStats.TruthTpCount}\t{overallEventStats.TruthFnCount}\t{overallEventStats.Recall:P3}\t" +
                        $"{overallEventStats.Fscore:P3}\t");
                    Console.WriteLine();
                    Console.WriteLine();
                }
            }

            return;

            string GenerateOutputFile(IWittyerResult queryResult, IWittyerResult truthResult)
                => "Wittyer." +
                   (truthResult == null
                       ? queryResult.SampleName
                       : queryResult == null
                           ? truthResult.SampleName
                           : $"{truthResult.SampleName}.Vs.{queryResult.SampleName}") +
                   WittyerConstants.VcfGzSuffix;
        }
    }
}