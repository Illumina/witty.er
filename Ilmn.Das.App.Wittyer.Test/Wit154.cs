using System.Collections.Generic;
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
    public class Wit154
    {
        private static readonly FileInfo Truth =
            Path.Combine("Resources", "WIT-154", "truth.vcf").ToFileInfo();

        private static readonly FileInfo Query =
            Path.Combine("Resources", "WIT-154", "query.vcf").ToFileInfo();

        private static readonly FileInfo Bed =
            Path.Combine("Resources", "WIT-154", "bed.bed").ToFileInfo();

        private static readonly FileInfo Config =
            Path.Combine("Resources", "WIT-154", "config.json").ToFileInfo();

        [Fact]
        public void CrossType_ComplexBed_Works()
        {
            var outputDirectory = Path.GetRandomFileName().ToDirectoryInfo();
            var inputSpecs = InputSpec.CreateSpecsFromString(
                    File.ReadAllText(Config.FullName), IncludeBedFile.CreateFromBedFile(Bed))
                ?.ToDictionary(i => i.VariantType, i => i)
                             ?? new Dictionary<WittyerType, InputSpec>();
            var wittyerSettings = WittyerSettings.Create(outputDirectory, Truth, Query,
                ImmutableList<ISamplePair>.Empty, EvaluationMode.CrossTypeAndSimpleCounting,
                inputSpecs);

            var (_, query, truth) = MainLauncher.GenerateResults(wittyerSettings)
                .EnumerateSuccesses().First();
            var results = MainLauncher
                .GenerateSampleMetrics(truth, query, false, inputSpecs);
            var baseStats = results.DetailedStats[WittyerType.Deletion].OverallStats[StatsType.Base];
            MultiAssert.Equal(206678U, baseStats.QueryStats.TrueCount);
            MultiAssert.Equal(206678U, baseStats.TruthStats.TrueCount);
            MultiAssert.AssertAll();
        }
    }
}