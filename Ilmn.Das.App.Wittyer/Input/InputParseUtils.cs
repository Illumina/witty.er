using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using Ilmn.Das.App.Wittyer.Vcf.Variants;
using Ilmn.Das.Std.AppUtils.Collections;
using Ilmn.Das.Std.AppUtils.Misc;
using JetBrains.Annotations;

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
        internal static IImmutableList<(uint size, bool skip)> ParseBinSizes([CanBeNull] string v)
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
            if (skip) binSizeString = binSizeString.Substring(1);
            return uint.TryParse(binSizeString, out var binSize)
                ? (binSize, skip)
                : throw new InvalidDataException($"Tried to input non-uint bin sizes using bin size arg {binSizeString}!");
        }

        /// <summary>
        /// Parse a basepair distance from a command line argument.
        /// </summary>
        /// <param name="v">A command line argument.</param>
        /// <returns>A basepair distance uint.</returns>
        internal static uint ParseBasepairDistance([NotNull] string v)
            => uint.TryParse(v, out var ret)
                ? ret
                : throw new InvalidDataException("Tried to input a non-uint for basepairOverlap!");

        /// <summary>
        /// Parse filters from a command line argument.
        /// </summary>
        /// <param name="v">A command line argument.</param>
        /// <returns>A set of filters.</returns>
        internal static IReadOnlyCollection<string> ParseFilters([NotNull] string v)
            => string.IsNullOrEmpty(v)
                ? ImmutableHashSet<string>.Empty
                : v.Split(InputDelimiter).Where(f => string.IsNullOrWhiteSpace(f)
                    ? throw new InvalidDataException("Tried to pass in whitespace as filters: " + v)
                    : true).ToImmutableHashSet();

        /// <summary>
        /// Parse percent distance from a command line argument.
        /// </summary>
        /// <param name="v">A command line argument.</param>
        /// <returns>A double percent distance.</returns>
        internal static double? ParsePercentDistance([CanBeNull] string v)
            => string.IsNullOrWhiteSpace(v)
                // needs the type otherwise, it assumes default(double)
                ? default(double?) 
                : (double.TryParse(v, out var ret)
                    ? (ret <= 0.0 ? throw new InvalidDataException("Tried to input a 0 or less percent overlap.") : ret)
                    : throw new InvalidDataException("Tried to input a non-double for percentOverlap!"));

        internal static IReadOnlyCollection<ISamplePair> ParseTruthToQuerySampleMap([NotNull] string s)
            => s.Trim() == string.Empty
                ? ImmutableList<ISamplePair>.Empty
                : s.Split(InputDelimiter).Select(map => map.Split(':'))
                    .Select(split => SamplePair.Create(split[0], split[1])).ToReadOnlyList();

        [NotNull]
        [ItemNotNull]
        internal static IEnumerable<WittyerType> ParseVariantTypes([NotNull] string v)
            => v.Split(InputDelimiter)
                .Select(s => WittyerType.TryParse(s, out var varType)
                    ? varType
                    : throw new InvalidDataException(
                        $"Unknown variant type '{s}' on command line.{Environment.NewLine}" +
                        $"Supported variant types: {string.Join(", ", WittyerType.AllTypes)}"));

        [CanBeNull]
        internal static IncludeBedFile ParseBedFile([CanBeNull] string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                return null;
            var file = filePath.ToFileInfo();
            if (!file.ExistsNow())
                throw new FileNotFoundException($"{filePath} not found!");
            return IncludeBedFile.CreateFromBedFile(file);
        }
    }
}