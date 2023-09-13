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
    public class Wit155
    {
        private static readonly FileInfo Query =
            Path.Combine("Resources", "WIT-155", "query.vcf").ToFileInfo();

        private static readonly FileInfo Bed =
            Path.Combine("Resources", "WIT-155", "bed.bed").ToFileInfo();

        [Fact]
        public void CrossType_ComplexBed_Works()
        {
            var outputDirectory = Path.GetRandomFileName().ToDirectoryInfo();
            var inputSpecs = InputSpec.GenerateDefaultInputSpecs(true)
                .ToDictionary(it => it.VariantType,
                    it => InputSpec.Create(it.VariantType, it.BinSizes, it.BasepairDistance, it.PercentThreshold,
                        it.ExcludedFilters, it.IncludedFilters, IncludeBedFile.CreateFromBedFile(Bed)));
            var wittyerSettings = WittyerSettings.Create(outputDirectory, Query, Query,
                ImmutableList<ISamplePair>.Empty, EvaluationMode.CrossTypeAndSimpleCounting,
                inputSpecs);

            var (_, query, truth) = MainLauncher.GenerateResults(wittyerSettings)
                .EnumerateSuccesses().First();
            var results = MainLauncher
                .GenerateSampleMetrics(truth, query, false, inputSpecs, true);
            var baseStats = results.DetailedStats[Category.Create(WittyerType.Deletion)].OverallStats[StatsType.Base];
            MultiAssert.Equal(0U, baseStats.QueryStats.FalseCount);
            MultiAssert.Equal(0U, baseStats.TruthStats.FalseCount);
            MultiAssert.AssertAll();
        }
    }
}