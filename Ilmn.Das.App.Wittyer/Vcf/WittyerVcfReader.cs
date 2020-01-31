using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using Ilmn.Das.App.Wittyer.Infrastructure;
using Ilmn.Das.App.Wittyer.Input;
using Ilmn.Das.App.Wittyer.Utilities;
using Ilmn.Das.App.Wittyer.Utilities.Enums;
using Ilmn.Das.App.Wittyer.Vcf.Variants;
using Ilmn.Das.App.Wittyer.Vcf.Variants.Breakend;
using Ilmn.Das.Core.Tries.Extensions;
using Ilmn.Das.Std.AppUtils.Collections;
using Ilmn.Das.Std.AppUtils.Enums;
using Ilmn.Das.Std.AppUtils.Files.FileReader;
using Ilmn.Das.Std.BioinformaticUtils.GenomicFeatures;
using Ilmn.Das.Std.VariantUtils.Vcf;
using Ilmn.Das.Std.VariantUtils.Vcf.Readers;
using Ilmn.Das.Std.VariantUtils.Vcf.Variants;
using Ilmn.Das.Std.VariantUtils.Vcf.Variants.Samples;
using JetBrains.Annotations;

namespace Ilmn.Das.App.Wittyer.Vcf
{
    /// <summary>
    /// Wittyer vcf reader. Should work for both truth and query
    /// </summary>
    /// <seealso cref="Std.AppUtils.Files.FileReader.IFileReader{IWittyerSimpleVariant}" />
    public class WittyerVcfReader : IFileReader<IWittyerSimpleVariant>
    {
        private const int MaxNonSupportedVariantToPrint = 10;
        private readonly IVcfReader _vcfReader;
        private readonly IReadOnlyDictionary<WittyerVariantType, InputSpec> _inputSpec;
        private readonly IReadOnlyList<IVcfVariant> _baseVariants;
        private readonly EvaluationMode _mode;

        [NotNull] public IImmutableList<string> SampleNames => _vcfReader.Header.SampleNames;

        public IEnumerator<IWittyerSimpleVariant> GetEnumerator() => throw new InvalidOperationException("You are not supposed to call this function");

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        [Obsolete("Please do not use. Use 'GetQueryset()/GetTruth()' instead")]
        public IEnumerable<IWittyerSimpleVariant> GetAllItems() => throw new InvalidOperationException("You are not supposed to call this function");

        public IEnumerable<string> ReadAllLines() => _vcfReader.ReadAllLines();

        public FileInfo FileSource => _vcfReader.FileSource;

        /// <summary>
        /// Gets the queryset.
        /// </summary>
        /// <param name="sampleName">Name of the sample.</param>
        /// <param name="genomeType">Type of the genome.</param>
        /// <returns></returns>
        [NotNull]
        internal QuerySet GetQueryset([CanBeNull] string sampleName, GenomeType genomeType)
        {
            var bndSet = new Dictionary<IGeneralBnd, IVcfVariant>(BreakendPairComparer.Default);
            var query = QuerySet.Builder.Create(sampleName.IsNullOrEmpty()
                ? SamplePair.Default.QuerySampleName
                : sampleName);

            var errorList = new List<string>();
            foreach (var baseVariant in _baseVariants)
            {
                var result = CreateVariant(baseVariant, genomeType, bndSet, sampleName, errorList, false, _mode);
                if (result is IGeneralBnd) //ugly implementation, first of breakend pair will return left as IGeneralBnd
                    continue;

                if (result is IVcfVariant vcfVariant)
                    query.AddNonSupported(vcfVariant);
                else if (result is IWittyerSimpleVariant simpleVariant)
                    query.AddQuery(simpleVariant);
            }

            if (errorList.Count > 0)
                Console.Error.WriteLine(
                    $"Fail to parse {errorList.Count} variants from query, check first {MaxNonSupportedVariantToPrint} or less: \n" +
                    errorList.Take(MaxNonSupportedVariantToPrint).StringJoin("\n"));

            if (bndSet.Count == 0) return query.Build();

            Console.Error.WriteLine(
                $"Found single breakend in query!!!! Those entries will be completely exclude from stats. First {MaxNonSupportedVariantToPrint} or less:");

            Console.Error.WriteLine(bndSet.Values.Take(MaxNonSupportedVariantToPrint).StringJoin("\n"));

            foreach (var kvp in bndSet)
                query.AddNonSupported(CreateUnsupportedVariant(kvp.Value, sampleName, false, FailedReason.UnpairedBnd));

            return query.Build();
        }

        /// <summary>
        /// Gets the truth.
        /// </summary>
        /// <param name="sampleName">Name of the sample.</param>
        /// <returns></returns>
        /// <exception cref="InvalidDataException"></exception>
        internal TruthForest GetTruth([CanBeNull] string sampleName)
        {
            var bndSet = new Dictionary<IGeneralBnd, IVcfVariant>(BreakendPairComparer.Default);
            var truth = TruthForest.CreateEmpty(sampleName.IsNullOrEmpty() ? SamplePair.Default.TruthSampleName : sampleName);

            var errorList = new List<string>();
            foreach (var baseVariant in _baseVariants)
            {
                var result = CreateVariant(baseVariant, GenomeType.Unknown, bndSet, sampleName, errorList, true, _mode);
                if (result is IGeneralBnd)
                    continue;

                if (result is IVcfVariant vcfVariant)
                    truth.AddToLeftOver(vcfVariant);
                else if (result is IWittyerSimpleVariant simpleVariant)
                   truth.AddVariantToTrees(simpleVariant);
            }

            if (errorList.Count > 0)
            {
                var msg = (errorList.Count > MaxNonSupportedVariantToPrint
                    ? errorList.Take(MaxNonSupportedVariantToPrint)
                    : errorList).StringJoin("\n");

                Console.Error.WriteLine(
                    $"Fail to parse {errorList.Count} variants from truth, check first {MaxNonSupportedVariantToPrint} or less: \n{msg}");
            }

            if (bndSet.Count > 0)
            {
                Console.Error.WriteLine($"Found single breakend in truth!!!! Those entries will be completely exclude from stats. First {MaxNonSupportedVariantToPrint} or less:");
                var msg = (bndSet.Count > MaxNonSupportedVariantToPrint
                    ? bndSet.Values.Take(MaxNonSupportedVariantToPrint)
                    : bndSet.Values).StringJoin("\n");

                Console.Error.WriteLine(msg);
                foreach (var kvp in bndSet)
                {
                    truth.AddToLeftOver(CreateUnsupportedVariant(kvp.Value, sampleName, true, FailedReason.UnpairedBnd));
                }
            }

            return truth;
        }

        private IContigProvider CreateVariant([NotNull] IVcfVariant baseVariant, GenomeType genomeType, 
            IDictionary<IGeneralBnd, IVcfVariant> bndSet, [CanBeNull] string sampleName, IList<string> errorList, 
            bool isTruth, EvaluationMode mode)
        { 
            var vcfVariant = baseVariant.ConvertGenomeType(genomeType);

            var svType = baseVariant.ParseWittyerVariantType(sampleName);

            //User does not specify this SVTYPE in input spec, consider user want to exlude this particular SVTYPE comparison entirely
            if (!_inputSpec.ContainsKey(svType))
                return CreateUnsupportedVariant(vcfVariant, sampleName, isTruth, FailedReason.FailedFilter);

            var isSupportedVariant = IsSupportedVariant(vcfVariant, sampleName, _inputSpec[svType], isTruth, svType);
            if (!isSupportedVariant.Equals(FailedReason.Unset))
                return CreateUnsupportedVariant(vcfVariant, sampleName, isTruth, isSupportedVariant);

            //get InputSpec
            var currentInputSpec = _inputSpec[svType];
            var actualBaseDistance = currentInputSpec.BasepairDistance;
            var acutalPercentDistance = currentInputSpec.PercentageDistance;

            switch (svType)
            {
                case WittyerVariantType.CopyNumberReference
                    when baseVariant.Info.TryGetValue(VcfConstants.SvTypeKey, out var svTypeString)
                         && !WittyerConstants.BaseLevelStatsTypeStrings.Contains(svTypeString):
                    return CreateUnsupportedVariant(vcfVariant, sampleName, isTruth,
                        FailedReason.UnassessedRefCall);

                case WittyerVariantType.Insertion:
                    //insertion is basically using one same record as the both entries of the breakend pair
                    return WittyerBnd.WittyerBndInternal.Create(vcfVariant, vcfVariant, sampleName,
                        currentInputSpec.PercentageDistance, currentInputSpec.BasepairDistance,
                        currentInputSpec.Bins);

                case WittyerVariantType.TranslocationBreakend:
                case WittyerVariantType.IntraChromosomeBreakend:
                    var currentBnd = GeneralBnd.Create(vcfVariant);

                    //Note: this means the paired BND is found as a key in dictionary. Checkout the comparer for details
                    if (bndSet.TryGetValue(currentBnd, out var v))
                    {
                        if (!bndSet.Remove(currentBnd))
                            throw new InvalidOperationException(
                                $"Cannot remove {v} from breakend dictionary when pair is found: {vcfVariant}! Find a developer to debug!");

                        return WittyerBnd.WittyerBndInternal.Create(vcfVariant, v, sampleName, acutalPercentDistance,
                            actualBaseDistance, currentInputSpec.Bins);
                    }

                    bndSet.Add(currentBnd, vcfVariant);
                    return currentBnd;

                default:
                    if (mode == EvaluationMode.CrossTypeAndSimpleCounting &&
                        WittyerConstants.BaseLevelStatsTypes.Contains(svType))
                    {
                        var crossMatchedType = FigureOutCrossMatchingSvType(svType);
                        if (crossMatchedType == WittyerVariantType.Invalid)
                            return CreateUnsupportedVariant(vcfVariant, sampleName, isTruth,
                                FailedReason.UnassessedRefCall);
                        svType = crossMatchedType;
                    }

                    break;
            }

            try
            {
                return WittyerVariant.WittyerVariantInternal.Create(vcfVariant, sampleName,
                    currentInputSpec.PercentageDistance, currentInputSpec.BasepairDistance,
                    currentInputSpec.Bins, svType);
            }
            catch (Exception)
            {
                if (errorList.Count <= MaxNonSupportedVariantToPrint)
                    errorList.Add(vcfVariant.ToString());
                return CreateUnsupportedVariant(vcfVariant, sampleName, isTruth, FailedReason.Other);
            }

            WittyerVariantType FigureOutCrossMatchingSvType(WittyerVariantType realType)
            {
                if (realType != WittyerVariantType.Cnv)
                    return svType;
                if (baseVariant.Samples.Count == 0)
                    throw new InvalidDataException($"CNVs must have sample field, as {VcfConstants.CnSampleFieldKey} is a required sample field for CNV evaluation!");

                var sample = sampleName == null ? baseVariant.Samples[0] : baseVariant.Samples[sampleName];
                var ploidy = sample.SampleDictionary
                    .TryGetValue(VcfConstants.GenotypeKey).Select(x =>
                        x.Split(VcfConstants.GtPhasedValueDelimiter[0], VcfConstants.GtUnphasedValueDelimiter[0])
                            .Length).GetOrElse(2);             

                if (!sample.SampleDictionary.ContainsKey(VcfConstants.CnSampleFieldKey))
                    throw new InvalidDataException(
                        $"{VcfConstants.CnSampleFieldKey} does not exist in variant:\n{baseVariant}");

                var cnString = sample.SampleDictionary[VcfConstants.CnSampleFieldKey];
                    
                var valid = uint.TryParse(cnString, out var cn);
                if (!valid)
                    throw new InvalidDataException(
                        $"Impossible to evaluate CNVs: Cannnot parse {VcfConstants.CnSampleFieldKey}");

                // should never happen.
                if (cn == ploidy)
                    return WittyerVariantType.Invalid;

                return cn < ploidy ? WittyerVariantType.Deletion : WittyerVariantType.Duplication;
            }
        }

        private FailedReason IsSupportedVariant([NotNull] IVcfVariant variant, [CanBeNull] string sampleName, 
            InputSpec inputSpec, bool isTruth, WittyerVariantType svType)
        {
            //check filter
            var filterOk = variant.IsFilterIncluded(isTruth ? ImmutableHashSet.Create(VcfConstants.PassFilter) : inputSpec.IncludedFilters,
                isTruth ? ImmutableHashSet<string>.Empty : inputSpec.ExcludedFilters);

            if (!filterOk)
                return FailedReason.FailedFilter;

            //SVLEN = 0 are ignored except Ins and Bnd
            var isInValidSvLen = !new[] { WittyerVariantType.Insertion, WittyerVariantType.TranslocationBreakend,
                                     WittyerVariantType.IntraChromosomeBreakend}.Contains(svType) &&
                                 variant.GetSvLength() == 0;

            if (isInValidSvLen)
                return FailedReason.InvalidSvLen;

            //Bnd with pos and alt to be same position (temporarily to accomendate the situation of SVLEN=0 INV representing as bnd)
            if (svType.Equals(WittyerVariantType.IntraChromosomeBreakend))
            {
                var bnd = GeneralBnd.Create(variant);
                if (bnd.Position == bnd.Mate.Position)
                    return FailedReason.InvalidSvLen;
            }

            //sampleFilterOk
            var sampleFilterOk = isTruth || !inputSpec.IncludedFilters.Contains(VcfConstants.PassFilter) || variant.IsSamplePassFilter(sampleName);

            if (!sampleFilterOk)
                return FailedReason.FailedFilter;

            //CNVs has to have at least one sample and need the CN key
            if (svType == WittyerVariantType.Cnv && (variant.Samples.Count == 0
                                         || !variant.Samples[0].SampleDictionary.ContainsKey(VcfConstants.CnSampleFieldKey)))
            {
                return FailedReason.CnvWithoutCn;
            }

            //CN=. in CNV/DEL/DUP is not assessed
            if (svType == WittyerVariantType.Cnv)
            {
                var sample = sampleName == null ? variant.Samples[0] : variant.Samples[sampleName];
                if (sample.SampleDictionary
                    .TryGetValue(VcfConstants.CnSampleFieldKey, out string cnValue) && cnValue == VcfConstants.MissingValueString)
                {
                    return FailedReason.UndeterminedCn;
                }
            } 

            return FailedReason.Unset;
        }


        [NotNull]
        private IVcfVariant CreateUnsupportedVariant([NotNull] IVcfVariant baseVariant, [CanBeNull] string sampleName,
            bool isTruth,
            FailedReason why)
        {
            var defaultName = isTruth ? SamplePair.Default.TruthSampleName : SamplePair.Default.QuerySampleName;
            var realName = sampleName ?? defaultName;
            var samplBuilder = SampleDictionaries.CreateBuilder()
                // ReSharper disable once AssignNullToNotNullAttribute
                .AddSample(realName).MoveOnToDictionaries();

            var updatedDicts = ImmutableDictionary<string, string>.Empty
                .Add(WittyerConstants.WittyMetaInfoLineKeys.Wit, WitDecision.NotAssessed.ToStringDescription())
                .Add(WittyerConstants.WittyMetaInfoLineKeys.Why, why.ToString());

            if (sampleName == null && !baseVariant.Samples.IsNullOrEmpty())
                updatedDicts = updatedDicts.AddRange(baseVariant.Samples[0].SampleDictionary);
            else if (sampleName != null)
                updatedDicts = updatedDicts.AddRange(baseVariant.Samples[sampleName].SampleDictionary);

            foreach (var kvp in updatedDicts)
                samplBuilder.SetSampleField(realName, (kvp.Key, kvp.Value));

            return baseVariant.ToBuilder().SetSamples(samplBuilder.Build()).Build();
        }

        [NotNull]
        public static WittyerVcfReader Create([NotNull] IVcfReader vcfReader, 
            [NotNull] IReadOnlyDictionary<WittyerVariantType, InputSpec> inputSpec, EvaluationMode mode, [CanBeNull] string sampleName)
        {
            var successVariants = new List<IVcfVariant>();
            var exceptions = new List<Exception>();

            foreach (var item in vcfReader.GetAllItems())
            {
                item.DoOnSuccess(v =>
                {
                    var variant = v;
                    if (v.Samples.Count > 0)
                    {
                        var sample = sampleName == null ? v.Samples[0] : v.Samples[sampleName];
                        var sampleDict = SampleDictionaries.CreateBuilder().AddSample(sample.SampleName)
                            .MoveOnToDictionaries();
                        sample.SampleDictionary.ForEach(kvp =>
                            {
                                sampleDict.SetSampleField(sample.SampleName, (kvp.Key, kvp.Value));
                            });
                        variant = v.ToBuilder().SetSamples(sampleDict.Build()).Build();
                    }
                    successVariants.Add(variant);
                }).DoOnFailure(e => exceptions.Add(e));
            }

            if (exceptions.Count == 0)
                return new WittyerVcfReader(vcfReader, inputSpec, successVariants.AsReadOnly(), mode);

            var msg = (exceptions.Count > 5 ? exceptions.Take(5) : exceptions).Select(x => x.Message)
                .StringJoin("\n");
            throw new InvalidDataException
                ($"Found {exceptions.Count} variants cannot be parsed in {vcfReader.FileSource.FullName}: first 5 or less:\n {msg}");
        }

        private WittyerVcfReader([NotNull] IVcfReader vcfReader, [NotNull] IReadOnlyDictionary<WittyerVariantType, InputSpec> inputSpec,
            IReadOnlyList<IVcfVariant> baseVariants, EvaluationMode mode)
        {
            _vcfReader = vcfReader;
            _inputSpec = inputSpec;
            _baseVariants = baseVariants;
            _mode = mode;
        }
    }
}