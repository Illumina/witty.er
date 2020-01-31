using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text;
using Ilmn.Das.App.Wittyer.Utilities;
using Ilmn.Das.App.Wittyer.Vcf.Variants;
using Ilmn.Das.Core.InputUtils.NdeskOption;
using Ilmn.Das.Std.AppUtils.Collections;
using Ilmn.Das.Std.AppUtils.Enums;
using Ilmn.Das.Std.AppUtils.Misc;
using JetBrains.Annotations;

namespace Ilmn.Das.App.Wittyer.Input
{
    public class WittyerSettings : IWittyerSettings
    {
        public DirectoryInfo OutputDirectory { get; }
        public FileInfo TruthVcf { get; }
        public FileInfo QueryVcf { get; }
        public IReadOnlyCollection<ISamplePair> SamplePairs { get; }
        public EvaluationMode Mode { get; }
        public IReadOnlyDictionary<WittyerVariantType, InputSpec> InputSpecs { get; }

        private WittyerSettings([NotNull] DirectoryInfo outputDirectory, [NotNull] FileInfo truthVcf, FileInfo queryVcf,
            [NotNull] IReadOnlyCollection<ISamplePair> samplePairs, EvaluationMode mode,
            [NotNull] IReadOnlyDictionary<WittyerVariantType, InputSpec> inputSpecs)
        {
            OutputDirectory = outputDirectory;
            TruthVcf = truthVcf;
            QueryVcf = queryVcf;
            SamplePairs = samplePairs;
            Mode = mode;
            InputSpecs = inputSpecs;
        }

        [NotNull]
        public static WittyerSettings Create([NotNull] DirectoryInfo outputDirectory, [NotNull] FileInfo truthVcf,
            [NotNull] FileInfo queryVcf,
            [NotNull] IReadOnlyCollection<ISamplePair> samplePairs, EvaluationMode mode,
            [NotNull] IReadOnlyDictionary<WittyerVariantType, InputSpec> inputSpecs)
        {
            if (!truthVcf.ExistsNow())
            {
                throw new InvalidDataException($"{truthVcf.FullName} does not exist!");
            }


            if (!queryVcf.ExistsNow())
            {
                throw new InvalidDataException($"{queryVcf.FullName} does not exist!");
            }
                

            if (!outputDirectory.ExistsNow())
            {
                outputDirectory.Create();
            }

            if (outputDirectory.IsNotEmpty())
            {
                throw new InvalidDataException($"{outputDirectory.FullName} is not empty. Needs a clean output folder!!!");
            }
                
            return new WittyerSettings(outputDirectory, truthVcf, queryVcf, samplePairs, mode,
                inputSpecs);
        }

        public static class Parser
        {
            private const string InputDelimiter = ",";

            [NotNull, Pure]
            public static IWittyerSettings Parse(string[] args) => WittyerParameters.ParsePrivate(args);

            private class WittyerParameters : IWittyerSettings, IAdditionalNdeskOptions
            {


                [NotNull, Pure]
                internal static IWittyerSettings ParsePrivate(string[] args)
                {
                    var parameters = new WittyerParameters();
                    var ndeskParser = new NdeskOptionParser(parameters, args);
                    var unparsed = ndeskParser.UnparsedArgs;
                    if (unparsed.Count > 0)
                        Console.WriteLine("Warning: Unparsed args: " + String.Join(" ", unparsed));

                    if (!parameters._isDisplayVersion.Argument)
                    {
                        ValidateRequiredParameters(parameters._inputVcf);
                        ValidateRequiredParameters(parameters._truthVcf);
                        return parameters;
                    }

                    Console.WriteLine($"witty.er {WittyerConstants.CurrentVersion}");
                    Console.WriteLine(parameters.AdditionalHelpHeader);
                    Environment.Exit(0);
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
                private const string PercentDistancePrototype = "pd|percentDistance=";

                /// <summary>
                /// The basepair overlap prototype
                /// </summary>
                private const string BasepairDistancePrototype = "bpd|bpDistance=";

                /// <summary>
                /// The included filters prototype
                /// </summary>
                private const string IncludedFiltersPrototype = "if|includedFilters=";

                /// <summary>
                /// The exluded filters prototype
                /// </summary>
                private const string ExludedFiltersPrototype = "ef|excludedFilters=";

                private const string BinSizesPrototype = "bs|binSizes=";

                private const string OutputDirPrototype = "o|outputDirectory=";

                private const string SampleMatchPrototype = "sp|samplePair=";

                private const string EvaluationModePrototype = "em|evaluationMode=";

                private const string VersionPrototype = "v|version";

                #endregion

                private readonly NdeskOption<FileInfo> _inputVcf = new NdeskOption<FileInfo>(InputVcfPrototype,
                    "Query vcf file (only support one file for now)", v => new FileInfo(v),
                    v =>
                    {
                        if (!(v.Name.EndsWith(WittyerConstants.VcfGzSuffix) ||
                              v.Name.EndsWith(WittyerConstants.VcfSuffix)))
                        {
                            Console.Error.WriteLine($"{v.FullName} is not a vcf file!");
                            Environment.Exit(1);
                        }                         

                    });

                private readonly NdeskOption<FileInfo> _truthVcf = new NdeskOption<FileInfo>(TruthVcfPrototype,
                    "Truth vcf file (currently only support one file)", v => new FileInfo(v),
                    v =>
                    {
                        if (!(v.Name.EndsWith(WittyerConstants.VcfGzSuffix) ||
                              v.Name.EndsWith(WittyerConstants.VcfSuffix)))
                        {
                            Console.Error.WriteLine($"{v.FullName} is not a vcf file!");
                        }
                            
                    });

                private readonly NdeskOption<double> _percentDistance = new NdeskOption<double>(
                    PercentDistancePrototype,
                    "In order to consider truth and query to be the same, the distance between both boundaries should be within a number that's proportional to total SV length.  Input this as a decimal, by default is 0.05.",
                    v =>
                    {
                        // ReSharper disable once InlineOutVariableDeclaration
                        double ret;
                        var valid = double.TryParse(v, out ret);
                        if (!valid)
                            throw new InvalidDataException("Tried to input a non-double for percentOverlap!");
                        if (ret <= 0.0)
                            throw new InvalidDataException("Tried to input a 0 or less percent overlap.");
                        return ret;
                    }, WittyerConstants.DefaultPd);

                private readonly NdeskOption<uint> _basepairDistance = new NdeskOption<uint>(BasepairDistancePrototype,
                    "Upper bound of boundary distance when comparing truth and query. By default is 500bp.",
                    v =>
                    {
                        // ReSharper disable once InlineOutVariableDeclaration
                        uint ret;
                        var valid = uint.TryParse(v, out ret);
                        if (!valid)
                            throw new InvalidDataException("Tried to input a non-uint for basepairOverlap!");
                        return ret;
                    },
                    WittyerConstants.DefaultBpOverlap);

                private readonly NdeskOption<IImmutableSet<string>> _includedFilters =
                    new NdeskOption<IImmutableSet<string>>(IncludedFiltersPrototype,
                        "Comma separated list. Only variants contain these filters will be considered. by default is PASS",
                        s => s.IsNullOrEmpty()
                            ? ImmutableHashSet<string>.Empty
                            : s.Split(InputDelimiter).Where(f => !String.IsNullOrWhiteSpace(f)).ToImmutableHashSet(),
                        WittyerConstants.DefaultIncludeFilters);

                private readonly NdeskOption<IImmutableSet<string>> _excludedFilters =
                    new NdeskOption<IImmutableSet<string>>(ExludedFiltersPrototype,
                        "Comma separated list. Variants with any of these filters will be excluded in comparison. If any variants have filters conflicting with those in the Included filters, excluded filters will take the previlege.",
                        s => s.Split(InputDelimiter).ToImmutableHashSet(),
                        ImmutableHashSet<string>.Empty); //default empty list means nothing to exclude

                /// <summary>
                /// The offset that is +/- of the position for Breakends.
                /// </summary>

                private readonly NdeskOption<IReadOnlyList<uint>> _binSizes = new NdeskOption<IReadOnlyList<uint>>(
                    BinSizesPrototype,
                    "Comma separated list of bin sizes. Default is 1000, 10000 which means there are 3 bins: [1,1000), [1000,10000), [10000, >10000)",
                    v =>
                    {
                        var result = v.Split(InputDelimiter)
                            .Select(bin =>
                                uint.TryParse(bin, out var ret)
                                    ? ret
                                    : throw new InvalidDataException("Tried to input non-uint bin sizes: " + v))
                            .ToReadOnlyList();
                        if (result.Count == 1 && result[0] == 0)
                            return ImmutableList<uint>.Empty;
                        if (result.Count > 1 && result.Distinct().Count() != result.Count)
                            throw new InvalidDataException("Tried to input a list with repeating numbers: " + v);
                        return result;
                    },                        
                    WittyerConstants.DefaultBins);

                private readonly NdeskOption<DirectoryInfo> _outputDir = new NdeskOption<DirectoryInfo>(
                    OutputDirPrototype,
                    "Directory where all output files located", s => s.ToDirectoryInfo(),
                    Directory.GetCurrentDirectory().ToDirectoryInfo());

                private readonly NdeskOption<IReadOnlyCollection<ISamplePair>> _truthToQuerySampleMap =
                    new NdeskOption<IReadOnlyCollection<ISamplePair>>(SampleMatchPrototype,
                        "Optional unless either or both query and truth vcfs have more than one sample column." +
                        "Comma separated list of truth to query sample mappings using colon (:) as the delimiter. " +
                        "For convenience, if you just want the first column compared, you can just provide this option with empty contents instead." +
                        "For example, Truth1:Query1,NA12878:NA1278_S1",
                        s => s.Trim() == String.Empty
                            ? ImmutableList<ISamplePair>.Empty
                            : s.Split(InputDelimiter).Select(map => map.Split(':'))
                                .Select(split => SamplePair.Create(split[0], split[1])).ToReadOnlyList(),
                        ImmutableList<ISamplePair>.Empty);

                private readonly NdeskOption<EvaluationMode> _evaluationMode =
                    new NdeskOption<EvaluationMode>(EvaluationModePrototype,
                        $"Choose your evaluation mode, options are \'{EvaluationMode.Default}\' ({EvaluationMode.Default.ToStringDescription()}), " +
                        $"\'{EvaluationMode.SimpleCounting}\' ({EvaluationMode.SimpleCounting.ToStringDescription()}), " +
                        $"\'{EvaluationMode.CrossTypeAndSimpleCounting}\' ({EvaluationMode.CrossTypeAndSimpleCounting.ToStringDescription()}), " +
                        $"by default it's using \'{EvaluationMode.Default}\' mode, which does comparison by SvType and requires genotyping match",
                        s =>
                        {
                            // ReSharper disable once InlineOutVariableDeclaration
                            EvaluationMode ret;
                            var valid = s.TryParseEnumOrDescription(out ret);
                            if(!valid)
                            throw new InvalidDataException(
                                    $"Cannot parse {s} into a valid Evaluation Mode!!! Options are: " +
                                    $"{EvaluationMode.Default}, {EvaluationMode.SimpleCounting} and {EvaluationMode.CrossTypeAndSimpleCounting}");
                            return ret;
                        },
                        EvaluationMode.Default);               

                private readonly NdeskOption<bool> _isDisplayVersion =
                    new NdeskOption<bool>(VersionPrototype, "witty.er version information", s => s != null);

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
                }

                #endregion

                [NotNull]
                public string AdditionalHelpHeader
                {
                    get
                    {
                        var stringBuilder =
                            new StringBuilder(
                                "What is true? Thank you! Earnestly. A tool to evaluate structural variants against truthset.\n");
                        return stringBuilder.ToString();
                    }
                }

                public DirectoryInfo OutputDirectory => _outputDir.Argument;
                public FileInfo TruthVcf => _truthVcf.Argument;
                public FileInfo QueryVcf => _inputVcf.Argument;
                public IReadOnlyCollection<ISamplePair> SamplePairs => _truthToQuerySampleMap.Argument;
                public EvaluationMode Mode => _evaluationMode.Argument;

                //todo: this needs to be changed when we actually implement input spec file 
                public IReadOnlyDictionary<WittyerVariantType, InputSpec> InputSpecs => WittyerConstants.SupportedSvType
                    .ToDictionary(s => s,
                        s => InputSpec.Create(_binSizes.Argument, _basepairDistance.Argument, _percentDistance.Argument,
                            _excludedFilters.Argument, _includedFilters.Argument)).ToImmutableDictionary();
            }
        }

    }
}
