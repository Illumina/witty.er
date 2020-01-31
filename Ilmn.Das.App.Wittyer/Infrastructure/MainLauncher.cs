using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using Ilmn.Das.App.Wittyer.Input;
using Ilmn.Das.App.Wittyer.Json;
using Ilmn.Das.App.Wittyer.Results;
using Ilmn.Das.App.Wittyer.Utilities.Enums;
using Ilmn.Das.App.Wittyer.Vcf;
using Ilmn.Das.Core.Tries.Extensions;
using Ilmn.Das.Std.AppUtils.Misc;
using Ilmn.Das.Std.VariantUtils.Vcf.Readers;
using JetBrains.Annotations;

namespace Ilmn.Das.App.Wittyer.Infrastructure
{
    public static class MainLauncher
    {
        [NotNull]
        public static GeneralStats GenerateJson([NotNull] IWittyerSettings settings,
            [NotNull] string cmd, bool isWritingvcf = true)
            => GeneralStats.Create(GetResultItems(settings, cmd, isWritingvcf), settings, cmd);

        [NotNull]
        public static IReadOnlyDictionary<ISamplePair, (WittyerVcfResult query, WittyerVcfResult truth)> GetResultItems(
            [NotNull] IWittyerSettings settings,
            [NotNull] string cmd, bool isWritingvcf = true)
        {
            if (isWritingvcf)
            {
                ValidateOutput(settings.OutputDirectory);
            }

            var baseQueryReader = VcfReader.TryCreate(settings.QueryVcf).GetOrThrowDebug();
            var baseTruthReader = VcfReader.TryCreate(settings.TruthVcf).GetOrThrowDebug();

            var genomeType = baseTruthReader.ReferenceGenome?.GetGenomeType()
                             ?? baseTruthReader.GetAllItems().First(x => x.Any()).GetOrThrowDebug().GetGenomeType();

            var result = ImmutableDictionary<ISamplePair, (WittyerVcfResult query, WittyerVcfResult truth)>.Empty;

            var samplePairs = settings.SamplePairs.Count == 0 ? ImmutableList<ISamplePair>.Empty.Add(SamplePair.NullPair) : settings.SamplePairs;

            foreach (var pair in samplePairs)
            {
                var truthReader = WittyerVcfReader.Create(baseTruthReader, settings.InputSpecs, settings.Mode, pair.TruthSampleName);
                var queryReader = WittyerVcfReader.Create(baseQueryReader, settings.InputSpecs, settings.Mode, pair.QuerySampleName);

                var perSampleResult =
                    GetResultItemPerSamplePair(queryReader, truthReader, pair, genomeType, settings.Mode);

                result = result.Add(pair, perSampleResult);

                if (!isWritingvcf) continue;

                var vcfWriter = new WittyerVcfWriter(perSampleResult.query, baseQueryReader.Header,
                    perSampleResult.truth, baseTruthReader.Header, cmd);

                vcfWriter.WriteOutputFile(settings.OutputDirectory);
            }

            return result;
        }

        private static void ValidateOutput([NotNull] DirectoryInfo outputDir)
        {
            if (!outputDir.Exists)
                outputDir.Create();
            if (outputDir.IsNotEmpty())
            {
                Console.Error.WriteLine(
                    $"Specified a output directory that's not empty: {outputDir.FullName}\n, witty.er requires an empty and clean output folder!");
                Environment.Exit(1);
            }
        }
        
        private static (WittyerVcfResult query, WittyerVcfResult truth) GetResultItemPerSamplePair(
            [NotNull] WittyerVcfReader queryReader, [NotNull] WittyerVcfReader truthReader,
            [NotNull] ISamplePair samplePair, GenomeType genomeType, EvaluationMode emode)
        {
            var truthSample = samplePair.TruthSampleName ??
                              (truthReader.SampleNames.Count == 0 ? null : truthReader.SampleNames.First());
            var truth = truthReader.GetTruth(truthSample);

            var querySample = samplePair.QuerySampleName ??
                              (queryReader.SampleNames.Count == 0 ? null : queryReader.SampleNames.First());
            var query = queryReader.GetQueryset(querySample, genomeType);

            Comparison.Work(truth, query.Query);

            var updatedQuery = WittyerVcfResult.Create(query, emode);
            var updatedTruth = WittyerVcfResult.Create(truth, emode);
            return (updatedQuery, updatedTruth);
        }
    }
}