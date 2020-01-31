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
    public class Wit144
    {
        private static readonly FileInfo Truth =
            Path.Combine("Resources", "WIT-144", "truth.vcf").ToFileInfo();

        private static readonly FileInfo Query =
            Path.Combine("Resources", "WIT-144", "query.vcf").ToFileInfo();

        private static readonly FileInfo Bed =
            Path.Combine("Resources", "WIT-144", "bed.bed").ToFileInfo();
        
        [Fact]
        public void Bed_Counts_Bases_Of_FP_Events()
        {
            var outputDirectory = Path.GetRandomFileName().ToDirectoryInfo();
            var inputSpecs = InputSpec.GenerateDefaultInputSpecs(false).Select(i => InputSpec.Create(i.VariantType, i.BinSizes,
                    10000, i.PercentDistance, i.ExcludedFilters, i.IncludedFilters, IncludeBedFile.CreateFromBedFile(Bed)))
                .ToDictionary(i => i.VariantType, i => i);
            var wittyerSettings = WittyerSettings.Create(outputDirectory, Truth, Query,
                ImmutableList<ISamplePair>.Empty, EvaluationMode.CrossTypeAndSimpleCounting,
                inputSpecs);

            var (_, query, truth) = MainLauncher.GenerateResults(wittyerSettings).EnumerateSuccesses().First();
            var results = MainLauncher.GenerateSampleMetrics(truth, query, false, inputSpecs);
            // should be end of bed - start of query + 1 = 149835000 - 145395620 + 1 = 4439381
            MultiAssert.Equal(4439381U, results.OverallStats[StatsType.Base].QueryStats.TrueCount);
            MultiAssert.Equal(4439381U, results.OverallStats[StatsType.Base].TruthStats.TrueCount);
            MultiAssert.Equal(0U, results.OverallStats[StatsType.Event].QueryStats.TrueCount);
            MultiAssert.Equal(0U, results.OverallStats[StatsType.Event].TruthStats.TrueCount);
            MultiAssert.AssertAll();
        }
    }
}