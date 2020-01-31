using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using Ilmn.Das.App.Wittyer.Utilities;
using Ilmn.Das.Std.AppUtils.Collections;
using Ilmn.Das.Std.VariantUtils.Vcf;
using JetBrains.Annotations;

namespace Ilmn.Das.App.Wittyer.Input
{
    public class InputSpec
    {
        /// <summary>
        /// Gets the bins.
        /// </summary>
        /// <value>
        /// The bins.
        /// </value>
        public IReadOnlyList<uint> Bins { get; }

        /// <summary>
        /// Gets the basepair distance.
        /// </summary>
        /// <value>
        /// The basepair distance.
        /// </value>
        public uint BasepairDistance { get; }

        /// <summary>
        /// Gets the percentage distance.
        /// </summary>
        /// <value>
        /// The percentage distance.
        /// </value>
        public double PercentageDistance { get; }

        /// <summary>
        /// Gets the included filters.
        /// </summary>
        /// <value>
        /// The included filters.
        /// </value>
        public IImmutableSet<string> IncludedFilters { get; }

        /// <summary>
        /// Gets the excluded filters.
        /// </summary>
        /// <value>
        /// The excluded filters.
        /// </value>
        public IImmutableSet<string> ExcludedFilters { get; }

        private InputSpec(IReadOnlyList<uint> bins, uint basepairDistance, double percentageDistance,
            IImmutableSet<string> includedFilters, IImmutableSet<string> excludedFilters)
        {
            Bins = bins;
            BasepairDistance = basepairDistance;
            PercentageDistance = percentageDistance;
            IncludedFilters = includedFilters;
            ExcludedFilters = excludedFilters;
        }

        [NotNull]
        public static InputSpec Create([NotNull] IReadOnlyList<uint> bins, uint basepairDistance,
            double percentageDistance, [NotNull] IImmutableSet<string> excludedFilters,
            [CanBeNull] IImmutableSet<string> includedFilters)
        {
            //validate filters
            var finalIncluded = includedFilters;
            if (includedFilters == null)
            {
                if (excludedFilters.Contains(VcfConstants.PassFilter))
                    throw new InvalidDataException(
                        $"Included filters must be specified when {VcfConstants.PassFilter} is specified in the excluded filter!");
                finalIncluded = WittyerConstants.DefaultIncludeFilters;
            }

            if (finalIncluded.Contains(VcfConstants.PassFilter) && finalIncluded.Count > 1)
                throw new InvalidDataException("Cannot specify multiple include filters when including PASS filter!");

            var badFilter = finalIncluded.Concat(excludedFilters)
                .Where(s => string.IsNullOrEmpty(s) || s.Any(c =>
                                c == VcfConstants.FilterFieldDelimiterChar || char.IsWhiteSpace(c))).ToList();

            if (badFilter.Count > 0)
               throw new InvalidDataException(
                    $"You tried to filter VCF entries with an invalid filter: \"{badFilter.StringJoin("\", \"")}\"");

            //percentage distance validation
            if (percentageDistance < 0 || percentageDistance > 1)
                throw new InvalidDataException(
                    "Tried to enter a value that is outside of 0.0 ~ 1.0 range for percent overlap. Value: " +
                    percentageDistance);

            return new InputSpec(bins, basepairDistance, percentageDistance, finalIncluded, excludedFilters);
        }
    }
}
