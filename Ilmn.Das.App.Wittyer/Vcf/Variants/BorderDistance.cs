using System;
using Ilmn.Das.App.Wittyer.Utilities;
using Ilmn.Das.Std.AppUtils.Collections;
using Ilmn.Das.Std.AppUtils.Misc;
using JetBrains.Annotations;

namespace Ilmn.Das.App.Wittyer.Vcf.Variants
{
    /// <inheritdoc />
    /// <summary>
    /// A data class for BorderDistance
    /// </summary>
    /// <seealso cref="T:Ilmn.Das.App.Wittyer.Vcf.Variants.BorderDistance" />
    public class BorderDistance : IComparable<BorderDistance>
    {
        private BorderDistance(uint posBorderLeft, uint posBorderRight, uint endBorderLeft, uint endBorderRight)
        {
            PosBorderLeft = posBorderLeft;
            PosBorderRight = posBorderRight;
            EndBorderLeft = endBorderLeft;
            EndBorderRight = endBorderRight;
        }

        /// <summary>
        /// Gets the position border left.
        /// </summary>
        /// <value>
        /// The position border left.
        /// </value>
        public uint PosBorderLeft { get; }

        /// <summary>
        /// Gets the position border right.
        /// </summary>
        /// <value>
        /// The position border right.
        /// </value>
        public uint PosBorderRight { get; }

        /// <summary>
        /// Gets the end border left.
        /// <c>Note:</c> For Breakends, this is the mate's Pos Border Left and for Insertions, this is the same as PosBorderLeft
        /// </summary>
        /// <value>
        /// The end border left.
        /// </value>
        public uint EndBorderLeft { get; }

        /// <summary>
        /// Gets the end border right.
        /// <c>Note:</c> For Breakends, this is the mate's Pos Border Right and for Insertions, this is the same as PosBorderRight
        /// </summary>
        /// <value>
        /// The end border right.
        /// </value>
        public uint EndBorderRight { get; }

        /// <summary>
        /// Gets the score.
        /// </summary>
        /// <value>
        /// The score.
        /// </value>
        public uint Score => PosBorderLeft + PosBorderRight + EndBorderLeft + EndBorderRight;

        /// <inheritdoc />
        public int CompareTo(BorderDistance other)
        {
            if (ReferenceEquals(this, other)) return 0;
            return other is null ? 1 : Score.CompareTo(other.Score);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="BorderDistance"/> class.
        /// </summary>
        /// <param name="posBorderLeft">The position border left.</param>
        /// <param name="posBorderRight">The position border right.</param>
        /// <param name="endBorderLeft">The end border left.</param>
        /// <param name="endBorderRight">The end border right.</param>
        [NotNull]
        public static BorderDistance Create(uint posBorderLeft, uint posBorderRight, uint endBorderLeft, uint endBorderRight)
            => new BorderDistance(posBorderLeft, posBorderRight, endBorderLeft, endBorderRight);

        /// <inheritdoc />
        [NotNull]
        public override string ToString()
            => PosBorderLeft.FollowedBy(PosBorderRight, EndBorderLeft, EndBorderRight)
                .StringJoin(WittyerConstants.BorderDistanceDelimiter);

        /// <summary>
        /// Initializes a new instance of the <see cref="BorderDistance"/> class.
        /// </summary>
        /// <param name="first">The first variant.</param>
        /// <param name="second">The second variant.</param>
        /// <returns></returns>
        [NotNull]
        public static BorderDistance CreateFromVariant([NotNull] IWittyerSimpleVariant first,
            [NotNull] IWittyerSimpleVariant second)
        {
            var lbl = GetDistance(first.CiPosInterval.Start, second.CiPosInterval.Start);
            var lbr = GetDistance(first.CiPosInterval.Stop, second.CiPosInterval.Stop);

            var rbl = GetDistance(first.CiEndInterval.Start, second.CiEndInterval.Start);
            var rbr = GetDistance(first.CiEndInterval.Stop, second.CiEndInterval.Stop);

            return Create(lbl, lbr, rbl, rbr);
        }

        internal static uint GetDistance(uint big, uint small) 
            => small > big ? small - big : big - small;
    }
}