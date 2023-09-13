using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using Ilmn.Das.App.Wittyer.Infrastructure;
using Ilmn.Das.App.Wittyer.Json.JsonConverters;
using Ilmn.Das.App.Wittyer.Utilities;
using Ilmn.Das.App.Wittyer.Vcf.Variants;
using Ilmn.Das.Std.AppUtils.Collections;
using Ilmn.Das.Std.AppUtils.Comparers;
using Ilmn.Das.Std.VariantUtils.Vcf;
using JetBrains.Annotations;
using Newtonsoft.Json;
using static Ilmn.Das.App.Wittyer.Input.WittyerSettings.Parser.WittyerParameters;

namespace Ilmn.Das.App.Wittyer.Input
{
    /// <summary>
    /// Class containing a set of settings for a structural variant type, parsed from the config file or command line settings.
    /// </summary>
    public class InputSpec
    {
        /// <summary>
        /// Gets the VariantType
        /// </summary>
        [JsonConverter(typeof(ObjectConverter))]
        [JsonProperty(WittyerSettings.VariantTypeName)]
        public WittyerType VariantType { get; }

        /// <summary>
        /// Gets the bins.
        /// </summary>
        /// <value>
        /// The bins.
        /// </value>
        [JsonConverter(typeof(BinsConverter))]
        [JsonProperty(WittyerSettings.BinSizesName)]
        public IImmutableList<(uint size, bool skip)> BinSizes { get; }

        /// <summary>
        /// Gets the basepair distance.
        /// </summary>
        /// <value>
        /// The basepair distance.
        /// </value>
        [JsonIgnore]
        public uint BasepairDistance => (uint) Math.Round(AbsoluteThreshold);

        /// <summary>
        /// Gets the basepair distance.
        /// </summary>
        /// <value>
        /// The basepair distance.
        /// </value>
        [JsonProperty(WittyerSettings.BpDistanceName)]
        public decimal AbsoluteThreshold { get; }

        /// <summary>
        /// Gets the percentage distance.
        /// </summary>
        /// <value>
        /// The percentage distance.
        /// </value>
        [JsonProperty(WittyerSettings.PercentThresholdName)]
        public double? PercentThreshold { get; }

        /// <summary>
        /// Gets the included filters.
        /// </summary>
        /// <value>
        /// The included filters.
        /// </value>
        [JsonConverter(typeof(EnumerableConverter))]
        [JsonProperty(IncludedFiltersName)]
        public IReadOnlyCollection<string> IncludedFilters { get; }

        /// <summary>
        /// Gets the excluded filters.
        /// </summary>
        /// <value>
        /// The excluded filters.
        /// </value>
        [JsonConverter(typeof(EnumerableConverter))]
        [JsonProperty(ExcludedFiltersName)]
        public IReadOnlyCollection<string> ExcludedFilters { get; }

        /// <summary>
        /// The regions to analyze, specified by the include bed file. Null value means evaluate all of the regions.
        /// </summary>
        /// <value>
        /// The name of the include bed file.
        /// </value>
        [JsonConverter(typeof(ObjectConverter))]
        [JsonProperty(IncludeBedName)]
        public IncludeBedFile? IncludedRegions { get; }
        
        private InputSpec(WittyerType variantType, IImmutableList<(uint size, bool skip)> binSizes,
            decimal absoluteThreshold, double? percentThreshold,
            IReadOnlyCollection<string> excludedFilters, IReadOnlyCollection<string> includedFilters,
            IncludeBedFile? includeBed)
        {
            BinSizes = binSizes;
            AbsoluteThreshold = absoluteThreshold;
            PercentThreshold = percentThreshold;
            IncludedFilters = includedFilters;
            ExcludedFilters = excludedFilters;
            VariantType = variantType;
            IncludedRegions = includeBed;
        }

        private InputSpec(WittyerType variantType, string binSizes, string? bpDistance,
            string? percentDistance, string excludedFilters, string includedFilters,
            string? includeBed, string? absoluteThreshold,
            string? percentThreshold)
            : this(variantType, InputParseUtils.ParseBinSizes(binSizes),
                InputParseUtils.ParseAbsoluteThreshold(bpDistance ?? absoluteThreshold ?? throw new JsonSerializationException(
                    $"{WittyerSettings.AbsoluteThresholdName} (preferred) or {WittyerSettings.BpDistanceName} is required!")),
                variantType.HasLengths && percentDistance == null && percentThreshold == null ? throw new JsonSerializationException(
                    $"{WittyerSettings.PercentThresholdName} is required!") : InputParseUtils.ParseDouble(percentDistance ?? percentThreshold, WittyerSettings.PercentThresholdName),
                InputParseUtils.ParseFilters(excludedFilters), InputParseUtils.ParseFilters(includedFilters),
                InputParseUtils.ParseBedFile(includeBed))
        {
        }

        [JsonConstructor]
        private InputSpec(string variantType, string binSizes, string? bpDistance,
            string? percentDistance, string excludedFilters, string includedFilters,
            string? includeBed, string? absoluteThreshold,
            string? percentThreshold)
            : this(WittyerType.Parse(variantType), binSizes, bpDistance, percentDistance, excludedFilters, includedFilters, includeBed, absoluteThreshold, percentThreshold)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="InputSpec"/> class.
        /// </summary>
        /// <param name="variantType">Type of the variant.</param>
        /// <param name="binSizes">The bin sizes.</param>
        /// <param name="absoluteThreshold">The basepair distance.</param>
        /// <param name="percentDistance">The percent distance.</param>
        /// <param name="excludedFilters">The excluded filters.</param>
        /// <param name="includedFilters">The included filters.</param>
        /// <param name="includeBed">The bed file that contains regions to analyze.</param>
        public static InputSpec Create(WittyerType variantType,
            IImmutableList<(uint size, bool skip)> binSizes, decimal absoluteThreshold, double? percentDistance,
            IReadOnlyCollection<string> excludedFilters, IReadOnlyCollection<string> includedFilters,
            IncludeBedFile? includeBed)
            => new(variantType, binSizes, absoluteThreshold, percentDistance, excludedFilters,
                VerifyFiltersAndGetFinalIncluded(excludedFilters, includedFilters), includeBed);

        /// <summary>
        /// Generates Default input specs for the given types.
        /// <c>Note</c>: If CrossType is on, it will filter out CopyNumberVariant types, so it could return an empty Enumerable
        /// </summary>
        /// <param name="isCrossTypeOn">if set to <c>true</c> [is cross type on].</param>
        /// <param name="types">The types.</param>
        /// <param name="excludedFilters">The excluded filters.</param>
        /// <param name="includedFilters">The included filters.</param>
        /// <param name="bedFile">The name of the bed file that contains regions to analyze.</param>
        /// <returns></returns>
        [Pure]
        public static IEnumerable<InputSpec> GenerateDefaultInputSpecs(bool isCrossTypeOn,
            IEnumerable<WittyerType>? types = null,
            IReadOnlyCollection<string>? excludedFilters = null,
            IReadOnlyCollection<string>? includedFilters = null,
            IncludeBedFile? bedFile = null)
            => GenerateCustomInputSpecs(isCrossTypeOn, types)
                .Select(i =>
                {
                    var spec = (i.VariantType == WittyerType.Insertion
                        ? WittyerConstants.DefaultInsertionSpec
                        :
                        i.VariantType == WittyerType.CopyNumberTandemRepeat
                            ? WittyerConstants.DefaultTandemRepeatSpec
                            :
                            i.VariantType == WittyerType.CopyNumberTandemReference
                                ?
                                WittyerConstants.DefaultTandemReferenceSpec
                                :
                                i).ReplaceExcludeFilters(excludedFilters).ReplaceIncludeFilters(includedFilters);
                    return bedFile == null ? spec : spec.ReplaceBedFile(bedFile);
                });

        /// <summary>
        /// Generates custom <see cref="InputSpec"/>s
        /// </summary>
        /// <param name="isCrossTypeOn">if set to <c>true</c> [is cross type on].</param>
        /// <param name="types">The types.</param>
        /// <param name="binSizes">The bin sizes.</param>
        /// <param name="absoluteThreshold">The basepair distance.</param>
        /// <param name="percentThreshold">The percent distance.</param>
        /// <param name="excludedFilters">The excluded filters.</param>
        /// <param name="includedFilters">The included filters.</param>
        /// <param name="bedFile">The name of the bed file that contains regions to analyze.</param>
        [Pure]
        public static IEnumerable<InputSpec> GenerateCustomInputSpecs(bool isCrossTypeOn,
            IEnumerable<WittyerType>? types,
            IImmutableList<(uint size, bool skip)>? binSizes = null,
            decimal absoluteThreshold = WittyerConstants.DefaultAbsThreshold,
            double percentThreshold = WittyerConstants.DefaultPercentThreshold,
            IReadOnlyCollection<string>? excludedFilters = null,
            IReadOnlyCollection<string>? includedFilters = null,
            IncludeBedFile? bedFile = null)
            => GenerateTypes(isCrossTypeOn, types)
                .Select(s => Create(s,
                    s.HasBins ? binSizes ?? s.DefaultBins : ImmutableList<(uint size, bool skip)>.Empty,
                    absoluteThreshold, s.HasLengths ? percentThreshold : default(double?),
                    excludedFilters ?? WittyerConstants.DefaultExcludeFilters,
                    includedFilters ?? WittyerConstants.DefaultIncludeFilters,
                    bedFile));

        private static IEnumerable<WittyerType> GenerateTypes(bool isCrossTypeOn, IEnumerable<WittyerType>? types)
        {
            if (types == null)
                return WittyerType.AllTypes;
            if (!isCrossTypeOn)
                return types.Distinct();
            
            var types2 = new HashSet<WittyerType>(types);
            var types3 = new HashSet<WittyerType>();
            foreach (var categories in Quantify.CrossTypeCategories.Values)
            foreach (var category in categories)
            {
                if ((category.MainType == WittyerType.CopyNumberTandemRepeat
                     || category.SecondaryType == WittyerType.CopyNumberTandemRepeat)
                    && !types2.Contains(WittyerType.CopyNumberTandemRepeat))
                    continue;
                if (types2.Contains(category.MainType))
                {
                    if (types2.Contains(category.SecondaryType))
                        return types2;
                }
                else
                    types3.Add(category.MainType);
                if (!types2.Contains(category.SecondaryType))
                    types3.Add(category.SecondaryType);
            }

            return types2.Concat(types3).Distinct();
        }

        /// <summary>
        /// Creates a new instance of <see cref="InputSpec"/> with a new value for the <see cref="InputSpec.IncludedRegions"/>
        /// </summary>
        /// <param name="bedFile">The new <see cref="IncludeBedFile"/>, can be null.</param>
        [Pure]
        public InputSpec ReplaceBedFile(IncludeBedFile? bedFile) 
            => Create(VariantType, BinSizes, AbsoluteThreshold, PercentThreshold, ExcludedFilters,
                IncludedFilters, bedFile);

        /// <summary>
        /// Creates a new instance of <see cref="InputSpec"/> with a new value for the <see cref="InputSpec.IncludedFilters"/>
        /// </summary>
        [Pure]
        public InputSpec ReplaceIncludeFilters(
            IReadOnlyCollection<string>? includedFilters)
            => includedFilters == null
                ? this
                : Create(VariantType, BinSizes, AbsoluteThreshold, PercentThreshold,
                    ExcludedFilters, includedFilters, IncludedRegions);

        /// <summary>
        /// Creates a new instance of <see cref="InputSpec"/> with a new value for the <see cref="InputSpec.ExcludedFilters"/>
        /// </summary>
        [Pure]
        public InputSpec ReplaceExcludeFilters(
            IReadOnlyCollection<string>? excludedFilters)
            => excludedFilters == null
                ? this
                : Create(VariantType, BinSizes, AbsoluteThreshold, PercentThreshold,
                    excludedFilters, IncludedFilters, IncludedRegions);

        /// <summary>
        /// Creates an IEnumerable of <see cref="InputSpec"/>s with a possible override of the <see cref="InputSpec.IncludedRegions"/>
        /// </summary>
        [Pure]
        public static IEnumerable<InputSpec> CreateSpecsFromString(
            string configText, IncludeBedFile? bedFileOverride, bool isCrossTypeOn)
        {
            var parsed = JsonConvert
                             .DeserializeObject<IEnumerable<InputSpec>>(configText, InputSpecConverter.Create())
                             ?.Select(x => bedFileOverride == null ? x : x.ReplaceBedFile(bedFileOverride)) ??
                         Enumerable.Empty<InputSpec>();
            if (!isCrossTypeOn)
                return parsed;

            var dict = parsed.ToDictionary(p => p.VariantType, p => p);
            var dict2 = new Dictionary<WittyerType, InputSpec>();
            foreach (var categories in Quantify.CrossTypeCategories.Values)
            foreach (var category in categories)
            {
                if ((category.MainType == WittyerType.CopyNumberTandemRepeat
                     || category.SecondaryType == WittyerType.CopyNumberTandemRepeat)
                    && !dict.ContainsKey(WittyerType.CopyNumberTandemRepeat))
                    continue;
                if (dict.TryGetValue(category.MainType, out var spec))
                {
                    if (dict.ContainsKey(category.SecondaryType))
                        return dict.Values;
                    dict2[category.SecondaryType] = new InputSpec(category.SecondaryType, spec.BinSizes,
                        spec.AbsoluteThreshold, spec.PercentThreshold, spec.ExcludedFilters,
                        spec.IncludedFilters, spec.IncludedRegions);
                }
                else if (dict.TryGetValue(category.SecondaryType, out spec))
                    dict2[category.MainType] = new InputSpec(category.MainType, spec.BinSizes,
                        spec.AbsoluteThreshold, spec.PercentThreshold, spec.ExcludedFilters,
                        spec.IncludedFilters, spec.IncludedRegions);
            }

            return dict.Values.Concat(dict2.Values);
        }

        private static IReadOnlyCollection<string> VerifyFiltersAndGetFinalIncluded(
            IReadOnlyCollection<string> excludedFilters, IReadOnlyCollection<string>? includedFilters)
        {
            if (includedFilters == null)
            {
                if (excludedFilters.Contains(VcfConstants.PassFilter))
                    throw new InvalidDataException(
                        $"Included filters must be specified when {VcfConstants.PassFilter} is specified in the excluded filter!");
                includedFilters = WittyerConstants.DefaultIncludeFilters;
            }

            var badFilter = includedFilters.Concat(excludedFilters)
                .Where(s => string.IsNullOrEmpty(s) || s.Any(c =>
                                c == VcfConstants.FilterFieldDelimiterChar || char.IsWhiteSpace(c))).StringJoin("\", \"");

            if (badFilter.Length > 0)
                throw new InvalidDataException(
                    $"You tried to filter VCF entries with an invalid filter: \"{badFilter}\"");

            return includedFilters;
        }

        /// <inheritdoc />
        public override bool Equals(object? obj)
            => obj is InputSpec spec &&
               VariantType == spec.VariantType &&
               AbsoluteThreshold == spec.AbsoluteThreshold &&
               (spec.PercentThreshold == null && PercentThreshold == null ||
                spec.PercentThreshold != null && PercentThreshold != null &&
                Math.Abs(PercentThreshold.Value - spec.PercentThreshold.Value) < .000000000001) &&
               BinSizes.SequenceEqual(spec.BinSizes) &&
               IncludedFilters.IsScrambledEquals(spec.IncludedFilters) &&
               ExcludedFilters.IsScrambledEquals(spec.ExcludedFilters) && 
               Equals(IncludedRegions, spec.IncludedRegions);

        /// <inheritdoc />
        public override int GetHashCode()
        {
            var hashCode = 1621349179;
            hashCode = hashCode * -1521134295 + VariantType.GetHashCode();
            hashCode = hashCode * -1521134295 + AbsoluteThreshold.GetHashCode();
            hashCode = hashCode * -1521134295 + PercentThreshold?.GetHashCode() ?? 0;
            hashCode = hashCode * -1521134295 + HashCodeUtils.GenerateForEnumerablesStruct(BinSizes, true);
            hashCode = hashCode * -1521134295 + HashCodeUtils.GenerateForEnumerables(IncludedFilters, false);
            hashCode = hashCode * -1521134295 + HashCodeUtils.GenerateForEnumerables(ExcludedFilters, false);
            hashCode = hashCode * -1521134295 + IncludedRegions?.GetHashCode() ?? 0;
            return hashCode;
        }
    }
}