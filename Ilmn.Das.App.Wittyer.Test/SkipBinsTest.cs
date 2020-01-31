using System.Collections.Immutable;
using System.IO;
using System.Linq;
using Ilmn.Das.App.Wittyer.Infrastructure;
using Ilmn.Das.App.Wittyer.Input;
using Ilmn.Das.App.Wittyer.Stats;
using Ilmn.Das.App.Wittyer.Vcf.Variants;
using Ilmn.Das.Core.Tries.Extensions;
using Ilmn.Das.Std.AppUtils.Misc;
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
            var alternatingBins = ImmutableList<(uint size, bool skip)>.Empty
                .Add((1, true))
                .Add((100000, false))
                .Add((200000, true))
                .Add((300000, false));

            var inputSpecs = InputSpec.GenerateDefaultInputSpecs(true)
                .Select(i => InputSpec.Create(i.VariantType, alternatingBins,
                    1, 1, i.ExcludedFilters, i.IncludedFilters, i.IncludedRegions))
                .ToDictionary(i => i.VariantType, i => i);
            var wittyerSettings = WittyerSettings.Create(outputDirectory, TinyTruth, TinyQuery,
                ImmutableList<ISamplePair>.Empty, EvaluationMode.SimpleCounting,
                inputSpecs);

            var (_, query, truth) = MainLauncher.GenerateResults(wittyerSettings).EnumerateSuccesses().First();
            var results = MainLauncher.GenerateSampleMetrics(truth, query, false, inputSpecs);

            Assert.Equal(1U, results.OverallStats[StatsType.Event].QueryStats.TrueCount);
            Assert.Equal(1U, results.OverallStats[StatsType.Event].QueryStats.FalseCount);
            Assert.Equal(0.5, results.EventLevelRecallOverall.First(typeRecallTuple => typeRecallTuple.type == WittyerType.CopyNumberGain).recall);

            var numberOfBinsReportedOn = results.EventLevelRecallPerBin.First().perBinRecall.Count();
            Assert.Equal(2, numberOfBinsReportedOn);
        }
    }
}