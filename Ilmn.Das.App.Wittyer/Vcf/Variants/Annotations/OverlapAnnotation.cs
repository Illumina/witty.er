using System;
using System.Linq;
using Ilmn.Das.App.Wittyer.Utilities.Enums;
using Ilmn.Das.Std.AppUtils.Comparers;
using Ilmn.Das.Std.AppUtils.Intervals;
using JetBrains.Annotations;

namespace Ilmn.Das.App.Wittyer.Vcf.Variants.Annotations
{
    /// <inheritdoc />
    /// <summary>
    ///     A class describing following tags in INFO field:
    ///     Who, What, Wow
    /// </summary>
    public class OverlapAnnotation : IComparable<OverlapAnnotation>
    {
        private OverlapAnnotation(uint? who, MatchSet what, IInterval<uint>? wow, BorderDistance? where,
            FailedReason why)
        {
            Who = who;
            What = what;
            Wow = wow;
            Where = where;
            Why = why;
        }

        /// <summary>
        /// Gets the who aka who did this overlap with (it's an id that usually is the truth entry's Position).
        /// </summary>
        /// <value>
        /// The who.
        /// </value>
        public uint? Who { get; }

        internal MatchSet What { get; }

        /// <summary>
        /// Gets the wow aka Overlap Window, which tracks the interval that overlapped with the target.
        /// </summary>
        /// <value>
        /// The wow.
        /// </value>
        public IInterval<uint>? Wow { get; }

        /// <summary>
        /// Gets the where aka where the entry is relative to the target, how far it is from the borders.
        /// </summary>
        /// <value>
        /// The where.
        /// </value>
        public BorderDistance? Where { get; }

        internal FailedReason Why { get; }

        /// <inheritdoc />
        public int CompareTo(OverlapAnnotation? other)
        {
            if (ReferenceEquals(this, other)) return 0;
            if (other is null) return 1;
            var whereComparison = ComparerUtils.HandleNullComparison(Where, other.Where) ??
                                  Where!.CompareTo(other.Where!);
            if (whereComparison != 0) return whereComparison;

            var whatComparison = What.Sum(x => (int)x).CompareTo(other.What.Sum(x => (int)x)); // we need better matches to be sorted first.
            if (whatComparison != 0) return whatComparison;

            var whoComparison = ComparerUtils.HandleNullComparison(Who, other.Who) ??
                                Who!.Value.CompareTo(other.Who!.Value);
            return whoComparison != 0 ? whoComparison : Why.CompareTo(other.Why);
        }
        /// <summary>
        /// Initializes a new instance of the <see cref="OverlapAnnotation"/> class.
        /// </summary>
        /// <param name="who">The who.</param>
        /// <param name="what">The what.</param>
        /// <param name="wow">The wow.</param>
        /// <param name="where">The where.</param>
        /// <param name="why">The why.</param>
        [Pure]
        public static OverlapAnnotation Create(uint? who, MatchSet what, IInterval<uint>? wow,
            BorderDistance? where, FailedReason why) 
            => new(who, what, wow, where, why);
        
        [Pure]
        public static OverlapAnnotation Create(uint? who, IInterval<uint>? wow,
            BorderDistance? where, FailedReason why) 
            => new(who, MatchSet.Empty, wow, where, why);
    }
}