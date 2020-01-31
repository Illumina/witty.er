using System;
using Ilmn.Das.App.Wittyer.Utilities.Enums;
using Ilmn.Das.Std.AppUtils.Intervals;
using JetBrains.Annotations;

namespace Ilmn.Das.App.Wittyer.Vcf.Variants.Annotations
{
    /// <summary>
    ///     A class describing following tags in INFO field:
    ///     Who, What, Wow
    /// </summary>
    public class OverlapAnnotation : IComparable<OverlapAnnotation>
    {
        private OverlapAnnotation(uint who, MatchEnum what, [CanBeNull] IInterval<uint> wow, BorderDistance where,
            FailedReason why)
        {
            Who = who;
            What = what;
            Wow = wow;
            Where = where;
            Why = why;
        }

        public uint Who { get; }
        internal MatchEnum What { get; }
        [CanBeNull]
        public IInterval<uint> Wow { get; }
        public BorderDistance Where { get; }

        internal FailedReason Why { get; }

        public int CompareTo(OverlapAnnotation other)
        {
            if (ReferenceEquals(this, other)) return 0;
            if (ReferenceEquals(null, other)) return 1;
            var whereComparison = Where.CompareTo(other.Where);
            if (whereComparison != 0) return whereComparison;

            var whatComparison = What.CompareTo(other.What);
            if (whatComparison != 0) return whatComparison;

            var whoComparison = Who.CompareTo(other.Who);
            if (whoComparison != 0) return whoComparison;

            return Why.CompareTo(other.Why);
        }

        public static OverlapAnnotation Create(uint who, MatchEnum what, [CanBeNull] IInterval<uint> wow,
            BorderDistance where, FailedReason why) 
            => new OverlapAnnotation(who, what, wow, where, why);
    }
}