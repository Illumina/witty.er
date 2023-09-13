using System.Collections.Generic;
using Ilmn.Das.App.Wittyer.Utilities;
using Ilmn.Das.Std.AppUtils.Intervals;
using JetBrains.Annotations;

namespace Ilmn.Das.App.Wittyer.Vcf.Variants.Annotations
{
    /// <summary>
    ///     Win tag annotation in INFO field
    /// </summary>
    public class Winner
    {
        private Winner(WittyerType svType, uint? start, uint? end)
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
        public WittyerType SvType { get; }

        /// <summary>
        ///     Gets the start.
        /// </summary>
        /// <value>
        ///     The start.
        /// </value>
        public uint? Start { get; }

        /// <summary>
        ///     Gets the end.
        /// </summary>
        /// <value>
        ///     The end.
        /// </value>
        public uint? End { get; }

        internal static Winner Create(WittyerType svType, IInterval<uint>? variantInterval, 
            IReadOnlyList<(uint start, bool skip)>? bins)
        {
            if (bins == null || bins.Count == 0 || variantInterval == null)
                return new Winner(svType, null, null); // means no bins.

            var index = GetBinIndex(bins, variantInterval);

            return index < 0
                ? Create(svType, WittyerConstants.StartingBin, bins[0].start)
                : Create(svType, bins[index].start, index < bins.Count - 1 ? bins[index + 1].start : default(uint?));
        }

        [Pure]
        internal static Winner Create(WittyerType svType) 
            => Create(svType, WittyerConstants.StartingBin, null);

        /// <summary>
        /// Initializes a new instance of the <see cref="Winner"/> class.
        /// </summary>
        /// <param name="svType">Type of the sv.</param>
        /// <param name="start">The start.</param>
        /// <param name="end">The end.</param>
        [Pure]
        public static Winner Create(WittyerType svType, uint start, uint? end)
            => new(svType, start, end);

        private static int GetBinIndex(IReadOnlyList<(uint start, bool skip)> bins, IInterval<uint> interval)
            => GetBinIndex(bins, interval.GetLength());

        public static int GetBinIndex(IReadOnlyList<(uint start, bool skip)> bins, uint length)
        {
            var i = bins.Count - 1;
            for (; i >= 0; i--)
                if (bins[i].start <= length)
                    return i;
            return i;
        }

        /// <inheritdoc />
        public override string ToString()
        {
            if (Start == null)
                return $"{SvType}|NA";
            var endString = End == null ? "+" : $"-{End}";
            return $"{SvType}|{Start}{endString}";
        }
    }
}