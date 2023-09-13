using System.Collections.Immutable;
using System.IO;
using System.Linq;
using Ilmn.Das.App.Wittyer.Infrastructure;
using Ilmn.Das.App.Wittyer.Input;
using Ilmn.Das.App.Wittyer.Stats;
using Ilmn.Das.App.Wittyer.Vcf.Variants;
using Ilmn.Das.Core.Tries.Extensions;
using Ilmn.Das.Std.AppUtils.Misc;
using Ilmn.Das.Std.XunitUtils;
using Xunit;

namespace Ilmn.Das.App.Wittyer.Test
{
    public class Wit173
    {
        private static readonly FileInfo Truth =
            Path.Combine("Resources", "WIT-173", "truth.vcf").ToFileInfo();

        private static readonly FileInfo Query =
            Path.Combine("Resources", "WIT-173", "query.vcf").ToFileInfo();

        [Fact]
        public void MaxMatches_1_Works()
        {
            var outputDirectory = Path.GetRandomFileName().ToDirectoryInfo();
            var inputSpecs = InputSpec.GenerateDefaultInputSpecs(false)
                .ToDictionary(i => i.VariantType, i => i);
            var wittyerSettings = WittyerSettings.Create(outputDirectory, Truth, Query,
                ImmutableList<ISamplePair>.Empty, EvaluationMode.GenotypeMatching,
                inputSpecs, maxMatches: 1);

            var (_, query, truth) = MainLauncher.GenerateResults(wittyerSettings)
                .EnumerateSuccesses().First();
            var results = MainLauncher
                .GenerateSampleMetrics(truth, query, true, inputSpecs, true);
            var baseStats = results.DetailedStats[Category.Create(WittyerType.Insertion)].OverallStats[StatsType.Event];
            MultiAssert.Equal(0U, baseStats.QueryStats.TrueCount);
            MultiAssert.Equal(0U, baseStats.TruthStats.TrueCount);
            MultiAssert.Equal(1U, baseStats.QueryStats.FalseCount);
            MultiAssert.Equal(2U, baseStats.TruthStats.FalseCount);
            MultiAssert.AssertAll();
        }

        [Fact]
        public void MaxMatches_Zero_Works()
        {
            var outputDirectory = Path.GetRandomFileName().ToDirectoryInfo();
            var inputSpecs = InputSpec.GenerateDefaultInputSpecs(false)
                .ToDictionary(i => i.VariantType, i => i);
            var wittyerSettings = WittyerSettings.Create(outputDirectory, Truth, Query,
                ImmutableList<ISamplePair>.Empty, EvaluationMode.GenotypeMatching,
                inputSpecs, maxMatches: 0);

            var (_, query, truth) = MainLauncher.GenerateResults(wittyerSettings)
                .EnumerateSuccesses().First();
            var results = MainLauncher
                .GenerateSampleMetrics(truth, query, false, inputSpecs, true);
            var baseStats = results.DetailedStats[Category.Create(WittyerType.Insertion)].OverallStats[StatsType.Event];
            MultiAssert.Equal(0U, baseStats.QueryStats.TrueCount);
            MultiAssert.Equal(0U, baseStats.TruthStats.TrueCount);
            MultiAssert.Equal(1U, baseStats.QueryStats.FalseCount);
            MultiAssert.Equal(2U, baseStats.TruthStats.FalseCount);
            MultiAssert.AssertAll();
        }

        [Fact]
        public void MaxMatches_Null_Works()
        {
            var outputDirectory = Path.GetRandomFileName().ToDirectoryInfo();
            var inputSpecs = InputSpec.GenerateDefaultInputSpecs(false)
                .ToDictionary(i => i.VariantType, i => i);
            var wittyerSettings = WittyerSettings.Create(outputDirectory, Truth, Query,
                ImmutableList<ISamplePair>.Empty, EvaluationMode.GenotypeMatching,
                inputSpecs, maxMatches: null);

            var (_, query, truth) = MainLauncher.GenerateResults(wittyerSettings)
                .EnumerateSuccesses().First();
            var results = MainLauncher
                .GenerateSampleMetrics(truth, query, false, inputSpecs, true);
            var baseStats = results.DetailedStats[Category.Create(WittyerType.Insertion)].OverallStats[StatsType.Event];
            MultiAssert.Equal(1U, baseStats.QueryStats.TrueCount);
            MultiAssert.Equal(2U, baseStats.TruthStats.TrueCount);
            MultiAssert.Equal(0U, baseStats.QueryStats.FalseCount);
            MultiAssert.Equal(0U, baseStats.TruthStats.FalseCount);
            MultiAssert.AssertAll();
        }
    }
}