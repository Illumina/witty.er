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
        private Winner(uint? start, uint? end)
        {
            Start = start;
            End = end;
        }

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

        internal static Winner Create(IInterval<uint>? variantInterval, 
            IReadOnlyList<(uint start, bool skip)>? bins)
        {
            if (bins == null || bins.Count == 0 || variantInterval == null)
                return new Winner(null, null); // means no bins.

            var index = GetBinIndex(bins, variantInterval);

            return index < 0
                ? Create(WittyerConstants.StartingBin, bins[0].start)
                : Create(bins[index].start, index < bins.Count - 1 ? bins[index + 1].start : default(uint?));
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Winner"/> class.
        /// </summary>
        /// <param name="svType">Type of the sv.</param>
        /// <param name="start">The start.</param>
        /// <param name="end">The end.</param>
        [Pure]
        public static Winner Create(uint start, uint? end)
            => new(start, end);

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

        public string ToWinTag(WittyerType svType)
        {
            if (Start == null)
                return $"{svType}|NA";
            var endString = End == null ? "+" : $"-{End}";
            return $"{svType}|{Start}{endString}";
        }
    }
}