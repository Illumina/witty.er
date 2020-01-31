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
        private Winner([NotNull] WittyerType svType, uint start, uint? end)
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
        [NotNull]
        public WittyerType SvType { get; }

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
        internal static Winner Create([NotNull] WittyerType svType, [CanBeNull] IInterval<uint> variantInterval, 
            [CanBeNull] IReadOnlyList<uint> bins)
        {
            if (bins == null || bins.Count == 0)
                return Create(svType);

            if (variantInterval == null) return new Winner(svType, bins[bins.Count - 1], null); // means take the last bin.

            var index = GetBinIndex(variantInterval, bins);

            return index < 0
                ? Create(svType, WittyerConstants.StartingBin, bins[0])
                : Create(svType, bins[index], index < bins.Count - 1 ? bins[index + 1] : default(uint?));
        }

        [Pure]
        [NotNull]
        internal static Winner Create([NotNull] WittyerType svType) 
            => Create(svType, WittyerConstants.StartingBin, null);

        /// <summary>
        /// Initializes a new instance of the <see cref="Winner"/> class.
        /// </summary>
        /// <param name="svType">Type of the sv.</param>
        /// <param name="start">The start.</param>
        /// <param name="end">The end.</param>
        [Pure]
        [NotNull]
        public static Winner Create([NotNull] WittyerType svType, uint start, uint? end)
            => new Winner(svType, start, end);

        private static int GetBinIndex([NotNull] IInterval<uint> interval, [NotNull] IReadOnlyList<uint> bins)
        {
            var length = interval.GetLength();
            var i = bins.Count - 1;
            for (; i >= 0; i--)
                if (bins[i] <= length)
                    return i;
            return i;
        }

        /// <inheritdoc />
        [NotNull]
        public override string ToString()
        {
            var endString = End == null ? "+" : $"-{End}";
            return $"{SvType}|{Start}{endString}";
        }
    }
}