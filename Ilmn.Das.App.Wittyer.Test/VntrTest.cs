using System.Collections.Immutable;
using System.IO;
using System.Linq;
using Ilmn.Das.App.Wittyer.Infrastructure;
using Ilmn.Das.App.Wittyer.Input;
using Ilmn.Das.App.Wittyer.Stats;
using Ilmn.Das.App.Wittyer.Utilities.Enums;
using Ilmn.Das.App.Wittyer.Vcf.Variants;
using Ilmn.Das.Core.Tries.Extensions;
using Ilmn.Das.Std.AppUtils.Misc;
using Ilmn.Das.Std.XunitUtils;
using Xunit;

namespace Ilmn.Das.App.Wittyer.Test
{
    public class VntrTest
    {
        private static readonly FileInfo Truth =
            Path.Combine("Resources", "VNTR", "catalog1.vcf").ToFileInfo();

        private static readonly FileInfo Query =
            Path.Combine("Resources", "VNTR", "catalog2.vcf").ToFileInfo();

        private static readonly FileInfo Bed =
            Path.Combine("Resources", "VNTR", "HG002.delphi0.1.GRCh38.TandemRepeatRegions.bed.gz").ToFileInfo();

        [Fact]
        public void VntrVsTruth_CrossType_Works()
        {
            var outputDirectory = Path.GetRandomFileName().ToDirectoryInfo();
            var inputSpecs = InputSpec.GenerateDefaultInputSpecs(true).Select(i => InputSpec.Create(i.VariantType, i.BinSizes,
                    i.BasepairDistance, i.PercentThreshold, i.ExcludedFilters, i.IncludedFilters, IncludeBedFile.CreateFromBedFile(Bed)))
                .ToDictionary(i => i.VariantType, i => i);
            var wittyerSettings = WittyerSettings.Create(outputDirectory,
                Path.Combine("Resources", "VNTR", "HG002.delphi0.1.GRCh38.SV.vcf.gz").ToFileInfo(), 
                Path.Combine("Resources", "VNTR", "HG002.delphi0.1.GRCh38.VNTR.vcf.gz").ToFileInfo(),
                ImmutableList<ISamplePair>.Empty, EvaluationMode.CrossTypeAndSimpleCounting,
                inputSpecs);

            var (_, query, truth) = MainLauncher.GenerateResults(wittyerSettings)
                .EnumerateSuccesses().First();
            var results = MainLauncher
                .GenerateSampleMetrics(truth, query, false, inputSpecs, true);
            var cnTrStats = results.DetailedStats[Category.Create(WittyerType.CopyNumberTandemRepeat)].OverallStats[StatsType.Event];
            // entries have been spot checked so these stats are assumed to be correct
            MultiAssert.Equal(145U, cnTrStats.QueryStats.FalseCount);
            MultiAssert.Equal(272U, cnTrStats.QueryStats.TrueCount);
            MultiAssert.Equal(0U, cnTrStats.TruthStats.TrueCount);
            MultiAssert.Equal(0U, cnTrStats.TruthStats.FalseCount);
            var insertionStats = results.DetailedStats[Category.Create(WittyerType.Insertion)].OverallStats[StatsType.Event];
            MultiAssert.Equal(206U, insertionStats.TruthStats.TrueCount);
            MultiAssert.Equal(197U, insertionStats.TruthStats.FalseCount);
            var deletionStats = results.DetailedStats[Category.Create(WittyerType.Deletion)].OverallStats[StatsType.Event];
            MultiAssert.Equal(142U, deletionStats.TruthStats.TrueCount);
            MultiAssert.Equal(195U, deletionStats.TruthStats.FalseCount);
            var dupStats = results.DetailedStats[Category.Create(WittyerType.Duplication)].OverallStats[StatsType.Event];
            MultiAssert.Equal(0U, dupStats.TruthStats.TrueCount);
            MultiAssert.Equal(0U, dupStats.TruthStats.FalseCount);
            MultiAssert.Equal(
                new[] { Quantify.DelAndCnTrLossCategory, Quantify.InsAndCnTrGainCategory, Quantify.RefAndCnTrCategory }.Sum(it =>
                    results.DetailedStats[it].OverallStats[StatsType.Event].TruthStats.TrueCount),
                new[] { WittyerType.Deletion, WittyerType.Insertion, WittyerType.CopyNumberTandemRepeat }.Sum(it =>
                    results.DetailedStats[Category.Create(it)].OverallStats[StatsType.Event].TruthStats.TrueCount));
            MultiAssert.Equal(
                new[] { Quantify.DelAndCnTrLossCategory, Quantify.InsAndCnTrGainCategory, Quantify.RefAndCnTrCategory }.Sum(it =>
                    results.DetailedStats[it].OverallStats[StatsType.Event].TruthStats.FalseCount),
                new[] { WittyerType.Deletion, WittyerType.Insertion, WittyerType.CopyNumberTandemRepeat }.Sum(it =>
                    results.DetailedStats[Category.Create(it)].OverallStats[StatsType.Event].TruthStats.FalseCount));
            MultiAssert.Equal(
                new[] { Quantify.DelAndCnTrLossCategory, Quantify.InsAndCnTrGainCategory, Quantify.RefAndCnTrCategory }.Sum(it =>
                    results.DetailedStats[it].OverallStats[StatsType.Event].QueryStats.TrueCount),
                new[] { WittyerType.Deletion, WittyerType.Insertion, WittyerType.CopyNumberTandemRepeat }.Sum(it =>
                    results.DetailedStats[Category.Create(it)].OverallStats[StatsType.Event].QueryStats.TrueCount));
            MultiAssert.Equal(
                new[] { Quantify.DelAndCnTrLossCategory, Quantify.InsAndCnTrGainCategory, Quantify.RefAndCnTrCategory }.Sum(it =>
                    results.DetailedStats[it].OverallStats[StatsType.Event].QueryStats.FalseCount),
                new[] { WittyerType.Deletion, WittyerType.Insertion, WittyerType.CopyNumberTandemRepeat }.Sum(it =>
                    results.DetailedStats[Category.Create(it)].OverallStats[StatsType.Event].QueryStats.FalseCount));
            MultiAssert.AssertAll();
        }

        [Fact]
        public void VntrVsTruth_CrossType_Off_Works()
        {
            var outputDirectory = Path.GetRandomFileName().ToDirectoryInfo();
            var inputSpecs = InputSpec.GenerateDefaultInputSpecs(false).Select(i => InputSpec.Create(i.VariantType, i.BinSizes,
                    i.BasepairDistance, i.PercentThreshold, i.ExcludedFilters, i.IncludedFilters, IncludeBedFile.CreateFromBedFile(Bed)))
                .ToDictionary(i => i.VariantType, i => i);
            var wittyerSettings = WittyerSettings.Create(outputDirectory,
                Path.Combine("Resources", "VNTR", "HG002.delphi0.1.GRCh38.SV.vcf.gz").ToFileInfo(), 
                Path.Combine("Resources", "VNTR", "HG002.delphi0.1.GRCh38.VNTR.vcf.gz").ToFileInfo(),
                ImmutableList<ISamplePair>.Empty, EvaluationMode.GenotypeMatching,
                inputSpecs);

            var (_, query, truth) = MainLauncher.GenerateResults(wittyerSettings)
                .EnumerateSuccesses().First();
            var results = MainLauncher
                .GenerateSampleMetrics(truth, query, false, inputSpecs, false);
            var stats = results.DetailedStats[Category.Create(WittyerType.CopyNumberTandemRepeat)].OverallStats[StatsType.Event];
            // entries have been spot checked so these stats are assumed to be correct
            MultiAssert.Equal(417U, stats.QueryStats.FalseCount);
            MultiAssert.Equal(0U, stats.QueryStats.TrueCount);
            MultiAssert.Equal(0U, stats.TruthStats.TrueCount);
            MultiAssert.Equal(0U, stats.TruthStats.FalseCount);
            stats = results.DetailedStats[Category.Create(WittyerType.Insertion)].OverallStats[StatsType.Event];
            MultiAssert.Equal(0U, stats.TruthStats.TrueCount);
            MultiAssert.Equal(403U, stats.TruthStats.FalseCount);
            stats = results.DetailedStats[Category.Create(WittyerType.Deletion)].OverallStats[StatsType.Event];
            MultiAssert.Equal(0U, stats.TruthStats.TrueCount);
            MultiAssert.Equal(337U, stats.TruthStats.FalseCount);
            stats = results.DetailedStats[Category.Create(WittyerType.Duplication)].OverallStats[StatsType.Event];
            MultiAssert.Equal(0U, stats.TruthStats.TrueCount);
            MultiAssert.Equal(0U, stats.TruthStats.FalseCount);
            MultiAssert.AssertAll();
        }

        [Fact]
        public void VntrVsVntr_Works()
        {
            var outputDirectory = Path.GetRandomFileName().ToDirectoryInfo();
            var inputSpecs = InputSpec.GenerateDefaultInputSpecs(true)
                .ToDictionary(i => i.VariantType, i => i);
            var wittyerSettings = WittyerSettings.Create(outputDirectory, Truth, Query,
                ImmutableList<ISamplePair>.Empty, EvaluationMode.CrossTypeAndSimpleCounting,
                inputSpecs);

            var (_, query, truth) = MainLauncher.GenerateResults(wittyerSettings)
                .EnumerateSuccesses().First();
            var results = MainLauncher
                .GenerateSampleMetrics(truth, query, false, inputSpecs, true);
            var stats = results.DetailedStats[Category.Create(WittyerType.CopyNumberTandemRepeat)].OverallStats[StatsType.Event];
            // MultiAssert.Equal(stats.TruthStats.FalseCount, stats.QueryStats.FalseCount);
            // MultiAssert.Equal(stats.TruthStats.TrueCount, stats.QueryStats.TrueCount);
            MultiAssert.Equal(15U, stats.TruthStats.FalseCount);
            MultiAssert.Equal(16U, stats.QueryStats.FalseCount);
            MultiAssert.Equal(15U, stats.TruthStats.TrueCount);
            MultiAssert.Equal(15U, stats.QueryStats.TrueCount);
            MultiAssert.AssertAll();
        }
    }
}