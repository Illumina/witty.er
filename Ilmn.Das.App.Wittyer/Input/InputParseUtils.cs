using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using Ilmn.Das.App.Wittyer.Vcf.Variants;
using Ilmn.Das.Std.AppUtils.Collections;
using Ilmn.Das.Std.AppUtils.Misc;

namespace Ilmn.Das.App.Wittyer.Input
{
    /// <summary>
    /// Utility class containing methods to pass into the 'parser' parameter of an <see cref="Core.InputUtils.NdeskOption.NdeskOption{T}"/> constructor.
    /// </summary>
    internal static class InputParseUtils
    {
        private const string InputDelimiter = ",";

        /// <summary>
        /// Parse bin sizes from a command line argument.
        /// </summary>
        /// <param name="v">A command line argument.</param>
        /// <returns>A list of bins sizes.</returns>
        internal static IImmutableList<(uint size, bool skip)> ParseBinSizes(string? v)
        {
            if (string.IsNullOrWhiteSpace(v))
                return ImmutableList<(uint size, bool skip)>.Empty;
            var result = v.Split(InputDelimiter).Select(ParseBinAndSkip).ToImmutableList();
            if (result.Count == 1 && result[0].size == 0)
                return ImmutableList<(uint size, bool skip)>.Empty;
            return result;
        }

        private static (uint size, bool skip) ParseBinAndSkip(string binSizeString)
        {
            var skip = binSizeString.StartsWith('!');
            if (skip) binSizeString = binSizeString[1..];
            return uint.TryParse(binSizeString, out var binSize)
                ? (binSize, skip)
                : throw new InvalidDataException($"Tried to input non-uint bin sizes using bin size arg {binSizeString}!");
        }

        /// <summary>
        /// Parse a absolute threshold from a command line argument.
        /// </summary>
        /// <param name="v">A command line argument.</param>
        internal static decimal ParseAbsoluteThreshold(string v)
            => decimal.TryParse(v, out var ret)
                ? ret
                : throw new InvalidDataException($"Tried to input a non-decimal for {WittyerSettings.AbsoluteThresholdName}: {v}!");

        /// <summary>
        /// Parse a alignment Similarity threshold from a command line argument.
        /// </summary>
        /// <param name="v">A command line argument.</param>
        internal static long ParseSimilarityThreshold(string v)
            => long.TryParse(v, out var ret)
                ? ret
                : throw new InvalidDataException($"Tried to input a non-long for {WittyerSettings.SimilarityThresholdName}: {v}!");

        /// <summary>
        /// Parse filters from a command line argument.
        /// </summary>
        /// <param name="v">A command line argument.</param>
        /// <returns>A set of filters.</returns>
        internal static IReadOnlyCollection<string> ParseFilters(string v)
            => string.IsNullOrEmpty(v)
                ? ImmutableHashSet<string>.Empty
                : v.Split(InputDelimiter).Where(f => string.IsNullOrWhiteSpace(f)
                    ? throw new InvalidDataException($"Tried to pass in whitespace as filters: {v}")
                    : true).ToImmutableHashSet();

        /// <summary>
        /// Parse double values
        /// </summary>
        /// <param name="v">A command line argument.</param>
        /// <returns>A double percent distance.</returns>
        internal static double? ParseDouble(string? v, string name)
            => string.IsNullOrWhiteSpace(v)
                ? default(double?) 
                : double.TryParse(v, out var ret)
                    ? ret < 0.0 ? throw new InvalidDataException($"Tried to input a 0 or less {name}: {v}") : ret
                    : throw new InvalidDataException($"Tried to input a non-double for {name}: {v}");

        internal static IReadOnlyCollection<ISamplePair> ParseTruthToQuerySampleMap(string s)
            => s.Trim() == string.Empty
                ? ImmutableList<ISamplePair>.Empty
                : s.Split(InputDelimiter).Select(map => map.Split(':'))
                    .Select(split => SamplePair.Create(split[0], split[1])).ToReadOnlyList();

        internal static IEnumerable<WittyerType> ParseVariantTypes(string v)
            => v.Split(InputDelimiter)
                .Select(s => WittyerType.TryParse(s, out var varType)
                    ? varType
                    : throw new InvalidDataException(
                        $"Unknown variant type '{s}' on command line.{Environment.NewLine}" +
                        $"Supported variant types: {string.Join(", ", WittyerType.AllTypes)}"));

        internal static IncludeBedFile? ParseBedFile(string? filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                return null;
            var file = filePath.ToFileInfo();
            return IncludeBedFile.CreateFromBedFile(file);
        }
    }
}