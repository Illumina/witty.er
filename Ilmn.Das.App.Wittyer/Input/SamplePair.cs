using System.Collections.Generic;
using System.IO;
using System.Linq;
using Ilmn.Das.App.Wittyer.Utilities;
using JetBrains.Annotations;
using EnumerableExtensions = Ilmn.Das.Std.AppUtils.Collections.EnumerableExtensions;

namespace Ilmn.Das.App.Wittyer.Input
{
    /// <summary>
    /// A data interface for a sample pair
    /// </summary>
    public interface ISamplePair
    {
        /// <summary>
        /// Gets the name of the query sample.
        /// </summary>
        /// <value>
        /// The name of the query sample.
        /// </value>
        string? QuerySampleName { get; }

        /// <summary>
        /// Gets the name of the truth sample.
        /// </summary>
        /// <value>
        /// The name of the truth sample.
        /// </value>
        string? TruthSampleName { get; }
    }

    /// <inheritdoc />
    /// <summary>
    /// The default implementation for <see cref="T:Ilmn.Das.App.Wittyer.Input.ISamplePair" />
    /// </summary>
    /// <seealso cref="T:Ilmn.Das.App.Wittyer.Input.ISamplePair" />
    public class SamplePair : ISamplePair
    {
        /// <summary>
        /// Invalid Path chars.
        /// </summary>
        public static readonly IReadOnlyCollection<char> InvalidChars = new HashSet<char>(Path.GetInvalidFileNameChars());

        /// <inheritdoc />
        public string QuerySampleName { get; }

        /// <inheritdoc />
        public string TruthSampleName { get; }

        private SamplePair(string? truth, string? query)
        {
            QuerySampleName = query;
            TruthSampleName = truth;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SamplePair"/> class.
        /// </summary>
        /// <param name="truthName">The truth.</param>
        /// <param name="queryName">The query.</param>
        [Pure]
        public static ISamplePair Create(string truthName, string queryName)
            => CreatePrivate(truthName, queryName);

        private static ISamplePair CreatePrivate(string? truthName, string? queryName)
        {
            var invalidNames = new Dictionary<string, string>();
            if (truthName?.Any(InvalidChars.Contains) ?? false)
                invalidNames[nameof(truthName)] = truthName;
            if (queryName?.Any(InvalidChars.Contains) ?? false)
                invalidNames[nameof(queryName)] = queryName;
            if (invalidNames.Count > 0)
                throw new InvalidDataException(
                    "Sample Name(s) contains invalid characters: " +
                    EnumerableExtensions.StringJoin(
                        invalidNames.Select(kvp => $"{kvp.Key} = '{kvp.Value}'"),
                        "; "));
            return new SamplePair(truthName, queryName);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SamplePair"/> class.
        /// </summary>
        /// <param name="truthName">The truth.</param>
        [Pure]
        public static ISamplePair CreateTruthOnly(string truthName)
            => CreatePrivate(truthName, null);

        /// <summary>
        /// Initializes a new instance of the <see cref="SamplePair"/> class.
        /// </summary>
        /// <param name="queryName">The truth.</param>
        [Pure]
        public static ISamplePair CreateQueryOnly(string queryName)
            => CreatePrivate(null, queryName);

        /// <summary>
        /// The pair with both values as null.
        /// </summary>
        public static readonly ISamplePair NullPair = CreatePrivate(null, null);

        internal static readonly ISamplePair Default
            = Create(WittyerConstants.WittyerMetaInfoLineKeys.DefaultTruthSampleName, WittyerConstants.WittyerMetaInfoLineKeys.DefaultQuerySampleName);

    }
}