using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using Ilmn.Das.App.Wittyer.Input;
using Ilmn.Das.App.Wittyer.Results;
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
using Ilmn.Das.Std.VariantUtils.Vcf.Variants.Alleles.BreakEnds;
using Ilmn.Das.Std.VariantUtils.Vcf.Variants.Samples;
using JetBrains.Annotations;

namespace Ilmn.Das.App.Wittyer.Vcf
{
    /// <inheritdoc />
    /// <summary>
    /// Wittyer vcf reader. Should work for both truth and query
    /// </summary>
    /// <seealso cref="T:Ilmn.Das.Std.AppUtils.Files.FileReader.IFileReader`1" />
    internal class WittyerVcfReader : IFileReader<IMutableWittyerSimpleVariant>
    {
        private const int MaxNonSupportedVariantToPrint = 10;
        private readonly IVcfReader _vcfReader;
        private readonly IReadOnlyDictionary<WittyerType, InputSpec> _inputSpec;
        private readonly IReadOnlyList<IVcfVariant> _baseVariants;
        private readonly EvaluationMode _mode;
        private static readonly string NotAssessed = WitDecision.NotAssessed.ToStringDescription();

        [NotNull] public IImmutableList<string> SampleNames 
            => _vcfReader.Header.SampleNames;

        public IEnumerator<IMutableWittyerSimpleVariant> GetEnumerator() 
            => throw new InvalidOperationException("You are not supposed to call this function");

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        [Obsolete("Please do not use. Use 'GetQueryset()/GetTruth()' instead")]
        public IEnumerable<IMutableWittyerSimpleVariant> GetAllItems() 
            => throw new InvalidOperationException("You are not supposed to call this function");

        public IEnumerable<string> ReadAllLines() => _vcfReader.ReadAllLines();

        public FileInfo FileSource => _vcfReader.FileSource;

        /// <summary>
        /// Gets the queryset.
        /// </summary>
        /// <param name="sampleName">Name of the sample.</param>
        /// <param name="genomeType">Type of the genome.</param>
        /// <returns></returns>
        [NotNull]
        internal MutableWittyerResult GetQueryset([CanBeNull] string sampleName, GenomeType genomeType)
        {
            var query = new MutableWittyerResult(sampleName, false, _vcfReader.Header);
            GetResult(sampleName, query, genomeType);
            return query;
        }

        /// <summary>
        /// Gets the truth.
        /// </summary>
        /// <param name="sampleName">Name of the sample.</param>
        /// <returns></returns>
        /// <exception cref="InvalidDataException"></exception>
        internal (TruthForest forest, GenomeType genomeType) GetTruth([CanBeNull] string sampleName)
        {
            var truth = TruthForest.Create(sampleName, _vcfReader.Header);
            var genomeType = GetResult(sampleName, truth, GenomeType.Unknown);
            return (truth, genomeType ?? GenomeType.Unknown);
        }
        
        [NotNull]
        public static WittyerVcfReader Create([NotNull] IVcfReader vcfReader, 
            [NotNull] IReadOnlyDictionary<WittyerType, InputSpec> inputSpec, EvaluationMode mode,
            [CanBeNull] string sampleName)
        {
            if (inputSpec.SelectMany(kvp => kvp.Value.ExcludedFilters.Concat(kvp.Value.IncludedFilters)).Any(string.IsNullOrWhiteSpace))
                throw new InvalidDataException("Passed in empty or whitespace as a filter!");

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
                            sampleDict.SetSampleField(sample.SampleName, (kvp.Key, kvp.Value)));
                        variant = v.ToBuilder().SetSamples(sampleDict.Build()).Build();
                    }
                    successVariants.Add(variant);
                }).DoOnFailure(e => exceptions.Add(e));
            }

            if (exceptions.Count == 0)
                return new WittyerVcfReader(vcfReader, inputSpec, successVariants.AsReadOnly(), mode);

            var msg = exceptions.Take(5).Select(x => x.Message)
                .StringJoin("\n");
            throw new InvalidDataException
                ($"Found {exceptions.Count} variants cannot be parsed in {vcfReader.FileSource.FullName}: first 5 or less:\n {msg}");
        }

        private WittyerVcfReader([NotNull] IVcfReader vcfReader, [NotNull] IReadOnlyDictionary<WittyerType, InputSpec> inputSpec,
            IReadOnlyList<IVcfVariant> baseVariants, EvaluationMode mode)
        {
            _vcfReader = vcfReader;
            _inputSpec = inputSpec;
            _baseVariants = baseVariants;
            _mode = mode;
        }

        private GenomeType? GetResult([CanBeNull] string sampleName, 
            [NotNull] IMutableWittyerResult mutableResult, GenomeType targetType)
        {
            var bndSet = new Dictionary<IGeneralBnd, IVcfVariant>(BreakendPairComparer.Default);

            var errorList = new List<string>();
            GenomeType? genomeType = null;
            foreach (var baseVariant in _baseVariants)
            {
                if (genomeType == null)
                    genomeType = baseVariant.Contig.GetGenomeType();
                var sample = sampleName == null ? null : baseVariant.Samples[sampleName];
                var result = CreateVariant(baseVariant.ConvertGenomeType(targetType),
                    sample, mutableResult.IsTruth, sampleName, _inputSpec, bndSet, errorList, 
                    _mode == EvaluationMode.CrossTypeAndSimpleCounting);
                switch (result)
                {
                    //ugly implementation, first of breakend pair will return left as IGeneralBnd
                    case IGeneralBnd _:
                        continue;
                    case IVcfVariant vcfVariant:
                        mutableResult.AddUnsupported(vcfVariant);
                        break;
                    case IMutableWittyerSimpleVariant simpleVariant:
                        mutableResult.AddTarget(simpleVariant);
                        break;
                }
            }

            if (errorList.Count > 0)
            {
                var msg = (errorList.Count > MaxNonSupportedVariantToPrint
                    ? errorList.Take(MaxNonSupportedVariantToPrint)
                    : errorList).StringJoin("\n");

                Console.Error.WriteLine(
                    $"Fail to parse {errorList.Count} variants from truth, check first {MaxNonSupportedVariantToPrint} or less: \n{msg}");
            }

            if (bndSet.Count == 0) return genomeType ?? GenomeType.Unknown;

            Console.Error.WriteLine(
                $"Found single breakend in truth!!!! Those entries will be completely exclude from stats. First {MaxNonSupportedVariantToPrint} or less:");

            Console.Error.WriteLine(bndSet.Values.Take(MaxNonSupportedVariantToPrint).StringJoin("\n"));
            foreach (var kvp in bndSet)
                mutableResult.AddUnsupported(CreateUnsupportedVariant(kvp.Value,
                    sampleName == null ? null : kvp.Value.Samples[sampleName], FailedReason.UnpairedBnd, mutableResult.IsTruth));

            return genomeType ?? GenomeType.Unknown;

            
        }

        [NotNull]
        internal static IContigProvider CreateVariant([NotNull] IVcfVariant vcfVariant, [CanBeNull] IVcfSample sample, bool isTruth,
            [CanBeNull] string sampleName, IReadOnlyDictionary<WittyerType, InputSpec> inputSpecDict,
            IDictionary<IGeneralBnd, IVcfVariant> bndSet, List<string> errorList, bool isCrossTypeOn)
        {
            var failedReason = WittyerType.ParseFromVariant(vcfVariant, isCrossTypeOn, sampleName, out var svType);
            if (failedReason != null)
                return CreateUnsupportedVariant(vcfVariant, sample,
                    failedReason.Value == FailedReason.Unset
                        ? throw new ArgumentOutOfRangeException(
                            $"Got {nameof(FailedReason)}.{FailedReason.Unset} which means bug in {nameof(WittyerType.TryParse)}")
                        : failedReason.Value,
                    isTruth);

            if (svType == null)
                throw new InvalidDataException("svType should not be null with no failed reason");

            //User does not specify this SVTYPE in input spec, consider user want to exlude this particular SVTYPE comparison entirely
            if (!inputSpecDict.TryGetValue(svType, out var inputSpec))
                return CreateUnsupportedVariant(vcfVariant, sample, FailedReason.VariantTypeSkipped, isTruth);

            var isSupportedVariant = IsSupportedVariant();
            if (!isSupportedVariant.Equals(FailedReason.Unset))
                return CreateUnsupportedVariant(vcfVariant, sample, isSupportedVariant, isTruth);

            var bpd = inputSpec.BasepairDistance;
            var pd = inputSpec.PercentDistance;

            var bins = inputSpec.BinSizes;
            if (svType == WittyerType.Insertion)
                //insertion is basically using one same record as the both entries of the breakend pair
                return WittyerBndInternal.Create(vcfVariant,
                    sample, inputSpec.VariantType, bins.Select(sizeSkipTuple => sizeSkipTuple.size).ToReadOnlyList(), bpd, pd, vcfVariant);

            if (svType == WittyerType.CopyNumberReference &&
                vcfVariant.Info.TryGetValue(VcfConstants.SvTypeKey, out var svTypeString) &&
                !WittyerConstants.BaseLevelStatsTypeStrings.Contains(svTypeString))
                // any non-DEL/DUP/CNV that is determined to be reference copy number is not supported.
                return CreateUnsupportedVariant(vcfVariant, sample,
                    FailedReason.UnsupportedRefCall, isTruth);

            if (svType == WittyerType.TranslocationBreakend ||
                svType == WittyerType.IntraChromosomeBreakend)
            {
                var currentBnd = GeneralBnd.CreateFromVariant(vcfVariant);

                //Note: this means the paired BND is found as a key in dictionary. Checkout the comparer for details
                if (bndSet.TryGetValue(currentBnd, out var secondVariant))
                {
                    if (!bndSet.Remove(currentBnd))
                        throw new InvalidOperationException(
                            $"Cannot remove {secondVariant} from breakend dictionary when pair is found: {vcfVariant}! Find a developer to debug!");
                    return WittyerBndInternal.Create(vcfVariant, sample, svType, bins.Select(sizeSkipTuple => sizeSkipTuple.size).ToReadOnlyList(), bpd, pd, secondVariant);
                }

                bndSet.Add(currentBnd, vcfVariant);
                return currentBnd;
            }

            try
            {
                return WittyerVariantInternal.Create(vcfVariant, sample, svType, bins.Select(sizeSkipTuple => sizeSkipTuple.size).ToReadOnlyList(), pd, bpd);
            }
            catch (Exception e)
            {
                if (errorList.Count <= MaxNonSupportedVariantToPrint)
                    errorList.Add(
                        new[] {"Exception caught:", e.ToString(), vcfVariant.ToString()}
                            .StringJoin(Environment.NewLine));
                return CreateUnsupportedVariant(vcfVariant, sample, FailedReason.Other, isTruth);
            }

            FailedReason IsSupportedVariant()
            {
                // Check filters.
                IReadOnlyCollection<string> includedFilters, excludedFilters;
                if (isTruth)
                {
                    includedFilters = WittyerConstants.DefaultIncludeFilters;
                    excludedFilters = WittyerConstants.DefaultExcludeFilters;
                }
                else
                {
                    includedFilters = inputSpec.IncludedFilters;
                    excludedFilters = inputSpec.ExcludedFilters;
                }

                if (vcfVariant.Filters.Any(excludedFilters.Contains)
                    || includedFilters.Count > 0
                    && (vcfVariant.Filters.Count == 0 || !vcfVariant.Filters.Any(includedFilters.Contains)))
                    return FailedReason.FilteredBySettings;

                // SVLEN = 0 when they are supposed to have overlaps (svlen is needed for overlapping windows) are ignored
                if (svType.HasOverlappingWindows
                    && (vcfVariant.Info.TryGetValue(VcfConstants.EndTagKey, out var endString)
                        && vcfVariant.Position.ToString() == endString
                        || vcfVariant.Info.TryGetValue(VcfConstants.SvLenKey, out var svLenString)
                        && svLenString == "0"))
                    return FailedReason.InvalidSvLen;

                // Bnd with pos and alt to be same position (temporarily to accomendate the situation of SVLEN=0 INV representing as bnd)
                if (svType == WittyerType.IntraChromosomeBreakend)
                {
                    var mate = SimpleBreakEnd.TryParse(vcfVariant.GetSingleAlt()).GetOrThrow();
                    return vcfVariant.Position == mate.Position ? FailedReason.InvalidSvLen : FailedReason.Unset;
                }

                // todo: truth does not care about Sample FT tag, is that ok?
                var sampleFilterOk = isTruth || !includedFilters.Contains(VcfConstants.PassFilter) || !vcfVariant.IsPassFilter() || IsSampleFtPassFilter();

                if (!sampleFilterOk) return FailedReason.FailedSampleFilter;

                // used include bed and variant is completely within a single contig and the bed doesn't include the contig
                if (inputSpec.IncludedRegions != null && svType != WittyerType.TranslocationBreakend &&
                    !inputSpec.IncludedRegions.IntervalTree.ContainsKey(vcfVariant.Contig))
                    return FailedReason.OutsideBedRegion;

                return FailedReason.Unset;

                bool IsSampleFtPassFilter()
                    => sample != null && (!sample.SampleDictionary.TryGetValue(WittyerConstants.Ft, out var ft)
                                          || ft.Equals(VcfConstants.PassFilter));
            }
        }

        [NotNull]
        internal static IVcfVariant CreateUnsupportedVariant([NotNull] IVcfVariant baseVariant, [CanBeNull] IVcfSample sample,
            FailedReason why, bool isTruth)
        {
            var realName = (isTruth ? SamplePair.Default.TruthSampleName : SamplePair.Default.QuerySampleName)
                           ?? throw new InvalidDataException(
                               $"Somehow, {nameof(SamplePair)}.{nameof(SamplePair.Default)} was null!!");
            var sampleBuilder = SampleDictionaries.CreateBuilder()
                .AddSample(realName).MoveOnToDictionaries();

            var dicts = (sample?.SampleDictionary ?? ImmutableDictionary<string, string>.Empty.AsEnumerable())
                .Select(kvp => (kvp.Key, kvp.Value))
                .FollowedWith(
                    (WittyerConstants.WittyerMetaInfoLineKeys.Wit, NotAssessed),
                    (WittyerConstants.WittyerMetaInfoLineKeys.Why, why.ToString()));

            foreach (var tuple in dicts)
                sampleBuilder.SetSampleField(realName, tuple);

            return baseVariant.ToBuilder().SetSamples(sampleBuilder.Build()).Build();
        }
    }
}