using System.Collections.Generic;
using Ilmn.Das.App.Wittyer.Utilities;
using Ilmn.Das.Std.AppUtils.Enums;
using Ilmn.Das.Std.AppUtils.Intervals;
using JetBrains.Annotations;

namespace Ilmn.Das.App.Wittyer.Vcf.Variants.Annotations
{
    /// <summary>
    ///     Win tag annotation in INFO field
    /// </summary>
    public class Winner
    {
        private Winner(WittyerVariantType svType, uint start, uint? end)
        {
            SvType = svType;
            Start = start;
            End = end;
        }

        /// <summary>
        ///     Gets the type of the sv.
        /// </summary>
        /// <value>
        ///     The type of the sv.
        /// </value>
        public WittyerVariantType SvType { get; }

        /// <summary>
        ///     Gets the start.
        /// </summary>
        /// <value>
        ///     The start.
        /// </value>
        public uint Start { get; }

        /// <summary>
        ///     Gets the end.
        /// </summary>
        /// <value>
        ///     The end.
        /// </value>
        public uint? End { get; }

        [NotNull]
        internal static Winner Create(WittyerVariantType svType, [CanBeNull] IInterval<uint> variantInterval, IReadOnlyList<uint> bins)
        {
            if (svType.Equals(WittyerVariantType.TranslocationBreakend) || bins.Count == 0)
                return Create(svType);

            if (variantInterval == null) return new Winner(svType, bins[bins.Count - 1], null); // means take the last bin.

            var index = GetBinIndex(variantInterval, bins);

            if (index < 0)
                return new Winner(svType, WittyerConstants.StartingBin, bins[0]);

            return index < bins.Count - 1
                ? new Winner(svType, bins[index], bins[index + 1])
                : new Winner(svType, bins[index], null);
        }

        [NotNull]
        internal static Winner Create(WittyerVariantType svType) => new Winner(svType, WittyerConstants.StartingBin, null);

        private static int GetBinIndex([NotNull] IInterval<uint> interval, [NotNull] IReadOnlyList<uint> bins)
        {
            var length = interval.GetLength();
            var i = bins.Count - 1;
            for (; i >= 0; i--)
                if (bins[i] <= length)
                    return i;
            return i;
        }

        [NotNull]
        public override string ToString()
        {
            var endString = End == null ? "+" : $"-{End}";
            return $"{SvType.ToStringDescription()}|{Start}{endString}";
        }
    }
}