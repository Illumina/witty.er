using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using Ilmn.Das.App.Wittyer.Utilities;
using Ilmn.Das.App.Wittyer.Vcf.Variants;
using Ilmn.Das.Core.InputUtils.NdeskOption;
using Ilmn.Das.Std.AppUtils.Enums;
using Ilmn.Das.Std.AppUtils.Misc;
using JetBrains.Annotations;
using static Ilmn.Das.App.Wittyer.Utilities.WittyerConstants;

namespace Ilmn.Das.App.Wittyer.Input
{
    /// <inheritdoc />
    public class WittyerSettings : IWittyerSettings
    {
        /// <summary>
        /// The variantType name for settings and config json
        /// </summary>
        public const string VariantTypeName = "variantType";

        /// <summary>
        /// The percentThreshold name for settings and config json
        /// </summary>
        public const string PercentThresholdName = "percentThreshold";
        
        /// <summary>
        /// The absoluteThreshold name for settings and config json
        /// </summary>
        public const string AbsoluteThresholdName = "absoluteThreshold";
        
        /// <summary>
        /// The similarityThreshold name for settings
        /// </summary>
        public const string SimilarityThresholdName = "similarityThreshold";
        
        /// <summary>
        /// The maxMatches name for settings
        /// </summary>
        public const string MaxMatchesName = "maxMatches";
        
        /// <summary>
        /// The percentDistance name for settings and config json
        /// </summary>
        public const string PercentDistanceName = "percentDistance";
        
        /// <summary>
        /// The bpDistance name for settings and config json
        /// </summary>
        public const string BpDistanceName = "bpDistance";

        /// <summary>
        /// The binSizes name for settings and config json
        /// </summary>
        public const string BinSizesName = "binSizes";

        /// <inheritdoc />
        public DirectoryInfo OutputDirectory { get; }

        /// <inheritdoc />
        public FileInfo TruthVcf { get; }

        /// <inheritdoc />
        public FileInfo QueryVcf { get; }
        
        /// <inheritdoc />
        public IReadOnlyCollection<ISamplePair> SamplePairs { get; }

        /// <inheritdoc />
        public EvaluationMode Mode { get; }

        /// <inheritdoc />
        public IReadOnlyDictionary<WittyerType, InputSpec> InputSpecs { get; }

        /// <inheritdoc />
        public double SimilarityThreshold { get; }

        /// <inheritdoc />
        public byte? MaxMatches { get; }

        /// <summary>
        /// Validate that wittyer settings are properly formatted.
        /// </summary>
        /// <param name="wittyerSettings">Settings to validate.</param>
        /// <exception cref="ConstraintException"></exception>
        public static void ValidateSettings(IWittyerSettings wittyerSettings)
        {
            foreach (var variantType in wittyerSettings.InputSpecs.Keys)
            {
                var inputSpec = wittyerSettings.InputSpecs[variantType];
                VerifyBins(inputSpec, variantType);
            }

            void VerifyBins(InputSpec inputSpec, WittyerType variantType)
            {
                var bins = inputSpec.BinSizes;
                if (bins.Select(sizeSkipTuple => sizeSkipTuple.size).Distinct().Count() != bins.Count)
                {
                    throw new ConstraintException($"Duplicate bin sizes for variant type {variantType}.");
                }
                if (bins.Any() && bins.All(sizeSkipTuple => sizeSkipTuple.skip))
                {
                    throw new ConstraintException($"All bins marked as skipped for variant type {variantType}.");
                }
            }
        }

        private WittyerSettings(DirectoryInfo outputDirectory, FileInfo truthVcf, FileInfo queryVcf,
            IReadOnlyCollection<ISamplePair> samplePairs, EvaluationMode mode,
            IReadOnlyDictionary<WittyerType, InputSpec> inputSpecs, double similarityThreshold, byte? maxMatches)
        {
            OutputDirectory = outputDirectory;
            TruthVcf = truthVcf;
            QueryVcf = queryVcf;
            SamplePairs = samplePairs;
            Mode = mode;
            InputSpecs = inputSpecs;
            SimilarityThreshold = similarityThreshold;
            MaxMatches = maxMatches;
        }


        /// <summary>
        /// Create an <see cref="IWittyerSettings"/> object.
        /// </summary>
        /// <returns></returns>
        /// <exception cref="InvalidDataException"></exception>
        public static IWittyerSettings Create(DirectoryInfo outputDirectory, FileInfo truthVcf,
            FileInfo queryVcf,
            IReadOnlyCollection<ISamplePair> samplePairs, EvaluationMode mode,
            IReadOnlyDictionary<WittyerType, InputSpec> inputSpecs,
            double similarityThreshold = DefaultSimilarityThreshold, byte? maxMatches = DefaultMaxMatches)
        {
            if (!truthVcf.ExistsNow())
            {
                throw new InvalidDataException($"{truthVcf.FullName} does not exist!");
            }

            if (!queryVcf.ExistsNow())
            {
                throw new InvalidDataException($"{queryVcf.FullName} does not exist!");
            }

            if (outputDirectory.IsNotEmpty())
            {
                throw new InvalidDataException($"{outputDirectory.FullName} is not empty. Needs a clean output folder!!!");
            }

            return new WittyerSettings(outputDirectory, truthVcf, queryVcf, samplePairs, mode, inputSpecs,
                similarityThreshold, maxMatches);
        }

        /// <summary>
        /// A <see cref="WittyerSettings"/> <see cref="Parser"/>
        /// </summary>
        public static class Parser
        {
            /// <summary>
            /// Parses the specified arguments.
            /// </summary>
            /// <param name="args">The arguments.</param>
            /// <returns></returns>
            [Pure]
            public static IWittyerSettings Parse(string[] args) => WittyerParameters.ParsePrivate(args);


            internal class WittyerParameters : IWittyerSettings, IAdditionalNdeskOptions
            {
                [Pure]
                internal static IWittyerSettings ParsePrivate(string[] args)
                {
                    var parameters = new WittyerParameters();
                    var ndeskParser = new NdeskOptionParser(parameters, args);
                    var unparsed = ndeskParser.UnparsedArgs;

                    if (unparsed.Count > 0)
                        Console.WriteLine("Warning: Unparsed args: " + string.Join(" ", unparsed));

                    if (parameters._isDisplayVersion.Argument)
                    {
                        Console.WriteLine($"witty.er {CurrentVersion}");
                        Console.WriteLine(parameters.AdditionalHelpHeader);
                        Environment.Exit(0);
                    }

                    if (parameters._configFile.IsArgumentAssigned)
                    {
                        if (parameters._configOptions.Any(x => x.IsArgumentAssigned 
                                                               && x != parameters._bedFile))
                        {
                            Console.Error.WriteLine(
                                "Config file argument cannot be used in combination with arguments for bin sizes, basepair distance, " +
                                "percent distance, included filters, excluded filters, or variant types. Exiting.");
                            Environment.Exit(1);
                        }

                        var fi = parameters._configFile.Argument;
                        if (!fi.ExistsNow())
                        {
                            Console.WriteLine(
                                $"Config file {fi.FullName} did not exist! Generating default config file in its place. Rerun with exactly same command line and it will work this time.");
                            File.WriteAllText(fi.FullName, InputSpec.GenerateDefaultInputSpecs(
                                parameters.Mode == EvaluationMode.CrossTypeAndSimpleCounting).SerializeToString());
                            Environment.Exit(0);
                        }

                        var configText = File.ReadAllText(parameters._configFile.Argument.FullName);
                        if (string.IsNullOrWhiteSpace(configText))
                        {
                            Console.Error.WriteLine("Bad Config file passed in at " + fi.FullName);
                            Environment.Exit(1);
                        }

                        var bedFile = parameters._bedFile.IsArgumentAssigned ? parameters._bedFile.Argument : null;
                        parameters.InputSpecs = InputSpec.CreateSpecsFromString(
                                                        configText, bedFile,
                                                        parameters.Mode == EvaluationMode.CrossTypeAndSimpleCounting)?
                                                    .ToImmutableDictionary(x => x.VariantType, x => x)
                                                ?? ImmutableDictionary<WittyerType, InputSpec>.Empty;
                    }
                    else
                    {
                        var generatedSpecs = InputSpec.GenerateCustomInputSpecs(
                            parameters.Mode == EvaluationMode.CrossTypeAndSimpleCounting,
                            parameters._variantTypes.Argument, parameters._binSizes.Argument,
                            parameters._absoluteThreshold.Argument,  parameters._percentThreshold.Argument,
                            parameters._excludedFilters.Argument, 
                            parameters._includedFilters.Argument, parameters._bedFile.Argument);

                        if (!parameters._absoluteThreshold.IsArgumentAssigned) // keep default 
                            generatedSpecs = generatedSpecs.Select(i =>
                                i.VariantType == WittyerType.Insertion
                                    ? InputSpec.Create(i.VariantType, i.BinSizes, DefaultInsertionSpec.BasepairDistance,
                                        i.PercentThreshold, i.ExcludedFilters, i.IncludedFilters, i.IncludedRegions)
                                    : i.VariantType == WittyerType.CopyNumberTandemRepeat
                                        ? InputSpec.Create(i.VariantType, i.BinSizes,
                                            DefaultTandemRepeatSpec.BasepairDistance,
                                            i.PercentThreshold, i.ExcludedFilters, i.IncludedFilters, i.IncludedRegions)
                                        : i);
                        parameters.InputSpecs = generatedSpecs.ToImmutableDictionary(s => s.VariantType, s => s);
                    }

                    ValidateRequiredParameters(parameters._inputVcf);
                    ValidateRequiredParameters(parameters._truthVcf);
                    return parameters;
                }

                private static void ValidateRequiredParameters(INdeskOption parameter)
                {
                    if (!parameter.IsArgumentAssigned)
                    {
                        Console.Error.WriteLine($"Required parameter {parameter.Prototype} is missing.");
                        Environment.Exit(1);
                    }

                }

                #region cmdline option prototype

                /// <summary>
                /// The input vcf prototype
                /// </summary>
                private const string InputVcfPrototype = "i|inputVcf=";

                /// <summary>
                /// The truth vcf prototype
                /// </summary>
                private const string TruthVcfPrototype = "t|truthVcf=";

                /// <summary>
                /// The percent overlap prototype
                /// </summary>
                private const string PercentThresholdPrototype = $"pt|{PercentThresholdName}|pd|{PercentDistanceName}=";

                /// <summary>
                /// The basepair overlap prototype
                /// </summary>
                private const string AbsoluteThresholdPrototype = $"at|{AbsoluteThresholdName}|bpd|{BpDistanceName}=";

                /// <summary>
                /// The max matches prototype
                /// </summary>
                private const string MaxMatchesPrototype = $"mm|{MaxMatchesName}=";

                /// <summary>
                /// The percent overlap prototype
                /// </summary>
                private const string SimilarityThresholdPrototype = $"st|{SimilarityThresholdName}=";

                internal const string IncludedFiltersName = "includedFilters";

                /// <summary>
                /// The included filters prototype
                /// </summary>
                private const string IncludedFiltersPrototype = $"if|{IncludedFiltersName}=";

                internal const string ExcludedFiltersName = "excludedFilters";

                internal const string IncludeBedName = "includeBed";

                /// <summary>
                /// The excluded filters prototype
                /// </summary>
                private static readonly string ExcludedFiltersPrototype = $"ef|{ExcludedFiltersName}=";

                private static readonly string BinSizesPrototype = $"bs|{BinSizesName}=";


                private static readonly string VariantTypesPrototype = $"vt|{VariantTypeName}s=";

                private const string OutputDirPrototype = "o|outputDirectory=";

                private const string SampleMatchPrototype = "sp|samplePair=";

                private const string EvaluationModePrototype = "em|evaluationMode=";

                private const string VersionPrototype = "v|version";

                private const string ConfigFilePrototype = "c|configFile=";
                internal const string AlleleCountShortName = "ac";
                internal const string AlleleCountLongName = "allelecount";

                private static readonly string BedFilePrototype = $"b|{IncludeBedName}=";

                #endregion

                private readonly NdeskOption<FileInfo> _inputVcf = new(
                    InputVcfPrototype,
                    "Query vcf file (only support one file for now)",
                    v => v.ToFileInfo(),
                    v =>
                    {
                        if (!(v.Name.EndsWith(VcfGzSuffix) ||
                              v.Name.EndsWith(VcfSuffix)))
                        {
                            Console.Error.WriteLine($"{v.FullName} is not a vcf file!");
                            Environment.Exit(1);
                        }
                    });

                private readonly NdeskOption<FileInfo> _truthVcf = new(
                    TruthVcfPrototype,
                    "Truth vcf file (currently only support one file)",
                    v => v.ToFileInfo(),
                    v =>
                    {
                        if (!(v.Name.EndsWith(VcfGzSuffix) ||
                              v.Name.EndsWith(VcfSuffix)))
                            Console.Error.WriteLine($"{v.FullName} is not a vcf file!");
                    });

                private readonly NdeskOption<double> _percentThreshold = new(
                    PercentThresholdPrototype,
                    $"This is used for percentage thresholding.  For {WittyerType.CopyNumberTandemRepeat}s,"
                    + " this determines how large of a RepeatUnitCount (RUC) threshold to use (see README) for large"
                    + " Tandem Repeats.  For all the other SVs, in order to consider truth and query to be a match,"
                    + " the distance between both boundaries should be within a number that's proportional to total"
                    + $" SV length.  Input this as a decimal, by default is {DefaultPercentThreshold:N2}.  Please note that if you"
                    + " set this value in the command line, it overrides all the defaults.  If you want customization,"
                    + " please use the -c config file option.",
                    x => InputParseUtils.ParseDouble(x, PercentThresholdName) 
                         ?? throw new InvalidOperationException($"Somehow got null for {PercentThresholdName}"),
                    DefaultPercentThreshold);

                private readonly NdeskOption<decimal> _absoluteThreshold = new(
                    AbsoluteThresholdPrototype,
                    $"This is used for absolute thresholding.  For {WittyerType.CopyNumberTandemRepeat}s,"
                    + " this determines how large of a RepeatUnitCount (RUC) threshold to use (see README) for small"
                    + " Tandem Repeats.  For all the other SVs, this is the upper bound of boundary distance when"
                    + " comparing truth and query.  Please note that if you set this value in the command line, it"
                    + " overrides all the defaults.  If you want customization, please use the -c config file option.",
                    InputParseUtils.ParseAbsoluteThreshold,
                    DefaultAbsThreshold);

                private readonly NdeskOption<double> _similarityThreshold = new(
                    SimilarityThresholdPrototype,
                    $"This is used for sequence similarity thresholding.  For {WittyerType.Insertion}s, this"
                    + " determines how similar the alignment of the sequences must be before considered a match.",
                    it => InputParseUtils.ParseDouble(it, SimilarityThresholdName) ?? DefaultSimilarityThreshold,
                    DefaultSimilarityThreshold);

                private readonly NdeskOption<byte?> _maxMatches = new(
                    MaxMatchesPrototype,
                    "This is used for matching behavior." +
                    " A zero or negative means to match any number, but this will probably result in a very large vcf" +
                    $" and is not recommended. By default, it is {DefaultMaxMatches}." +
                    $" You can also provide '{AlleleCountShortName}' or '{AlleleCountLongName}'" +
                    " (without quotes, case insensitive), which will only match as many as the ploidy" +
                    " (assumes 2 if no ploidy given in the GT)." +
                    "  Furthermore, when matching for genotype, it can consider two heterozygous ref/variants" +
                    " to match with a single homozygous query variant entry (see README).",
                    x => byte.TryParse(x, out var maxMatches)
                        ? maxMatches
                        : x.ToLower() == AlleleCountShortName || x.ToLower() == AlleleCountLongName
                            ? null
                            : throw new ArgumentException(
                                $"{x} is not supported for {MaxMatchesName}, only non-negative integers" +
                                $" or '{AlleleCountShortName}' or '{AlleleCountLongName}' is supported."),
                    DefaultMaxMatches);

                private readonly NdeskOption<IReadOnlyCollection<string>> _includedFilters =
                    new(
                        IncludedFiltersPrototype,
                        "Comma separated list. Only variants contain these filters will be considered. By default is PASS. "
                        + "Use Empty String (\"\") to include all filters.",
                        InputParseUtils.ParseFilters,
                        DefaultIncludeFilters);

                private readonly NdeskOption<IReadOnlyCollection<string>> _excludedFilters =
                    new(
                        ExcludedFiltersPrototype,
                        "Comma separated list. Variants with any of these filters will be excluded in comparison. " +
                        "If any variants have filters conflicting with those in the included filters, excluded filters will take priority.",
                        InputParseUtils.ParseFilters,
                        DefaultExcludeFilters); //default empty list means nothing to exclude

                /// <summary>
                /// The offset that is +/- of the position for Breakends.
                /// </summary>
                private readonly NdeskOption<IImmutableList<(uint size, bool skip)>> _binSizes = new(
                    BinSizesPrototype,
                    "Comma separated list of bin sizes. Default is dependent on type, but for example,"
                    + $" {WittyerType.CopyNumberTandemRepeat}s use 100, 1000 which means there are 3 bins:"
                    + " [1, 100), [100,1000), [1000, >1000]. You can ignore certain bins in the calculation of"
                    + " performance statistics by prepending them with an '!'. For example, \"!1,1000,5000,!10000\""
                    + " will ignore classifications in the [1, 5000) and [10000+) bins when calculating and reporting"
                    + " statistics. Calls will still be made in these bins in the Wittyer vcf though.",
                    InputParseUtils.ParseBinSizes,
                    WittyerType.CopyNumberGain.DefaultBins);

                private readonly NdeskOption<DirectoryInfo> _outputDir = new(
                    OutputDirPrototype,
                    "Directory where all output files located",
                    s => s.ToDirectoryInfo(),
                    Directory.GetCurrentDirectory().ToDirectoryInfo());

                private readonly NdeskOption<IReadOnlyCollection<ISamplePair>> _truthToQuerySampleMap =
                    new(
                        SampleMatchPrototype,
                        "Optional unless either or both query and truth vcfs have more than one sample column." +
                        "Comma separated list of truth to query sample mappings using colon (:) as the delimiter. " +
                        "For convenience, if you just want the first column compared, you can just provide this option with empty contents instead." +
                        "For example, Truth1:Query1,NA12878:NA1278_S1",
                        InputParseUtils.ParseTruthToQuerySampleMap,
                        ImmutableList<ISamplePair>.Empty);

                private readonly NdeskOption<EvaluationMode> _evaluationMode = new(
                    EvaluationModePrototype,
                    $"Choose your evaluation mode, options are \'{EvaluationMode.GenotypeMatching}\' ({EvaluationMode.GenotypeMatching.ToStringDescription()}), " +
                    $"\'{EvaluationMode.SimpleCounting}\' ({EvaluationMode.SimpleCounting.ToStringDescription()}, the default), " +
                    $"\'{EvaluationMode.CrossTypeAndSimpleCounting}\' ({EvaluationMode.CrossTypeAndSimpleCounting.ToStringDescription()}), " +
                    $"by default it's using \'{EvaluationMode.SimpleCounting}\' mode, which does comparison by SvType and does not requires genotyping match",
                    v => ModesDictionary.TryGetValue(v.ToLowerInvariant(), out var mode)
                        ? mode
                        : throw new KeyNotFoundException($"Unsupported {nameof(EvaluationMode)}: {v}"),
                    EvaluationMode.GenotypeMatching);

                private static readonly IReadOnlyDictionary<string, EvaluationMode> ModesDictionary
                    = EnumUtils.GetValues<EvaluationMode>()
                        .ToImmutableDictionary(m => m.ToString().ToLowerInvariant(), m => m)
                        .AddRange(EnumUtils.GetValues<EvaluationMode>().ToDictionary(
                            m => m.ToStringDescription().ToLowerInvariant(), m => m));

                private readonly NdeskOption<bool> _isDisplayVersion = new(
                    VersionPrototype,
                    "witty.er version information",
                    s => s != null);

                private readonly NdeskOption<FileInfo> _configFile = new(
                    ConfigFilePrototype,
                    "Config file used to specify per variant type settings. Used in place of bin sizes, basepair distance, " +
                    "percent distance, included filters, excluded filters, variant types, and include bed arguments.",
                    v => v.ToFileInfo(),
                    fi =>
                    {
                        if (!fi.ExistsNow() || fi.Length != 0) return;

                        Console.Error.WriteLine($"Config file {fi.FullName} was empty!");
                        Environment.Exit(1);
                    });

                private readonly NdeskOption<IncludeBedFile?> _bedFile = new(
                    BedFilePrototype,
                    "Bed file used to specify regions included in the analysis. Variants not completely within bed file regions " +
                    "will be marked as not assessed. This parameter is optional, and by default all variants will be analyzed.",
                    InputParseUtils.ParseBedFile, default(IncludeBedFile));

                private readonly NdeskOption<IEnumerable<WittyerType>> _variantTypes =
                    new(
                        VariantTypesPrototype,
                        "Variant types included in the analysis.",
                        InputParseUtils.ParseVariantTypes,
                        WittyerType.AllTypes
                    );

                private readonly ImmutableHashSet<INdeskOption> _configOptions;

                public WittyerParameters()
                    => _configOptions = ImmutableHashSet.Create<INdeskOption>(_binSizes, _absoluteThreshold,
                        _percentThreshold, _includedFilters, _excludedFilters, _variantTypes, _bedFile);

                #region Implementation of IAdditionalNdeskOptions

                public IEnumerable<INdeskOption> GetAdditionalOptions()
                {
                    yield return _inputVcf;
                    yield return _truthVcf;
                    yield return _bedFile;
                    yield return _outputDir;
                    yield return _evaluationMode;
                    yield return _configFile;
                    yield return _percentThreshold;
                    yield return _absoluteThreshold;
                    yield return _similarityThreshold;
                    yield return _maxMatches;
                    yield return _binSizes;
                    yield return _includedFilters;
                    yield return _excludedFilters;
                    yield return _truthToQuerySampleMap;
                    yield return _variantTypes;
                    yield return _isDisplayVersion;
                }

                #endregion

                public string AdditionalHelpHeader
                {
                    get
                    {
                        var stringBuilder =
                            new StringBuilder(
                                "What is true? Thank you! Ernestly. A tool to evaluate structural variants against truthset.\n");
                        return stringBuilder.ToString();
                    }
                }

                public DirectoryInfo OutputDirectory => _outputDir.Argument;
                public FileInfo TruthVcf => _truthVcf.Argument;
                public FileInfo QueryVcf => _inputVcf.Argument;
                public IReadOnlyCollection<ISamplePair> SamplePairs => _truthToQuerySampleMap.Argument;
                public EvaluationMode Mode => _evaluationMode.Argument;

                public IReadOnlyDictionary<WittyerType, InputSpec> InputSpecs { get; private set; } 
                    = ImmutableDictionary<WittyerType, InputSpec>.Empty;

                public double SimilarityThreshold => _similarityThreshold.Argument;
                public byte? MaxMatches => _maxMatches.Argument;
            }
        }
    }
}