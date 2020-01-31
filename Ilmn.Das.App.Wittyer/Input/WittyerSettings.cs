using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using Ilmn.Das.App.Wittyer.Json.JsonConverters;
using Ilmn.Das.App.Wittyer.Utilities;
using Ilmn.Das.App.Wittyer.Vcf.Variants;
using Ilmn.Das.Core.InputUtils.NdeskOption;
using Ilmn.Das.Std.AppUtils.Enums;
using Ilmn.Das.Std.AppUtils.Misc;
using JetBrains.Annotations;
using Newtonsoft.Json;
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
        /// The percentDistance name for settings and config json
        /// </summary>
        public const string PercentDistanceName = "percentDistance";

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

        /// <summary>
        /// Validate that wittyer settings are properly formatted.
        /// </summary>
        /// <param name="wittyerSettings">Settings to validate.</param>
        /// <exception cref="ConstraintException"></exception>
        public static void ValidateSettings([NotNull] IWittyerSettings wittyerSettings)
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

        private WittyerSettings([NotNull] DirectoryInfo outputDirectory, [NotNull] FileInfo truthVcf, FileInfo queryVcf,
            [NotNull] IReadOnlyCollection<ISamplePair> samplePairs, EvaluationMode mode,
            [NotNull] IReadOnlyDictionary<WittyerType, InputSpec> inputSpecs)
        {
            OutputDirectory = outputDirectory;
            TruthVcf = truthVcf;
            QueryVcf = queryVcf;
            SamplePairs = samplePairs;
            Mode = mode;
            InputSpecs = inputSpecs;
        }

        

        /// <summary>
        /// Create an <see cref="IWittyerSettings"/> object.
        /// </summary>
        /// <param name="outputDirectory"></param>
        /// <param name="truthVcf"></param>
        /// <param name="queryVcf"></param>
        /// <param name="samplePairs"></param>
        /// <param name="mode"></param>
        /// <param name="inputSpecs"></param>
        /// <returns></returns>
        /// <exception cref="InvalidDataException"></exception>
        [NotNull]
        public static IWittyerSettings Create([NotNull] DirectoryInfo outputDirectory, [NotNull] FileInfo truthVcf,
            [NotNull] FileInfo queryVcf,
            [NotNull] IReadOnlyCollection<ISamplePair> samplePairs, EvaluationMode mode,
            [NotNull] IReadOnlyDictionary<WittyerType, InputSpec> inputSpecs)
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

            return new WittyerSettings(outputDirectory, truthVcf, queryVcf, samplePairs, mode, inputSpecs);
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
            [NotNull, Pure]
            public static IWittyerSettings Parse(string[] args) => WittyerParameters.ParsePrivate(args);


            internal class WittyerParameters : IWittyerSettings, IAdditionalNdeskOptions
            {
                [NotNull, Pure]
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
                        if (parameters._configOptions.Any(x => x.IsArgumentAssigned))
                        {
                            Console.Error.WriteLine(
                                "Config file argument cannot be used in combination with arguments for bin sizes, basepair distance, " +
                                "percent distance, included filters, excluded filters, variant types, or include bed. Exiting.");
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

                        parameters.InputSpecs = JsonConvert
                            .DeserializeObject<IEnumerable<InputSpec>>(configText, InputSpecConverter.Create())
                            .ToImmutableDictionary(x => x.VariantType, x => x);
                    }
                    else
                    {

                        var generatedSpecs = InputSpec.GenerateCustomInputSpecs(
                            parameters.Mode != EvaluationMode.CrossTypeAndSimpleCounting,
                            parameters._variantTypes.Argument, parameters._binSizes.Argument,
                            parameters._basepairDistance.Argument,  parameters._percentDistance.Argument, 
                            parameters._excludedFilters.Argument, 
                            parameters._includedFilters.Argument, parameters._bedFile.Argument);

                        if (!parameters._basepairDistance.IsArgumentAssigned) // keep default 
                            generatedSpecs = generatedSpecs.Select(i =>
                                i.VariantType == WittyerType.Insertion
                                    ? InputSpec.Create(i.VariantType, i.BinSizes, DefaultInsertionSpec.BasepairDistance,
                                        i.PercentDistance, i.ExcludedFilters, i.IncludedFilters, i.IncludedRegions)
                                    : i);
                        parameters.InputSpecs = generatedSpecs.ToImmutableDictionary(s => s.VariantType, s => s);
                    }

                    if (parameters.Mode == EvaluationMode.CrossTypeAndSimpleCounting &&
                        parameters.InputSpecs.Keys.Any(s => s.IsCopyNumberVariant))
                        Console.WriteLine(
                            $"Warning: {WittyerType.CopyNumberGain} and/or {WittyerType.CopyNumberLoss}" +
                            $" setting given when using {EvaluationMode.CrossTypeAndSimpleCounting}" +
                            " mode!  This setting will be ignored.");

                    ValidateRequiredParameters(parameters._inputVcf);
                    ValidateRequiredParameters(parameters._truthVcf);
                    return parameters;
                }

                private static void ValidateRequiredParameters([NotNull] INdeskOption parameter)
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
                private static readonly string PercentDistancePrototype = $"pd|{PercentDistanceName}=";

                internal const string BpDistanceName = "bpDistance";

                /// <summary>
                /// The basepair overlap prototype
                /// </summary>
                private static readonly string BasepairDistancePrototype = $"bpd|{BpDistanceName}=";

                internal const string IncludedFiltersName = "includedFilters";
                /// <summary>
                /// The included filters prototype
                /// </summary>
                private static readonly string IncludedFiltersPrototype = $"if|{IncludedFiltersName}=";

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

                private static readonly string BedFilePrototype = $"b|{IncludeBedName}=";

                #endregion

                private readonly NdeskOption<FileInfo> _inputVcf = new NdeskOption<FileInfo>(
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

                private readonly NdeskOption<FileInfo> _truthVcf = new NdeskOption<FileInfo>(
                    TruthVcfPrototype,
                    "Truth vcf file (currently only support one file)",
                    v => v.ToFileInfo(),
                    v =>
                    {
                        if (!(v.Name.EndsWith(VcfGzSuffix) ||
                              v.Name.EndsWith(VcfSuffix)))
                            Console.Error.WriteLine($"{v.FullName} is not a vcf file!");
                    });

                private readonly NdeskOption<double> _percentDistance = new NdeskOption<double>(
                    PercentDistancePrototype,
                    "In order to consider truth and query to be the same, the distance between both boundaries should be within " +
                    $"a number that's proportional to total SV length.  Input this as a decimal, by default is {DefaultPd:N2}.",
                    x => InputParseUtils.ParsePercentDistance(x) 
                         ?? throw new InvalidOperationException("Somehow got null for " + PercentDistanceName),
                    DefaultPd);

                private readonly NdeskOption<uint> _basepairDistance = new NdeskOption<uint>(
                    BasepairDistancePrototype,
                    $"Upper bound of boundary distance when comparing truth and query. By default it is {DefaultBpOverlap}bp " +
                    $"for all types except for Insertions, which are {DefaultInsertionSpec.BasepairDistance}bp. Please note " +
                    "that if you set this value in the command line, it overrides all the defaults, so Insertions and other " +
                    "types will have the same bpd.  If you want customization, please use the -c config file option.",
                    InputParseUtils.ParseBasepairDistance,
                    DefaultBpOverlap);

                private readonly NdeskOption<IReadOnlyCollection<string>> _includedFilters =
                    new NdeskOption<IReadOnlyCollection<string>>(
                        IncludedFiltersPrototype,
                        "Comma separated list. Only variants contain these filters will be considered. By default is PASS. "
                        + "Use Empty String (\"\") to include all filters.",
                        InputParseUtils.ParseFilters,
                        DefaultIncludeFilters);

                private readonly NdeskOption<IReadOnlyCollection<string>> _excludedFilters =
                    new NdeskOption<IReadOnlyCollection<string>>(
                        ExcludedFiltersPrototype,
                        "Comma separated list. Variants with any of these filters will be excluded in comparison. " +
                        "If any variants have filters conflicting with those in the included filters, excluded filters will take priority.",
                        InputParseUtils.ParseFilters,
                        DefaultExcludeFilters); //default empty list means nothing to exclude

                /// <summary>
                /// The offset that is +/- of the position for Breakends.
                /// </summary>
                private readonly NdeskOption<IImmutableList<(uint size, bool skip)>> _binSizes = new NdeskOption<IImmutableList<(uint size, bool skip)>>(
                    BinSizesPrototype,
                    "Comma separated list of bin sizes. Default is 1000, 10000 which means there are 3 bins: [1,1000), [1000,10000), [10000, >10000). " +
                    "You can ignore certain bins in the calculation of performance statistics by prepending them with an '!'. For example, \"!1,1000,5000,!10000\" " +
                    "will ignore classifications in the [1, 5000) and [10000+) bins when calculating and reporting statistics. Calls will still be made in these " +
                    "bins in the Wittyer vcf though.",
                    InputParseUtils.ParseBinSizes,
                    DefaultBins);

                private readonly NdeskOption<DirectoryInfo> _outputDir = new NdeskOption<DirectoryInfo>(
                    OutputDirPrototype,
                    "Directory where all output files located",
                    s => s.ToDirectoryInfo(),
                    Directory.GetCurrentDirectory().ToDirectoryInfo());

                private readonly NdeskOption<IReadOnlyCollection<ISamplePair>> _truthToQuerySampleMap =
                    new NdeskOption<IReadOnlyCollection<ISamplePair>>(
                        SampleMatchPrototype,
                        "Optional unless either or both query and truth vcfs have more than one sample column." +
                        "Comma separated list of truth to query sample mappings using colon (:) as the delimiter. " +
                        "For convenience, if you just want the first column compared, you can just provide this option with empty contents instead." +
                        "For example, Truth1:Query1,NA12878:NA1278_S1",
                        InputParseUtils.ParseTruthToQuerySampleMap,
                        ImmutableList<ISamplePair>.Empty);

                private readonly NdeskOption<EvaluationMode> _evaluationMode = new NdeskOption<EvaluationMode>(
                    EvaluationModePrototype,
                    $"Choose your evaluation mode, options are \'{EvaluationMode.Default}\' ({EvaluationMode.Default.ToStringDescription()}), " +
                    $"\'{EvaluationMode.SimpleCounting}\' ({EvaluationMode.SimpleCounting.ToStringDescription()}), " +
                    $"\'{EvaluationMode.CrossTypeAndSimpleCounting}\' ({EvaluationMode.CrossTypeAndSimpleCounting.ToStringDescription()}), " +
                    $"by default it's using \'{EvaluationMode.Default}\' mode, which does comparison by SvType and requires genotyping match",
                    v => ModesDictionary.TryGetValue(v, out var mode)
                        ? mode
                        : throw new KeyNotFoundException($"Unsupported {nameof(EvaluationMode)}: {v}"),
                    EvaluationMode.Default);

                private static readonly IReadOnlyDictionary<string, EvaluationMode> ModesDictionary
                    = EnumUtils.GetValues<EvaluationMode>()
                        .ToImmutableDictionary(m => m.ToString(), m => m)
                        .Add("d", EvaluationMode.Default)
                        .Add("sc", EvaluationMode.SimpleCounting)
                        .Add("cts", EvaluationMode.CrossTypeAndSimpleCounting);

                private readonly NdeskOption<bool> _isDisplayVersion = new NdeskOption<bool>(
                    VersionPrototype,
                    "witty.er version information",
                    s => s != null);

                private readonly NdeskOption<FileInfo> _configFile = new NdeskOption<FileInfo>(
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

                private readonly NdeskOption<IncludeBedFile> _bedFile = new NdeskOption<IncludeBedFile>(
                    BedFilePrototype,
                    "Bed file used to specify regions included in the analysis. Variants not completely within bed file regions " +
                    "will be marked as not assessed. This parameter is optional, and by default all variants will be analyzed.",
                    InputParseUtils.ParseBedFile, default(IncludeBedFile));

                private readonly NdeskOption<IEnumerable<WittyerType>> _variantTypes =
                    new NdeskOption<IEnumerable<WittyerType>>(
                        VariantTypesPrototype,
                        "Variant types included in the analysis.",
                        InputParseUtils.ParseVariantTypes,
                        WittyerType.AllTypes
                    );

                private readonly ImmutableHashSet<INdeskOption> _configOptions;

                public WittyerParameters()
                    => _configOptions = ImmutableHashSet.Create<INdeskOption>(_binSizes, _basepairDistance,
                        _percentDistance, _includedFilters, _excludedFilters, _variantTypes, _bedFile);

                #region Implementation of IAdditionalNdeskOptions

                public IEnumerable<INdeskOption> GetAdditionalOptions()
                {
                    yield return _inputVcf;
                    yield return _truthVcf;
                    yield return _basepairDistance;
                    yield return _percentDistance;
                    yield return _binSizes;
                    yield return _includedFilters;
                    yield return _excludedFilters;
                    yield return _outputDir;
                    yield return _truthToQuerySampleMap;
                    yield return _evaluationMode;
                    yield return _isDisplayVersion;
                    yield return _configFile;
                    yield return _variantTypes;
                    yield return _bedFile;
                }

                #endregion

                [NotNull]
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
            }
        }
    }
}