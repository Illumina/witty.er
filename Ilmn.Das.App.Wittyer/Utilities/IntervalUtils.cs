using System;
using System.Collections.Immutable;
using System.Linq;
using Ilmn.Das.Std.AppUtils.Intervals;
using Ilmn.Das.Std.BioinformaticUtils.GenomicFeatures;
using Ilmn.Das.Std.VariantUtils.Vcf;
using Ilmn.Das.Std.VariantUtils.Vcf.Exceptions;
using Ilmn.Das.Std.VariantUtils.Vcf.Variants;
using JetBrains.Annotations;

namespace Ilmn.Das.App.Wittyer.Utilities
{
    internal static class IntervalUtils
    {
        /// <summary>
        /// Calculates the BND border interval.
        /// Breakend needs special logic 
        /// </summary>
        /// <param name="position">The position.</param>
        /// <param name="otherPosition">The other poisition.</param>
        /// <param name="confidentInterval">The confident interval.</param>
        /// <param name="percentageDistance">The percentage distance.</param>
        /// <param name="basepairDistance">The basepair distance.</param>
        /// <returns></returns>
        [NotNull]
        internal static IContigAndInterval CalculateBndBorderInterval([NotNull] this IContigAndPosition position,
            [NotNull] IContigAndPosition otherPosition,
            InclusiveInterval<int> confidentInterval,
            double percentageDistance, uint basepairDistance)
        {
            //inter-chromosome breakend
            var interval = position.Position.CalculateBasePairDistance(confidentInterval, basepairDistance);
            //intra-chromosome breakend
            if (position.Contig.Equals(otherPosition.Contig) && position.Position != otherPosition.Position) //Insertion does not count PD as well
            {
                interval = position.Position.CalculateBorderInterval(
                    new ClosedOpenInterval<uint>(Math.Min(position.Position, otherPosition.Position),
                        Math.Max(position.Position, otherPosition.Position)), confidentInterval,
                    percentageDistance, basepairDistance);
                
            }

            return ContigAndInterval.Create(position.Contig, interval.Start, interval.Stop);
        }


        /// <summary>
        /// Calculates the border distance interval using POS/END, CIPOS/CIEND, PD and BPD
        /// </summary>
        /// <param name="baseInterval"></param>
        /// <param name="confidentInterval"></param>
        /// <param name="percentageDistance">The percentage distance.</param>
        /// <param name="basepairDistance">The basepair distance.</param>
        /// <param name="position"></param>
        /// <returns></returns>
        [NotNull]
        internal static InclusiveInterval<uint> CalculateBorderInterval(this uint position, [NotNull] IInterval<uint> baseInterval,
            InclusiveInterval<int> confidentInterval,
            double percentageDistance, uint basepairDistance)
        {
            var pd = (uint) Math.Round(baseInterval.GetLength() * percentageDistance, MidpointRounding.AwayFromZero);
            return pd < basepairDistance
                ? position.ConvertPositionToInterval(pd, pd)
                : position.CalculateBasePairDistance(confidentInterval, basepairDistance);
        }

        [NotNull]
        private static InclusiveInterval<uint> CalculateBasePairDistance(this uint position,
            [NotNull] InclusiveInterval<int> confidentialInterval, uint basePairInterval)
        {
            var start = (int) Math.Min(confidentialInterval.Start, -basePairInterval);
            if (start < 0)
                start = -start;
            var stop = (uint) Math.Max(confidentialInterval.Stop, basePairInterval);
            return position.ConvertPositionToInterval((uint) start, stop);
        }

        [NotNull]
        private static InclusiveInterval<uint> ConvertPositionToInterval(
            this uint position, uint minusAmount, uint plusAmount)
        {
            var start = minusAmount > position ? 1 : position - minusAmount;
            var stop = position + plusAmount;
            return new InclusiveInterval<uint>(start, stop);
        }

        /// <summary>
        /// Parses the ci.
        /// If related tag does not exist, assuming no CI and return [0,0]
        /// </summary>
        /// <param name="variant">The variant.</param>
        /// <param name="tag">The tag.</param>
        /// <returns></returns>
        [NotNull]
        internal static InclusiveInterval<int> ParseCi([NotNull] this IVcfVariant variant, string tag)
        {
            if (!variant.Info.TryGetValue(tag, out var posString))
                return new InclusiveInterval<int>(0, 0);

            var split = posString.Split(WittyerConstants.InfoValueDel);
            if (split.Length != 2)
                throw VcfVariantFormatException.Create(variant.ToString(), ImmutableHashSet.Create(VcfColumn.Info),
                    $"Invalid {tag} found: {posString}", variant.ToStrings().ToList().AsReadOnly());

            return new InclusiveInterval<int>(int.Parse(split[0]), int.Parse(split[1]));
        }
    }
}