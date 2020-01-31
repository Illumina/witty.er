using System;
using Ilmn.Das.App.Wittyer.Stats;
using JetBrains.Annotations;
using Newtonsoft.Json;

namespace Ilmn.Das.App.Wittyer.Json
{
    /// <summary>
    /// 
    /// </summary>
    public class BasicJsonStats : IEquatable<BasicJsonStats>
    {
        /// <summary>
        /// The Type of stats.  Either Base or Event
        /// </summary>
        public StatsType StatsType { get; }

        /// <summary>
        /// Gets the truth tp count.
        /// </summary>
        /// <value>
        /// The truth tp count.
        /// </value>
        public uint TruthTpCount { get; }

        /// <summary>
        /// Gets the truth function count.
        /// </summary>
        /// <value>
        /// The truth function count.
        /// </value>
        public uint TruthFnCount { get; }

        private readonly uint? _truthTotalCount;

        /// <summary>
        /// Gets the truth total count.
        /// </summary>
        /// <value>
        /// The truth total count.
        /// </value>
        public uint TruthTotalCount => _truthTotalCount ?? TruthTpCount + TruthFnCount;

        private readonly double? _recall;

        /// <summary>
        /// Gets the recall.
        /// </summary>
        /// <value>
        /// The recall.
        /// </value>
        public double Recall => _recall ?? (double) TruthTpCount / TruthTotalCount;

        /// <summary>
        /// Gets the query tp count.
        /// </summary>
        /// <value>
        /// The query tp count.
        /// </value>
        public uint QueryTpCount { get; }

        /// <summary>
        /// Gets the query fp count.
        /// </summary>
        /// <value>
        /// The query fp count.
        /// </value>
        public uint QueryFpCount { get; }

        private readonly uint? _queryTotalCount;

        /// <summary>
        /// Gets the query total  count.
        /// </summary>
        /// <value>
        /// The query overall count.
        /// </value>
        public uint QueryTotalCount => _queryTotalCount ?? QueryTpCount + QueryFpCount;

        private readonly double? _precision;

        /// <summary>
        /// Gets the precision.
        /// </summary>
        /// <value>
        /// The precision.
        /// </value>
        public double Precision => _precision ?? (double) QueryTpCount / QueryTotalCount;

        private readonly double? _fscore;

        /// <summary>
        /// Gets the F-score.
        /// </summary>
        /// <value>
        /// The F-score.
        /// </value>
        public double Fscore => _fscore ?? 2 * (Recall * Precision) / (Recall + Precision);

        [JsonConstructor]
        private BasicJsonStats(StatsType statsType, uint truthTpCount, uint truthFnCount, uint? truthTotalCount,
            double? recall, uint queryTpCount, uint queryFpCount, uint? queryTotalCount, double? precision,
            double? fscore)
        {
            StatsType = statsType;
            TruthTpCount = truthTpCount;
            TruthFnCount = truthFnCount;
            QueryFpCount = queryFpCount;
            _truthTotalCount = truthTotalCount;
            _recall = recall;
            _queryTotalCount = queryTotalCount;
            _precision = precision;
            QueryTpCount = queryTpCount;
            _fscore = fscore;
        }

        /// <summary>
        /// Creates an instance out of given parameters.
        /// </summary>
        [NotNull, Pure]
        public static BasicJsonStats Create(StatsType statsType, uint truthTpCount, uint falseNegativeCount,
            uint queryTpCount, uint falsePositiveCount)
            => new BasicJsonStats(statsType, truthTpCount, falseNegativeCount, null, null, queryTpCount,
                falsePositiveCount, null, null, null);

        /// <summary>
        /// The plus operator.
        /// </summary>
        /// <exception cref="InvalidOperationException"></exception>
        [NotNull]
        public static BasicJsonStats operator +([NotNull] BasicJsonStats left, [NotNull] BasicJsonStats right)
        {
            if (left.StatsType != right.StatsType)
                throw new InvalidOperationException(string.Join("\n", "Cannot add two different stats together: ",
                    left, right));
            return Create(left.StatsType, left.TruthTpCount + right.TruthTpCount,
                left.TruthFnCount + right.TruthFnCount, left.QueryTpCount + right.QueryTpCount,
                left.QueryFpCount + right.QueryFpCount);
        }

        #region Overrides of Object

        /// <inheritdoc />
        public override string ToString() => JsonConvert.SerializeObject(this);

        #endregion

        #region Equality members

        /// <inheritdoc />
        public bool Equals([CanBeNull] BasicJsonStats other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return StatsType == other.StatsType && TruthTpCount == other.TruthTpCount &&
                   TruthFnCount == other.TruthFnCount && QueryTpCount == other.QueryTpCount &&
                   QueryFpCount == other.QueryFpCount;
        }

        /// <inheritdoc />
        public override bool Equals([CanBeNull] object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return obj is BasicJsonStats cast && Equals(cast);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = (int) StatsType;
                hashCode = (hashCode * 397) ^ (int) TruthTpCount;
                hashCode = (hashCode * 397) ^ (int) TruthFnCount;
                hashCode = (hashCode * 397) ^ (int) QueryTpCount;
                hashCode = (hashCode * 397) ^ (int) QueryFpCount;
                return hashCode;
            }
        }

        /// <summary>
        /// Implements the operator ==.
        /// </summary>
        /// <param name="left">The left.</param>
        /// <param name="right">The right.</param>
        /// <returns>
        /// The result of the operator.
        /// </returns>
        public static bool operator ==([CanBeNull] BasicJsonStats left, [CanBeNull] BasicJsonStats right) =>
            Equals(left, right);

        /// <summary>
        /// Implements the operator !=.
        /// </summary>
        /// <param name="left">The left.</param>
        /// <param name="right">The right.</param>
        /// <returns>
        /// The result of the operator.
        /// </returns>
        public static bool operator !=([CanBeNull] BasicJsonStats left, [CanBeNull] BasicJsonStats right) =>
            !Equals(left, right);

        #endregion
    }
}