using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using Ilmn.Das.App.Wittyer.Infrastructure;
using Ilmn.Das.App.Wittyer.Input;
using Ilmn.Das.App.Wittyer.Utilities.Enums;
using Ilmn.Das.App.Wittyer.Vcf.Samples;
using Ilmn.Das.App.Wittyer.Vcf.Variants;
using Ilmn.Das.Std.AppUtils.Collections;
using Ilmn.Das.Std.VariantUtils.Vcf.Variants;
using JetBrains.Annotations;

namespace Ilmn.Das.App.Wittyer.Results
{
    /// <summary>
    /// Flatten the queryset and truthtree into a list of variants, used for one of the API
    /// </summary>
    public class WittyerVcfResult
    {
        public IReadOnlyList<IWittyerVariant> NormalVariants { get; }

        public IReadOnlyList<IWittyerBnd> BreakendPairs { get; }

        public IReadOnlyList<IVcfVariant> NonEvaluatedVariants { get; }

        public string SampleName { get; }

        private WittyerVcfResult(IReadOnlyList<IWittyerVariant> normalVariants, IReadOnlyList<IWittyerBnd> breakendPairs,
            IReadOnlyList<IVcfVariant> nonEvaluatedVariants, string sampleName)
        {
            NormalVariants = normalVariants.ToReadOnlyList();
            BreakendPairs = breakendPairs;
            NonEvaluatedVariants = nonEvaluatedVariants;
            SampleName = sampleName;
        }

        [NotNull]
        internal static WittyerVcfResult Create(QuerySet updatedQueries, EvaluationMode emode)
        {
            var normalVariants = new List<IWittyerVariant>();
            var breakendPairs = new List<IWittyerBnd>();

            foreach (var variant in updatedQueries.Query)
            {
                UpdateWittyerVariant(variant, emode,false);
                switch (variant)
                {
                    case IWittyerVariant normal:
                        normalVariants.Add(normal);
                        break;
                    case IWittyerBnd bnd:
                        breakendPairs.Add(bnd);
                        break;
                    default:
                        throw new InvalidDataException(
                            "Not sure why there's a not supported type in query set, check with developer!");
                }

            }
            return new WittyerVcfResult(normalVariants.ToReadOnlyList(), breakendPairs.ToReadOnlyList(), updatedQueries.NotSupportedVariants, updatedQueries.SampleName);
        }

        [NotNull]
        internal static WittyerVcfResult Create(TruthForest truths, EvaluationMode emode)
        {
            var normalVariants = new List<IWittyerVariant>();
            var breakendPairs = new List<IWittyerBnd>();

            foreach (var variant in truths.Trees.SelectMany(gtree => gtree.Value.Values.SelectMany(v => v)))
            {
                UpdateWittyerVariant(variant, emode, true);
                switch (variant)
                {
                    case IWittyerVariant normal:
                        normalVariants.Add(normal);
                        break;
                    case IWittyerBnd bnd:
                        breakendPairs.Add(bnd);
                        break;
                    default:
                        throw new InvalidDataException(
                            "Not sure why there's a not supported type in query set, check with developer!");
                }
            }

            return new WittyerVcfResult(normalVariants.ToReadOnlyList(), breakendPairs.ToReadOnlyList(),
                truths.LeftOvers.ToReadOnlyList(), truths.SampleName);
            
        }

        private static void UpdateWittyerVariant(IWittyerSimpleVariant variant,EvaluationMode emode, bool isTruth)
        {
            var sample = variant.Sample;

            var match = emode.Equals(EvaluationMode.Default);
            variant.OverlapInfo.Sort();
            switch (sample)
            {
                    case WittyerSampleInternal simple:
                        UpdateWittyerVariantHelper(simple, variant, match, isTruth);
                        break;
                case WittyerCopyNumberSample cnsample:
                    UpdateWittyerVariantHelper(cnsample.BaseSample, variant, match, isTruth);
                    break;
                case WittyerGenotypedSample gtSample:
                    UpdateWittyerVariantHelper(gtSample.BaseSample, variant, match, isTruth);
                    break;
                case WittyerGenotypedCopyNumberSample gtCnSample:
                    UpdateWittyerVariantHelper(gtCnSample.BaseSample.BaseSample, variant, match, isTruth);
                    break;
                default:
                    throw new InvalidDataException(
                        "Not sure how we get here, you must have created some non-existed wittyer sample type, check with developer!");

            }
        }

        private static void UpdateWittyerVariantHelper(WittyerSampleInternal sample, 
            IWittyerSimpleVariant variant, bool isGenotypeMatch, bool isTruth)
        {
            var correctMatch = new []{MatchEnum.AlleleAndGenotypeMatch}.ToList();
            if(!isGenotypeMatch)
                correctMatch.Add(MatchEnum.AlleleMatch);
            sample.What = variant.OverlapInfo.Select(x => x.What).ToImmutableList();
            if (variant.OverlapInfo.Count == 0)
                sample.What = ImmutableList.Create(MatchEnum.Unmatched);
            sample.Why = variant.OverlapInfo.Select(x => x.Why).ToImmutableList();
            sample.Wit = correctMatch.Any(sample.What.Contains)
                ? WitDecision.TruePositive
                : (isTruth ? WitDecision.FalseNegative : WitDecision.FalsePositive);
        }
    }
}
