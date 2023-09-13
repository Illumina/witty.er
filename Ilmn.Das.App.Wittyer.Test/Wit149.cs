using System.Collections.Immutable;
using System.IO;
using System.Linq;
using Ilmn.Das.App.Wittyer.Infrastructure;
using Ilmn.Das.App.Wittyer.Input;
using Ilmn.Das.App.Wittyer.Stats;
using Ilmn.Das.Core.Tries.Extensions;
using Ilmn.Das.Std.AppUtils.Misc;
using Ilmn.Das.Std.XunitUtils;
using Xunit;

namespace Ilmn.Das.App.Wittyer.Test
{
    public class Wit149
    {
        private static readonly FileInfo Truth =
            Path.Combine("Resources", "WIT-149", "truth.vcf").ToFileInfo();

        private static readonly FileInfo Query =
            Path.Combine("Resources", "WIT-149", "query.vcf").ToFileInfo();

        private static readonly FileInfo Bed =
            Path.Combine("Resources", "WIT-149", "bed.bed").ToFileInfo();
        
        [Fact]
        public void WholeChrIncluded()
        {
            var outputDirectory = Path.GetRandomFileName().ToDirectoryInfo();
            var inputSpecs = InputSpec.GenerateDefaultInputSpecs(true).Select(i => InputSpec.Create(i.VariantType, i.BinSizes,
                    10000, i.PercentThreshold, i.ExcludedFilters, i.IncludedFilters, IncludeBedFile.CreateFromBedFile(Bed)))
                .ToDictionary(i => i.VariantType, i => i);
            var wittyerSettings = WittyerSettings.Create(outputDirectory, Truth, Query,
                ImmutableList<ISamplePair>.Empty, EvaluationMode.CrossTypeAndSimpleCounting,
                inputSpecs);

            var (_, query, truth) = MainLauncher.GenerateResults(wittyerSettings).EnumerateSuccesses().First();
            var results = MainLauncher.GenerateSampleMetrics(truth, query, false, inputSpecs, true);
            // should be end of bed - (start of query + 1 for padded base) = 135086622 - 1 = 135086621
            MultiAssert.Equal(135086621U, results.OverallStats[StatsType.Base].QueryStats.TrueCount);
            MultiAssert.Equal(135086621U, results.OverallStats[StatsType.Base].TruthStats.TrueCount);
            MultiAssert.Equal(1U, results.OverallStats[StatsType.Event].QueryStats.TrueCount);
            MultiAssert.Equal(1U, results.OverallStats[StatsType.Event].TruthStats.TrueCount);
            MultiAssert.AssertAll();
        }
    }
}