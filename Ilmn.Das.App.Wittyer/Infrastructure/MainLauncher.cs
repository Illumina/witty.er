using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Ilmn.Das.App.Wittyer.Input;
using Ilmn.Das.App.Wittyer.Json;
using Ilmn.Das.App.Wittyer.Results;
using Ilmn.Das.App.Wittyer.Stats;
using Ilmn.Das.App.Wittyer.Utilities;
using Ilmn.Das.App.Wittyer.Utilities.Enums;
using Ilmn.Das.App.Wittyer.Vcf;
using Ilmn.Das.App.Wittyer.Vcf.Variants;
using Ilmn.Das.Core.Tries;
using Ilmn.Das.Core.Tries.Extensions;
using Ilmn.Das.Std.VariantUtils.Vcf.Headers;
using Ilmn.Das.Std.VariantUtils.Vcf.Readers;
using JetBrains.Annotations;

namespace Ilmn.Das.App.Wittyer.Infrastructure
{
    /// <summary>
    /// The main launcher to use the API
    /// </summary>
    public static class MainLauncher
    {
        // ReSharper disable once ConvertToConstant.Local // this is mainly to allow easy parallization.
        private static readonly bool RunParallel = false;

        /// <summary>
        /// Generates the sample stats.
        /// </summary>
        /// <param name="truth">The truth.</param>
        /// <param name="query">The query.</param>
        /// <param name="isGenotypeEvaluated">if set to <c>true</c> [is genotype evaluated].</param>
        /// <param name="inputSpecs">The input specs.</param>
        /// <returns></returns>
        [NotNull]
        public static SampleMetrics GenerateSampleMetrics([NotNull] IWittyerResult truth,
            [NotNull] IWittyerResult query, bool isGenotypeEvaluated,
            [NotNull] IReadOnlyDictionary<WittyerType, InputSpec> inputSpecs)
            => Quantify.GenerateSampleStats(truth, query, isGenotypeEvaluated, inputSpecs);

        /// <summary>
        /// Generates the json and optionally writes out the annotated VCF.
        /// </summary>
        /// <param name="settings">The settings.</param>
        /// <param name="results">All the results.</param>
        /// <param name="command">The command to put as part of the Json object.</param>
        /// <returns></returns>
        [NotNull]
        public static ITry<GeneralStats> GenerateJson(IWittyerSettings settings,
            [NotNull] IEnumerable<(ISamplePair sampleName, IWittyerResult query, IWittyerResult truth)> results,
            [CanBeNull] string command = null)
            => TryFactory.Try(() => GeneralStats.Create(results,
                settings.Mode == EvaluationMode.Default, settings.InputSpecs, command));

        /// <summary>
        /// Generates the json and optionally writes out the annotated VCF.
        /// </summary>
        /// <param name="metrics">All the <see cref="SampleMetrics"/>.</param>
        /// <param name="command">The command to put as part of the Json object.</param>
        /// <returns></returns>
        [NotNull]
        public static ITry<GeneralStats> GenerateJsonFromSampleMetrics(IEnumerable<SampleMetrics> metrics,
            [CanBeNull] string command = null)
            => TryFactory.Try(() => GeneralStats.Create(metrics, command));

        /// <summary>
        /// Generates the two-sample merged VCF strings from the given results.
        /// </summary>
        /// <param name="queryResult">The query result.</param>
        /// <param name="truthResult">The truth result.</param>
        /// <param name="command">The command to input for the <see cref="IVcfHeader"/>.</param>
        public static IEnumerable<string> GenerateMergedVcfStrings([NotNull] IWittyerResult queryResult,
            [NotNull] IWittyerResult truthResult, [CanBeNull] string command = null)
        {
            if (queryResult != null && truthResult != null && queryResult.IsTruth == truthResult.IsTruth)
                throw new InvalidOperationException(
                    $"Called {nameof(GenerateMergedVcfStrings)} when you passed in two {nameof(IWittyerResult)}s of the same type!");

            return WittyerVcfWriter.GenerateVcfStrings(queryResult, truthResult, command);
        }

        /// <summary>
        /// Generates the VCF strings from the single result.
        /// </summary>
        /// <param name="result">The <see cref="IWittyerResult"/>.</param>
        /// <param name="command">The command to input for the <see cref="IVcfHeader"/>.</param>
        public static IEnumerable<string> GenerateSingleSampleVcfStrings([NotNull] IWittyerResult result, [CanBeNull] string command = null)
        {
            IWittyerResult query = null, truth = null;
            if (result.IsTruth)
                truth = result;
            else
                query = result;

            return WittyerVcfWriter.GenerateVcfStrings(query, truth, command);
        }

        /// <summary>
        /// Gets the result items given the <see cref="IWittyerSettings"/>.
        /// </summary>
        /// <param name="settings">The settings.</param>
        /// <returns></returns>
        [ItemNotNull]
        [NotNull]
        public static IEnumerable<ITry<(ISamplePair samplePair, IWittyerResult query, IWittyerResult truth)>>
            GenerateResults([NotNull] IWittyerSettings settings)
        {
            WittyerSettings.ValidateSettings(settings);
            
            var baseQueryReader = VcfReader.TryCreate(settings.QueryVcf).GetOrThrowDebug();
            var baseTruthReader = VcfReader.TryCreate(settings.TruthVcf).GetOrThrowDebug();

            var samplePairs = settings.SamplePairs.Count == 0
                ? ImmutableList<ISamplePair>.Empty.Add(SamplePair.NullPair)
                : settings.SamplePairs;
            var sampleSet = new HashSet<string>();
            foreach (var pair in samplePairs)
            {
                OverlappingUtils.ClearWhoTags();

                var truthReader = WittyerVcfReader.Create(baseTruthReader, settings.InputSpecs, settings.Mode,
                    pair.TruthSampleName);
                var queryReader = WittyerVcfReader.Create(baseQueryReader, settings.InputSpecs, settings.Mode,
                    pair.QuerySampleName);

                yield return TryFactory.Try(() => GetResultItemPerSamplePair(pair));

                (ISamplePair pair, IWittyerResult query, IWittyerResult truth) GetResultItemPerSamplePair(
                    ISamplePair samplePair)
                {
                    var truthSample = samplePair.TruthSampleName ?? truthReader.SampleNames.FirstOrDefault();
                    var (truth, genomeType) = truthReader.GetTruth(truthSample);

                    var querySample = samplePair.QuerySampleName ??
                                      queryReader.SampleNames.FirstOrDefault();
                    var query = queryReader.GetQueryset(querySample, genomeType);

                    if (samplePair.TruthSampleName != truthSample || samplePair.QuerySampleName != querySample)
                        // ReSharper disable AssignNullToNotNullAttribute
                        samplePair = SamplePair.Create(truthSample ?? SamplePair.Default.TruthSampleName,
                            querySample ?? SamplePair.Default.QuerySampleName);
                    // ReSharper restore AssignNullToNotNullAttribute

                    if (samplePair.QuerySampleName == null)
                        samplePair = SamplePair.Default;

                    var sampleKey = samplePair.TruthSampleName + samplePair.QuerySampleName;
                    if (!sampleSet.Add(sampleKey))
                        throw new InvalidOperationException(
                            $"Tried to analyze {samplePair.TruthSampleName} vs {samplePair.QuerySampleName} more than once!");

                    var isCrossType = settings.Mode == EvaluationMode.CrossTypeAndSimpleCounting;
                    var isSimpleCounting = settings.Mode == EvaluationMode.CrossTypeAndSimpleCounting 
                                           || settings.Mode == EvaluationMode.SimpleCounting;
                    if (RunParallel)
                        EvaluateParallel();
                    else
                        EvaluateSerial();

                    return (samplePair, query, truth);

                    void EvaluateParallel()
                    {
                        Parallel.Invoke(() => Parallel.ForEach(query.VariantsInternal, variants =>
                            {
                                foreach (var variant in variants)
                                    OverlappingUtils.DoOverlapping(truth.VariantTrees, variant,
                                        OverlappingUtils.IsVariantAlleleMatch, isCrossType, isSimpleCounting);
                                foreach (var variant in variants)
                                    variant.Finalize(WitDecision.FalsePositive, settings.Mode,
                                        settings.InputSpecs[variant.VariantType].IncludedRegions?.IntervalTree);
                            }),
                            () => Parallel.ForEach(query.BreakendPairsInternal, variants =>
                            {
                                foreach (var variant in variants)
                                    OverlappingUtils.DoOverlapping(truth.BpInsTrees, variant,
                                        OverlappingUtils.IsBndAlleleMatch, isCrossType, isSimpleCounting);
                                foreach (var variant in variants)
                                    variant.Finalize(WitDecision.FalsePositive, settings.Mode,
                                        settings.InputSpecs[variant.VariantType].IncludedRegions?.IntervalTree);
                            }));

                        Parallel.Invoke(
                            () => Parallel.ForEach(truth.VariantsInternal,
                                variants =>
                                {
                                    foreach (var variant in variants)
                                        variant.Finalize(WitDecision.FalseNegative, settings.Mode,
                                            settings.InputSpecs[variant.VariantType].IncludedRegions?.IntervalTree);
                                }),
                            () => Parallel.ForEach(truth.BreakendPairsInternal,
                                variants =>
                                {
                                    foreach (var variant in variants)
                                        variant.Finalize(WitDecision.FalseNegative, settings.Mode,
                                            settings.InputSpecs[variant.VariantType].IncludedRegions?.IntervalTree);
                                }));
                    }

                    void EvaluateSerial()
                    {
                        foreach (var variants in query.VariantsInternal)
                        {
                            foreach (var variant in variants)
                                OverlappingUtils.DoOverlapping(truth.VariantTrees, variant,
                                    OverlappingUtils.IsVariantAlleleMatch, isCrossType, isSimpleCounting);
                            foreach (var variant in variants)
                                variant.Finalize(WitDecision.FalsePositive, settings.Mode,
                                    settings.InputSpecs[variant.VariantType].IncludedRegions?.IntervalTree);
                        }

                        foreach (var variants in query.BreakendPairsInternal)
                        {
                            foreach (var variant in variants)
                                OverlappingUtils.DoOverlapping(truth.BpInsTrees, variant,
                                    OverlappingUtils.IsBndAlleleMatch, isCrossType, isSimpleCounting);
                            foreach (var variant in variants)
                                variant.Finalize(WitDecision.FalsePositive, settings.Mode,
                                    settings.InputSpecs[variant.VariantType].IncludedRegions?.IntervalTree);
                        }

                        foreach (var variant in truth.VariantsInternal
                            .SelectMany(v => v.AsEnumerable<IMutableWittyerSimpleVariant>())
                            .Concat(truth.BreakendPairsInternal.SelectMany(v => v)))
                            variant.Finalize(WitDecision.FalseNegative, settings.Mode,
                                settings.InputSpecs[variant.VariantType].IncludedRegions?.IntervalTree);
                    }
                }
            }
        }
    }
}