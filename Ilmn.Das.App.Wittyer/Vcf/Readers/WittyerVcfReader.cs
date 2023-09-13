using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Ilmn.Das.App.Wittyer.Input;
using Ilmn.Das.App.Wittyer.Results;
using Ilmn.Das.App.Wittyer.Utilities;
using Ilmn.Das.App.Wittyer.Utilities.Enums;
using Ilmn.Das.App.Wittyer.Vcf.Variants;
using Ilmn.Das.App.Wittyer.Vcf.Variants.Breakend;
using Ilmn.Das.Core.Eithers;
using Ilmn.Das.Core.Eithers.Extensions;
using Ilmn.Das.Core.Tries;
using Ilmn.Das.Core.Tries.Extensions;
using Ilmn.Das.Std.AppUtils.Collections;
using Ilmn.Das.Std.AppUtils.Enums;
using Ilmn.Das.Std.AppUtils.Files.FileReader;
using Ilmn.Das.Std.BioinformaticUtils.GenomicFeatures;
using Ilmn.Das.Std.VariantUtils.SimpleVariants;
using Ilmn.Das.Std.VariantUtils.Vcf;
using Ilmn.Das.Std.VariantUtils.Vcf.Variants.Alleles.BreakEnds;
using Ilmn.Das.Std.VariantUtils.Vcf.Variants.Samples;
using SampleDictionaries = Ilmn.Das.App.Wittyer.Vcf.Samples.SampleDictionaries;

namespace Ilmn.Das.App.Wittyer.Vcf.Readers
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

        public IImmutableList<string> SampleNames 
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
        internal MutableWittyerResult GetQueryset(string? sampleName, GenomeType genomeType)
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
        internal (TruthForest forest, GenomeType genomeType) GetTruth(string? sampleName)
        {
            var truth = TruthForest.Create(sampleName, _vcfReader.Header);
            var genomeType = GetResult(sampleName, truth, GenomeType.Unknown);
            return (truth, genomeType ?? GenomeType.Unknown);
        }
        
        public static WittyerVcfReader Create(IVcfReader vcfReader, 
            IReadOnlyDictionary<WittyerType, InputSpec> inputSpec, EvaluationMode mode,
            string? sampleName, FileInfo originalFile)
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
                ($"Found {exceptions.Count} variants cannot be parsed in {originalFile.FullName}: first 5 or less:\n {msg}");
        }

        private WittyerVcfReader(IVcfReader vcfReader, IReadOnlyDictionary<WittyerType, InputSpec> inputSpec,
            IReadOnlyList<IVcfVariant> baseVariants, EvaluationMode mode)
        {
            _vcfReader = vcfReader;
            _inputSpec = inputSpec;
            _baseVariants = baseVariants;
            _mode = mode;
        }

        private GenomeType? GetResult(string? sampleName, 
            IMutableWittyerResult mutableResult, GenomeType targetType)
        {
            var bndSet = new Dictionary<IGeneralBnd, IVcfVariant>(BreakendPairComparer.Default);

            var errorList = new List<string>();
            GenomeType? genomeType = null;
            foreach (var baseVariant in _baseVariants)
            {
                genomeType ??= baseVariant.Contig.GetGenomeType();
                var convertGenomeType = baseVariant.ConvertGenomeType(targetType);
                if (convertGenomeType.Alts.Count > 0 && convertGenomeType.Alts[0].EndsWith(":0[") == true)
                    convertGenomeType = convertGenomeType.ToBuilder().SetAlts(convertGenomeType.Alts
                        .Select(x => x.Replace(":0[", ":1[")).ToReadOnlyList()).Build();
                foreach (var result in CreateVariants(convertGenomeType,
                    mutableResult.IsTruth, sampleName, _inputSpec, bndSet, errorList))
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

        internal static IEnumerable<IContigProvider> CreateVariants(IVcfVariant vcfVariant, bool isTruth,
            string? sampleName, IReadOnlyDictionary<WittyerType, InputSpec> inputSpecDict,
            IDictionary<IGeneralBnd, IVcfVariant> bndSet, List<string> errorList)
        {
            var sample = sampleName == null ? null : vcfVariant.Samples[sampleName];
            bool breakEarly;
            var any = false;
            
            for (var altIndex = 0; altIndex < vcfVariant.Alts.Count; altIndex++)
            {
                var alt = vcfVariant.Alts[altIndex];
                if (alt.IsNonRef())
                    continue;

                foreach (var contigProvider in CreateVariantFromAltIndex(vcfVariant, isTruth, sampleName, inputSpecDict,
                             bndSet, errorList, altIndex, sample, out breakEarly))
                {
                    any = true;
                    yield return contigProvider;
                    if (breakEarly)
                        break;
                }
            }

            if (any) yield break;

            foreach (var contigProvider in CreateVariantFromAltIndex(vcfVariant, isTruth, sampleName, inputSpecDict,
                         bndSet, errorList, null,
                         sample, out breakEarly))
                yield return contigProvider;
        }

        internal static IEnumerable<IContigProvider> CreateVariantFromAltIndex(IVcfVariant vcfVariant, bool isTruth,
            string? sampleName, IReadOnlyDictionary<WittyerType, InputSpec> inputSpecDict,
            IDictionary<IGeneralBnd, IVcfVariant> bndSet, ICollection<string> errorList, int? altIndex,
            IVcfSample? sample, out bool breakEarly)
        {
            var variants = new List<IContigProvider>(); // this is needed so we can use the out parameter
            breakEarly = false;
            var failedReason =
                WittyerType.ParseFromVariant(vcfVariant, sampleName, out var wittyerType, out var noSplit, altIndex ?? 0);
            if (noSplit)
                breakEarly = true;
            if (failedReason != FailedReason.Unset)
            {
                breakEarly = true;
                variants.Add(CreateUnsupportedVariant(vcfVariant, sample, failedReason, isTruth));
                return variants;
            }

            if (wittyerType == null)
                throw new InvalidDataException("svType should not be null with no failed reason");

            //User does not specify this SVTYPE in input spec, consider user want to exclude this particular SVTYPE comparison entirely
            if (!inputSpecDict.TryGetValue(wittyerType, out var inputSpec))
            {
                breakEarly = true;
                variants.Add(CreateUnsupportedVariant(vcfVariant, sample, FailedReason.VariantTypeSkipped, isTruth));
                return variants;
            }

            var isSupportedVariant = IsSupportedVariant();
            if (!isSupportedVariant.Equals(FailedReason.Unset))
            {
                breakEarly = true;
                variants.Add(CreateUnsupportedVariant(vcfVariant, sample, isSupportedVariant, isTruth));
                return variants;
            }

            var bpd = inputSpec.BasepairDistance;
            var pd = inputSpec.PercentThreshold;

            var bins = inputSpec.BinSizes;

            if (wittyerType == WittyerType.CopyNumberReference
                || wittyerType == WittyerType.CopyNumberTandemRepeat && vcfVariant.IsRefSite()) // ref site
            {
                breakEarly = true;
                variants.Add(WittyerVariantInternal.Create(vcfVariant, sample, wittyerType, bins, pd, bpd, altIndex));
                return variants;
            }

            if (wittyerType == WittyerType.TranslocationBreakend ||
                wittyerType == WittyerType.IntraChromosomeBreakend)
            {
                var currentBnd = GeneralBnd.CreateFromVariant(vcfVariant);

                //Note: this means the paired BND is found as a key in dictionary. Checkout the comparer for details
                //todo: this is a place to check when implementing single breakends.
                if (bndSet.TryGetValue(currentBnd, out var secondVariant))
                {
                    if (!bndSet.Remove(currentBnd))
                        throw new InvalidOperationException(
                            $"Cannot remove {secondVariant} from breakend dictionary when pair is found: {vcfVariant}! Find a developer to debug!");
                    variants.Add(WittyerBndInternal.Create(
                        vcfVariant, sample, wittyerType,
                        bins, bpd, pd, secondVariant));
                }
                else
                {
                    bndSet.Add(currentBnd, vcfVariant);
                    variants.Add(currentBnd);
                }

                return variants;
            }

            // now we are INS/DEL/DUP/CNV/VNTR
            IWittyerSimpleVariant VariantGeneratorFunc(IVcfVariant newV) =>
                wittyerType == WittyerType.Insertion
                    ? WittyerBndInternal.Create(newV, sample, wittyerType, bins, bpd, pd, newV) //insertion is basically using one same record as the both entries of the breakend pair
                    : WittyerVariantInternal.Create(newV, sample, wittyerType, bins, pd, bpd, altIndex);

            var altVariant = CreateVariantsOfSpecificTypes(vcfVariant, sample,
                errorList, wittyerType, altIndex, VariantGeneratorFunc);

            var breakEarlyLocal = breakEarly;
            var ret = altVariant
                .SelectOnFailure(it =>
                {
                    breakEarlyLocal = true;
                    return (IContigProvider?) CreateUnsupportedVariant(vcfVariant, sample, it, isTruth);
                }).GetEither();
            breakEarly = breakEarlyLocal;
            
            if (ret != null)
                variants.Add(ret);

            return variants;

            FailedReason IsSupportedVariant()
            {
                // Check filters.
                var includedFilters = inputSpec.IncludedFilters;
                var excludedFilters = inputSpec.ExcludedFilters;

                if (vcfVariant.Filters.Any(excludedFilters.Contains)
                    || includedFilters.Count > 0
                    && (vcfVariant.Filters.Count == 0 || !vcfVariant.Filters.Any(includedFilters.Contains)))
                    return FailedReason.FilteredBySettings;

                // SVLEN = 0 when they are supposed to have overlaps (svlen is needed for overlapping windows) are ignored
                if (wittyerType.HasOverlappingWindows
                    && vcfVariant.Info.TryGetValue(VcfConstants.EndTagKey, out var endString)
                    && vcfVariant.Position.ToString() == endString
                    && (altIndex == null
                        || TryGetSvLenForAltIndex(vcfVariant, altIndex.Value, out var svLenValue)
                        && svLenValue == "0"))
                    return FailedReason.InvalidSvLen;

                if (wittyerType != WittyerType.IntraChromosomeBreakend)
                    return sample == null
                           || !sample.SampleDictionary.TryGetValue(WittyerConstants.Ft, out var ft)
                           || ft.Equals(VcfConstants.PassFilter)
                        ? FailedReason.Unset
                        : FailedReason.FailedSampleFilter;
                
                // Bnd with pos and alt to be same position (temporarily to accomodate the situation of SVLEN=0 INV representing as bnd)
                var mate = SimpleBreakEnd.TryParse(vcfVariant.GetSingleAlt()).GetOrThrow();
                return vcfVariant.Position == mate.Position ? FailedReason.InvalidSvLen : FailedReason.Unset;
            }
        }

        private static bool TryGetSvLenForAltIndex(IVcfVariant vcfVariant, int altIndex, out string? svLen)
        {
            svLen = null;
            if (!vcfVariant.Info.TryGetValue(VcfConstants.SvLenKey, out var svLenString))
                return false;
            var svLenSplit = svLenString.Split(VcfConstants.InfoFieldValueDelimiter);
            svLen = altIndex >= svLenSplit.Length ? svLenSplit[0] : svLenSplit[altIndex];
            return true;
        }

        // null means to skip
        private static IEither<FailedReason, IWittyerSimpleVariant?> CreateVariantsOfSpecificTypes(IVcfVariant vcfVariant,
            IVcfSample? sample, ICollection<string> errorList,
            WittyerType wittyerType, int? altIndex,
            Func<IVcfVariant, IWittyerSimpleVariant> variantGeneratorFunc)
        {
            if (wittyerType == WittyerType.CopyNumberReference &&
                vcfVariant.Info.TryGetValue(VcfConstants.SvTypeKey, out var svTypeString) &&
                !WittyerConstants.BaseLevelStatsTypeStrings.Contains(svTypeString))
                // any non-DEL/DUP/CNV that is determined to be reference copy number is not supported.
                return EitherFactory.LeftFailure<FailedReason, IWittyerSimpleVariant?>(FailedReason.UnsupportedRefCall);

            try
            {
                if (vcfVariant.Alts.Count < 2
                    || vcfVariant.Alts.Count == 2 && vcfVariant.Alts[1] == VcfConstants.NonRefKey
                    || sample == null
                    || !vcfVariant.Samples[sample.SampleName].SampleDictionary
                        .TryGetValue(VcfConstants.GenotypeKey, out var gt))
                    return EitherFactory.RightSuccess<FailedReason, IWittyerSimpleVariant?>(variantGeneratorFunc(vcfVariant));
                
                if (altIndex != null && !gt.Contains((altIndex.Value + 1).ToString()))
                    return EitherFactory.RightSuccess<FailedReason, IWittyerSimpleVariant?>(null);
                
                var generator = wittyerType == WittyerType.CopyNumberTandemRepeat
                    ? GenerateMultiAllelicTandemRepeats(vcfVariant)
                    : GenerateMultiAllelicGeneral(vcfVariant);
                if (generator == null)
                    return EitherFactory.RightSuccess<FailedReason, IWittyerSimpleVariant?>(variantGeneratorFunc(vcfVariant));
                return generator.SelectMany(alleleGenerator => CreateSingleFromMultiAllelic(vcfVariant, sample, gt,
                    alleleGenerator, altIndex).Select(it => variantGeneratorFunc(it ?? vcfVariant)));
            }
            catch (Exception e)
            {
                if (errorList.Count <= MaxNonSupportedVariantToPrint)
                    errorList.Add(
                        new[] { "Exception caught:", e.ToString(), vcfVariant.ToString() }
                            .StringJoin(Environment.NewLine));
                return EitherFactory.LeftFailure<FailedReason, IWittyerSimpleVariant?>(FailedReason.Other);
            }
        }

        // null means to just return the normal variant.
        private static IEither<FailedReason, IVcfVariant?> CreateSingleFromMultiAllelic(
            IVcfVariant vcfVariant,
            IVcfSample sample,
            string gt,
            Func<int?, IEither<FailedReason, (VcfVariant.Builder builder, Dictionary<string, string> infoModifications)?>> alleleGenerator,
            int? altIndex) => alleleGenerator(altIndex)
                .Select(newVariantBuilder =>
                {
                    if (newVariantBuilder == null) return null;

                    var (ret, infoDict) = newVariantBuilder.Value;
                    if (altIndex != null)
                        ret = ret.SetAlts(vcfVariant.Alts[altIndex.Value]);

                    if (!infoDict.ContainsKey(VcfConstants.SvLenKey) && vcfVariant.Info.TryGetValue(VcfConstants.SvLenKey, out var svlenStr))
                    {
                        var svlenSplit = svlenStr!.Split(VcfConstants.InfoFieldValueDelimiter);
                        var svLenStrNew = altIndex >= svlenSplit.Length ? svlenSplit[0] : svlenSplit[altIndex ?? 0];
                        if (svLenStrNew.StartsWith("-"))
                            svLenStrNew = svLenStrNew[1..];
                        infoDict[VcfConstants.SvLenKey] = svlenSplit.Length == 1 
                                ? svLenStrNew
                                :
                                // parse to long, with anything not parseable being treated as "."
                                svlenSplit.Select((it, i) => i == altIndex ? null : it.TryParse<long>())
                                    .OfType<ITry<long>>()
                            .OrderBy(it => it.Select(Math.Abs).GetOrElse(long.MinValue))
                            .Select(it => it.Select(s => s.ToString()).GetOrElse(VcfConstants.MissingValueString))
                            .LastOrException() // at this point, we have either the longest svlen or we have nothing
                            .Select(it => $"{svLenStrNew}{VcfConstants.InfoFieldValueDelimiter}{it}")
                            .GetOrElse(svLenStrNew)!; // nothing means we don't need to reconstruct it.
                    }
                    foreach (var (key, value) in vcfVariant.Info)
                        if (!infoDict.ContainsKey(key))
                            infoDict[key] = value;
                    ret = ret.SetInfo(infoDict);

                    var ids = vcfVariant.Ids.ToList();
                    var altIndexString = (altIndex + 1).ToString();
                    ids.Add($"{WittyerConstants.SplitAlleleIdPrefix}{altIndexString}");
                    ret = ret.SetIds(ids.ToArray());
                    
                    // ASSUMES no ref 0/0 calls!
                    var gtSplit = gt.Split(VcfConstants.GtPhasedValueDelimiter[0],
                        VcfConstants.GtUnphasedValueDelimiter[0]);
                    var newGt = new StringBuilder();
                    var strIndex = -1;
                    var hasNonRef = false;
                    foreach (var c in gtSplit)
                    {
                        if (c == altIndexString)
                            newGt.Append('1');
                        else if (c == "0")
                            newGt.Append('0');
                        else
                        {
                            newGt.Append('2');
                            hasNonRef = true;
                        }

                        strIndex += c.Length + 1;
                        if (strIndex < gt.Length)
                            newGt.Append(gt[strIndex]);
                    }

                    if (hasNonRef)
                        ret = altIndex == null
                            ? ret.SetAlts(VcfConstants.NonRefKey)
                            : ret.SetAlts(vcfVariant.Alts[altIndex.Value],
                                VcfConstants.NonRefKey);
                    var newGtString = newGt.ToString();
                    if (newGtString == "2/1")
                        newGtString = "1/2";
                    return ret
                        .SetSamples(builder =>
                            builder.MoveOnToDictionaries().SetSampleField(sample.SampleName,
                                    (VcfConstants.GenotypeKey, newGtString))
                                .Build()).Build();

                });


        /// <summary>
        /// SuccessRight means to create the unaltered as is and create an altered variant using the given func.
        /// LeftFailure means create unsupported.
        /// null means to just create the variant as is.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        internal delegate Func<(int? altIndex, IVcfVariant originalVariant, VcfVariant.Builder builder), IEither<FailedReason, VcfVariant.Builder?>> 
            MultiAllelicVariantsFuncGenerator<in T>(T variant, ICollection<FailedReason> failedReason);

        internal static IEither<FailedReason, Func<int?, IEither<FailedReason, (VcfVariant.Builder builder, Dictionary<string, string> infoModifications)?>>>?
            GenerateMultiAllelicGeneral(IVcfVariant vcfVariant)
        {
            IEither<FailedReason, (VcfVariant.Builder builder, Dictionary<string, string>
                infoModifications)?> PerAlleleBuilder(int? _) =>
                EitherFactory
                    .RightSuccess<FailedReason, (VcfVariant.Builder, Dictionary<string, string>)?>(
                        (vcfVariant.ToBuilder(), new Dictionary<string, string>()));

            return EitherFactory.RightSuccess<FailedReason, Func<int?, IEither<FailedReason, (VcfVariant.Builder, Dictionary<string, string>)?>>>(
                PerAlleleBuilder);

        }
        /// <summary>
        /// SuccessRight means to create the unaltered as is and create an altered variant using the given func.
        /// LeftFailure means create unsupported.
        /// null means to just create the variant as is.
        /// </summary>
        /// <returns></returns>
        internal static IEither<FailedReason, Func<int?, IEither<FailedReason, (VcfVariant.Builder builder, Dictionary<string, string> infoModifications)?>>>?
            GenerateMultiAllelicTandemRepeats(IVcfVariant vcfVariant)
        {
            if (!vcfVariant.Info.TryGetValue(WittyerConstants.RucInfoKey, out var ruc))
                return EitherFactory.LeftFailure<FailedReason, Func<int?, IEither<FailedReason, (VcfVariant.Builder, Dictionary<string, string>)?>>>(
                    FailedReason.RucNotFoundOrInvalid);

            // more than 1 ALT at this point
            var rucSplitMain = ruc.Split(VcfConstants.InfoFieldValueDelimiter);
            if (rucSplitMain.Length > vcfVariant.Alts.Count)
                return EitherFactory.LeftFailure<FailedReason, Func<int?, IEither<FailedReason, (VcfVariant.Builder, Dictionary<string, string>)?>>>(
                    FailedReason.RucAlleleCountDiff);
            return rucSplitMain.Length == 1
                ? null
                : EitherFactory
                    .RightSuccess<FailedReason, Func<int?, IEither<FailedReason, (VcfVariant.Builder, Dictionary<string, string>)?>>>(
                        i => PerAlleleBuilder(i, rucSplitMain));

            IEither<FailedReason, (VcfVariant.Builder, Dictionary<string, string>)?> PerAlleleBuilder(int? i, IReadOnlyList<string> rucSplit)
            {
                var infoDict = new Dictionary<string, string>();
                if (i == null)
                    return EitherFactory.LeftFailure<FailedReason, (VcfVariant.Builder, Dictionary<string, string>)?>(
                        FailedReason.Other);
                if (rucSplit.Count <= 2 && i.Value == 0)
                    return EitherFactory.RightSuccess<FailedReason, (VcfVariant.Builder, Dictionary<string, string>)?>((
                        vcfVariant.ToBuilder(), infoDict));

                var newRuc = $"{rucSplit[i.Value]}{VcfConstants.InfoFieldValueDelimiter}";
                if (rucSplit.Count == 2)
                    newRuc += $"{rucSplit[0]}";
                else
                {
                    var indexForCapture = i.Value;
                    var doubles = rucSplit
                        .Select((s, j) => (s, j))
                        .Where(it => it.j != indexForCapture)
                        .Select(it => it.s.TryParse<decimal>())
                        .EnumerateSuccesses().ToList();
                    if (doubles.Count != rucSplit.Count)
                        return EitherFactory.LeftFailure<FailedReason, (VcfVariant.Builder, Dictionary<string, string>)?>(
                            FailedReason.RucAlleleCountDiff);

                    newRuc += doubles.Sum().ToString(CultureInfo.CurrentCulture).TrimEnd('0');
                }

                if (i.Value != 0)
                    infoDict[WittyerConstants.RucInfoKey] = newRuc;
                return EitherFactory.RightSuccess<FailedReason, (VcfVariant.Builder, Dictionary<string, string>)?>(
                    (vcfVariant.ToBuilder(), infoDict));
            }
        }

        private static IVcfVariant CreateUnsupportedVariant(IVcfVariant baseVariant, IVcfSample? sample,
            FailedReason why, bool isTruth)
        {
            var realName = (isTruth ? SamplePair.Default.TruthSampleName : SamplePair.Default.QuerySampleName)
                           ?? throw new InvalidDataException(
                               $"Somehow, {nameof(SamplePair)}.{nameof(SamplePair.Default)} was null!!");
            var sampleBuilder = SampleDictionaries.CreateBuilder()
                .AddSample(realName).MoveOnToDictionaries();

            var dicts = (sample?.SampleDictionary ?? ImmutableDictionary<string, string>.Empty.AsEnumerable())?
                        .Select(kvp => (kvp.Key, kvp.Value))
                        .FollowedWith(
                            (WittyerConstants.WittyerMetaInfoLineKeys.Wit, NotAssessed),
                            (WittyerConstants.WittyerMetaInfoLineKeys.Why, why.ToString())) ??
                        Enumerable.Empty<(string, string)>();

            foreach (var tuple in dicts)
                sampleBuilder.SetSampleField(realName, tuple);

            return baseVariant.ToBuilder().SetSamples(sampleBuilder.Build()).Build();
        }
    }
}