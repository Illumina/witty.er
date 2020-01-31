using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
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
        [NotNull]
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
        [NotNull]
        public IImmutableList<(uint size, bool skip)> BinSizes { get; }

        /// <summary>
        /// Gets the basepair distance.
        /// </summary>
        /// <value>
        /// The basepair distance.
        /// </value>
        [JsonProperty(BpDistanceName)]
        public uint BasepairDistance { get; }

        /// <summary>
        /// Gets the percentage distance.
        /// </summary>
        /// <value>
        /// The percentage distance.
        /// </value>
        [JsonProperty(WittyerSettings.PercentDistanceName)]
        [CanBeNull]
        public double? PercentDistance { get; }

        /// <summary>
        /// Gets the included filters.
        /// </summary>
        /// <value>
        /// The included filters.
        /// </value>
        [JsonConverter(typeof(EnumerableConverter))]
        [JsonProperty(IncludedFiltersName)]
        [NotNull]
        public IReadOnlyCollection<string> IncludedFilters { get; }

        /// <summary>
        /// Gets the excluded filters.
        /// </summary>
        /// <value>
        /// The excluded filters.
        /// </value>
        [JsonConverter(typeof(EnumerableConverter))]
        [JsonProperty(ExcludedFiltersName)]
        [NotNull]
        public IReadOnlyCollection<string> ExcludedFilters { get; }

        /// <summary>
        /// The regions to analyze, specified by the include bed file. Null value means evaluate all of the regions.
        /// </summary>
        /// <value>
        /// The name of the include bed file.
        /// </value>
        [JsonConverter(typeof(ObjectConverter))]
        [JsonProperty(IncludeBedName)]
        [CanBeNull]
        public IncludeBedFile IncludedRegions { get; }
        
        private InputSpec([NotNull] WittyerType variantType, [NotNull] IImmutableList<(uint size, bool skip)> binSizes,
            uint basepairDistance, [CanBeNull] double? percentDistance,
            [NotNull] IReadOnlyCollection<string> excludedFilters, [NotNull] IReadOnlyCollection<string> includedFilters,
            [CanBeNull] IncludeBedFile includeBed)
        {
            BinSizes = binSizes;
            BasepairDistance = basepairDistance;
            PercentDistance = percentDistance;
            IncludedFilters = includedFilters;
            ExcludedFilters = excludedFilters;
            VariantType = variantType;
            IncludedRegions = includeBed;
        }

        [JsonConstructor]
        private InputSpec(string variantType, [NotNull] string binSizes, [NotNull] string bpDistance,
            [CanBeNull] string percentDistance, [NotNull] string excludedFilters, [NotNull] string includedFilters,
            [CanBeNull] string includeBed)
            : this(WittyerType.Parse(variantType), InputParseUtils.ParseBinSizes(binSizes),
                InputParseUtils.ParseBasepairDistance(bpDistance),
                InputParseUtils.ParsePercentDistance(percentDistance),
                InputParseUtils.ParseFilters(excludedFilters), InputParseUtils.ParseFilters(includedFilters),
                InputParseUtils.ParseBedFile(includeBed))
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="InputSpec"/> class.
        /// </summary>
        /// <param name="variantType">Type of the variant.</param>
        /// <param name="binSizes">The bin sizes.</param>
        /// <param name="basepairDistance">The basepair distance.</param>
        /// <param name="percentDistance">The percent distance.</param>
        /// <param name="excludedFilters">The excluded filters.</param>
        /// <param name="includedFilters">The included filters.</param>
        /// <param name="includeBed">The bed file that contains regions to analyze.</param>
        [NotNull]
        public static InputSpec Create([NotNull] WittyerType variantType,
            [NotNull] IImmutableList<(uint size, bool skip)> binSizes, uint basepairDistance, [CanBeNull] double? percentDistance,
            [NotNull] IReadOnlyCollection<string> excludedFilters, IReadOnlyCollection<string> includedFilters,
            [CanBeNull] IncludeBedFile includeBed)
            => new InputSpec(variantType, binSizes, basepairDistance, percentDistance, excludedFilters,
                VerifyFiltersAndGetFinalIncluded(excludedFilters, includedFilters), includeBed);

        /// <summary>
        /// Generates Default input specs for the given types.
        /// <c>Note</c>: If CrossType is on, it will filter out CopyNumberVariant types, so it could return an empty Enumerable
        /// </summary>
        /// <param name="isCrossTypeOff">if set to <c>true</c> if CrossType matching is off.</param>
        /// <param name="types">The types.</param>
        /// <returns></returns>
        [NotNull]
        [ItemNotNull]
        [Pure]
        public static IEnumerable<InputSpec> GenerateDefaultInputSpecs(bool isCrossTypeOff,
            [CanBeNull, ItemNotNull] IEnumerable<WittyerType> types = null)
            => GenerateCustomInputSpecs(isCrossTypeOff, types).Select(i =>
                i.VariantType == WittyerType.Insertion ? WittyerConstants.DefaultInsertionSpec : i);

        /// <summary>
        /// Generates custom <see cref="InputSpec"/>s
        /// <c>Note</c>: If CrossType is on, it will filter out CopyNumberVariant types, so it could return an empty Enumerable
        /// </summary>
        /// <param name="isCrossTypeOff">if set to <c>true</c> [is cross type off].</param>
        /// <param name="types">The types.</param>
        /// <param name="binSizes">The bin sizes.</param>
        /// <param name="basepairDistance">The basepair distance.</param>
        /// <param name="percentDistance">The percent distance.</param>
        /// <param name="excludedFilters">The excluded filters.</param>
        /// <param name="includedFilters">The included filters.</param>
        /// <param name="bedFile">The name of the bed file that contains regions to analyze.</param>
        [NotNull]
        [ItemNotNull]
        [Pure]
        public static IEnumerable<InputSpec> GenerateCustomInputSpecs(bool isCrossTypeOff,
            [CanBeNull, ItemNotNull] IEnumerable<WittyerType> types,
            [CanBeNull] IImmutableList<(uint size, bool skip)> binSizes = null,
            uint basepairDistance = WittyerConstants.DefaultBpOverlap,
            double percentDistance = WittyerConstants.DefaultPd,
            [CanBeNull] IReadOnlyCollection<string> excludedFilters = null,
            [CanBeNull] IReadOnlyCollection<string> includedFilters = null,
            [CanBeNull] IncludeBedFile bedFile = null)
            => (types ?? WittyerType.AllTypes)
                // if crosstype is on, then CopyNumberVariant should be filtered out.
                .Where(s => isCrossTypeOff || !s.IsCopyNumberVariant)
                .Select(s => Create(s, s.HasBins ? binSizes ?? WittyerConstants.DefaultBins : ImmutableList<(uint size, bool skip)>.Empty,
                    basepairDistance, s.HasLengths ? percentDistance : default(double?),
                    excludedFilters ?? WittyerConstants.DefaultExcludeFilters,
                    includedFilters ?? WittyerConstants.DefaultIncludeFilters,
                    bedFile));

        [NotNull]
        private static IReadOnlyCollection<string> VerifyFiltersAndGetFinalIncluded(
            [NotNull] IReadOnlyCollection<string> excludedFilters, [CanBeNull] IReadOnlyCollection<string> includedFilters)
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
        public override bool Equals([CanBeNull] object obj)
            => obj is InputSpec spec &&
               VariantType == spec.VariantType &&
               BasepairDistance == spec.BasepairDistance &&
               (spec.PercentDistance == null && PercentDistance == null ||
                spec.PercentDistance != null && PercentDistance != null &&
                Math.Abs(PercentDistance.Value - spec.PercentDistance.Value) < .000000000001) &&
               BinSizes.SequenceEqual(spec.BinSizes) &&
               IncludedFilters.IsScrambledEquals(spec.IncludedFilters) &&
               ExcludedFilters.IsScrambledEquals(spec.ExcludedFilters) && 
               Equals(IncludedRegions, spec.IncludedRegions);

        /// <inheritdoc />
        public override int GetHashCode()
        {
            var hashCode = 1621349179;
            hashCode = hashCode * -1521134295 + VariantType.GetHashCode();
            hashCode = hashCode * -1521134295 + BasepairDistance.GetHashCode();
            hashCode = hashCode * -1521134295 + PercentDistance?.GetHashCode() ?? 0;
            hashCode = hashCode * -1521134295 + HashCodeUtils.GenerateForEnumerablesStruct(BinSizes, true);
            hashCode = hashCode * -1521134295 + HashCodeUtils.GenerateForEnumerables(IncludedFilters, false);
            hashCode = hashCode * -1521134295 + HashCodeUtils.GenerateForEnumerables(ExcludedFilters, false);
            hashCode = hashCode * -1521134295 + IncludedRegions?.GetHashCode() ?? 0;
            return hashCode;
        }
    }
}