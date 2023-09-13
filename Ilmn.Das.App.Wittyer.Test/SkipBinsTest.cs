using System.Collections.Immutable;
using System.IO;
using System.Linq;
using Ilmn.Das.App.Wittyer.Infrastructure;
using Ilmn.Das.App.Wittyer.Input;
using Ilmn.Das.App.Wittyer.Stats;
using Ilmn.Das.App.Wittyer.Utilities.Enums;
using Ilmn.Das.App.Wittyer.Vcf.Variants;
using Ilmn.Das.Core.Tries.Extensions;
using Ilmn.Das.Std.AppUtils.Collections;
using Ilmn.Das.Std.AppUtils.Misc;
using Ilmn.Das.Std.XunitUtils;
using Xunit;

namespace Ilmn.Das.App.Wittyer.Test
{
    public class SkipBinsTest
    {
        private static readonly FileInfo TinyTruth =
            Path.Combine("Resources", "Tiny", "truth.vcf").ToFileInfo();

        private static readonly FileInfo TinyQuery =
            Path.Combine("Resources", "Tiny", "query.vcf").ToFileInfo();
        
        [Fact]
        public void SkippedBinsAreIgnoredInStats()
        {
            var outputDirectory = Path.GetRandomFileName().ToDirectoryInfo();
            const uint firstKeptBin = 100000U;
            const uint secondKeptBin = 300000U;
            var alternatingBins = ImmutableList<(uint size, bool skip)>.Empty
                .Add((1, true))
                .Add((firstKeptBin, false))
                .Add((200000, true))
                .Add((secondKeptBin, false));

            var inputSpecs = InputSpec.GenerateDefaultInputSpecs(false)
                .Select(i => InputSpec.Create(i.VariantType, alternatingBins,
                    1, 1, i.ExcludedFilters, i.IncludedFilters, i.IncludedRegions))
                .ToDictionary(i => i.VariantType, i => i);
            var wittyerSettings = WittyerSettings.Create(outputDirectory, TinyTruth, TinyQuery,
                ImmutableList<ISamplePair>.Empty, EvaluationMode.SimpleCounting,
                inputSpecs);

            var (_, query, truth) = MainLauncher.GenerateResults(wittyerSettings).EnumerateSuccesses().First();
            var results = MainLauncher.GenerateSampleMetrics(truth, query, false, inputSpecs, false);

            MultiAssert.Equal(2U, results.OverallStats[StatsType.Event].QueryStats.TrueCount);
            MultiAssert.Equal(1U, results.OverallStats[StatsType.Event].QueryStats.FalseCount);
            MultiAssert.Equal(0.6666666666666666,
                results.EventLevelRecallOverall.First(typeRecallTuple =>
                    typeRecallTuple.type.Is(WittyerType.CopyNumberGain)).recall);

            var bins = results.EventLevelRecallPerBin.First().perBinRecall
                .Select(it => it.binStart).Where(it => it != null).ToList();
            MultiAssert.Equal(firstKeptBin.FollowedBy(secondKeptBin).StringJoin(","), bins.StringJoin(","));
            MultiAssert.AssertAll();
        }
    }
}