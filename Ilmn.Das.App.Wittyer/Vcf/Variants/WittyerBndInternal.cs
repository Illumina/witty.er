using System.Collections.Generic;
using System.Linq;
using Ilmn.Das.App.Wittyer.Input;
using Ilmn.Das.App.Wittyer.Utilities;
using Ilmn.Das.App.Wittyer.Utilities.Enums;
using Ilmn.Das.App.Wittyer.Vcf.Samples;
using Ilmn.Das.App.Wittyer.Vcf.Variants.Annotations;
using Ilmn.Das.Core.Tries.Extensions;
using Ilmn.Das.Std.AppUtils.Intervals;
using Ilmn.Das.Std.BioinformaticUtils.Bed;
using Ilmn.Das.Std.BioinformaticUtils.Contigs;
using Ilmn.Das.Std.BioinformaticUtils.GenomicFeatures;
using Ilmn.Das.Std.VariantUtils.SimpleVariants;
using Ilmn.Das.Std.VariantUtils.Vcf.Variants;
using Ilmn.Das.Std.VariantUtils.Vcf.Variants.Samples;
using JetBrains.Annotations;

namespace Ilmn.Das.App.Wittyer.Vcf.Variants
{
    internal class WittyerBndInternal : IMutableWittyerBnd
    {
        private readonly IInterval<uint> _posInterval;

        private WittyerBndInternal([NotNull] WittyerType svType, [NotNull] IVcfVariant baseVariant, 
            [NotNull] IInterval<uint> posInterval, [NotNull] IInterval<uint> ciPosInterval, 
            [NotNull] IVcfVariant endOriginalVariant, [NotNull] IContigAndInterval endInterval, 
            [NotNull] IInterval<uint> ciEndInterval, [NotNull] Winner win, [NotNull] IWittyerSample sample)
        {
            Contig = baseVariant.Contig;
            _posInterval = posInterval;
            EndInterval = endInterval;
            Sample = sample;
            OriginalVariant = baseVariant;
            EndOriginalVariant = endOriginalVariant;
            Win = win;
            VariantType = svType;
            CiPosInterval = ciPosInterval;
            CiEndInterval = ciEndInterval;
        }

        public IContigInfo Contig { get; }

        public int CompareTo(IInterval<uint> other) => _posInterval.CompareTo(other);

        public bool Equals(IInterval<uint> other) => _posInterval.Equals(other);

        /// <inheritdoc />
        public uint Start => _posInterval.Start;

        /// <inheritdoc />
        public uint Stop => _posInterval.Stop;

        /// <inheritdoc />
        public bool IsStartInclusive => _posInterval.IsStartInclusive;

        /// <inheritdoc />
        public bool IsStopInclusive => _posInterval.IsStopInclusive;

        /// <inheritdoc />
        public int CompareTo(IContigAndInterval other) => ContigAndIntervalComparer.Default.Compare(this, other);

        /// <inheritdoc />
        public bool Equals(IContigAndInterval other) 
            => ContigAndIntervalComparer.Default.Equals(this, other);

        /// <inheritdoc />
        public WittyerType VariantType { get; }

        /// <inheritdoc />
        public Winner Win { get; }

        /// <inheritdoc />
        public IInterval<uint> CiPosInterval { get; }

        /// <inheritdoc />
        public IInterval<uint> CiEndInterval { get; }

        /// <inheritdoc />
        public IContigAndInterval EndInterval { get; }

        private readonly List<OverlapAnnotation> _overlapInfo = new List<OverlapAnnotation>();

        /// <inheritdoc />
        public IReadOnlyList<OverlapAnnotation> OverlapInfo => _overlapInfo.AsReadOnly();

        /// <inheritdoc />
        public IWittyerSample Sample { get; }

        /// <inheritdoc />
        public IVcfVariant OriginalVariant { get; }

        /// <inheritdoc />
        public void AddToOverlapInfo(OverlapAnnotation newAnnotation) => _overlapInfo.Add(newAnnotation);

        /// <inheritdoc />
        public void Finalize(WitDecision falseDecision, EvaluationMode mode,
            GenomeIntervalTree<IContigAndInterval> includedRegions)
        {
            bool? isIncluded = null;
            if (includedRegions != null)
                isIncluded = includedRegions.TryGetValue(OriginalVariant.Contig, out var tree)
                             && tree.Search(CiPosInterval).Any() // or there's overlap in bed regions
                             && (ReferenceEquals(OriginalVariant, EndOriginalVariant)
                                 || Equals(OriginalVariant.Contig, EndOriginalVariant.Contig)
                                 || includedRegions.TryGetValue(EndOriginalVariant.Contig, out tree)
                                 && tree.Search(CiEndInterval).Any()); // or end overlaps.

            WittyerVariantInternal.Finalize(this, _overlapInfo, falseDecision, mode, isIncluded);
        }

        public IVcfVariant EndOriginalVariant { get; }

        [NotNull]
        [Pure]
        internal static IWittyerBnd CreateInsertion([NotNull] IVcfVariant first, [CanBeNull] IVcfSample originalSample,
            [NotNull] WittyerType svType, [NotNull] IReadOnlyList<uint> bins, uint bpd, double? pd)
            => Create(first, originalSample, svType, bins, bpd, pd, first);

        [NotNull]
        [Pure]
        internal static IWittyerBnd Create([NotNull] IVcfVariant first, [CanBeNull] IVcfSample originalSample,
            [NotNull] WittyerType svType, [NotNull] IReadOnlyList<uint> bins, uint bpd, double? percentageDistance,
            [NotNull] IVcfVariant second)
        {
            if (!ReferenceEquals(first, second))
                (first, second) = FindBndEntriesOrder(in first, in second);

            var ciPosInterval = first.Position.ConvertPositionToCiInterval(first, WittyerConstants.Cipos);
            var ciEndInterval = ReferenceEquals(first, second) 
                ? ciPosInterval // same variant means same intervals.
                : second.Position.ConvertPositionToCiInterval(second, WittyerConstants.Cipos);

            IContigAndInterval posInterval, endInterval;
            if (ReferenceEquals(first, second)) // insertions need trimming and stuff.
            {
                var trimmed = first.TryNormalizeVariant(VariantNormalizer.TrimCommonBases, 0).GetOrThrow();
                var tuple = (bpd, bpd);
                var (posStart, posStop) = trimmed.Position.ConvertPositionToCiInterval(tuple);
                WittyerUtils.GetBetterInterval(ciPosInterval, ref posStart, ref posStop);
                posInterval = endInterval = ContigAndInterval.Create(first.Contig, posStart, posStop);
            }
            else
                (posInterval, endInterval) = WittyerUtils.GetPosAndEndInterval(first.Contig,
                    svType == WittyerType.IntraChromosomeBreakend ? percentageDistance : null, bpd,
                    ciPosInterval, first.Position, ciEndInterval, second.Position, second.Contig);

            var winner = GetWinner();

            var sample = WittyerSample.CreateFromVariant(first, originalSample, false);
            return new WittyerBndInternal(svType, first, posInterval, ciPosInterval,
                second, endInterval, ciEndInterval, winner, sample);

            (IVcfVariant first, IVcfVariant second) FindBndEntriesOrder(in IVcfVariant variantA,
                in IVcfVariant variantB)
                => ContigAndPositionComparer.Default.Compare(variantA, variantB) > 0
                    ? (variantB, variantA)
                    : (variantA, variantB);

            Winner GetWinner()
            {
                if (svType == WittyerType.TranslocationBreakend)
                    return Winner.Create(svType);


                IInterval<uint> bedInterval;
                if (svType == WittyerType.Insertion)
                    bedInterval = GetInsertionInterval(first);
                else
                {
                    var start = first.Position;
                    if (start > 0)
                        start--;
                    bedInterval = BedInterval.Create(start, second.Position);
                }

                return Winner.Create(svType, bedInterval, bins);
            }
        }

        [CanBeNull]
        internal static IInterval<uint> GetInsertionInterval([NotNull] IVcfVariant first)
            => first.IsAltSimpleSequence(0)
                ? first.ToBedInterval(false, out _, out _)
                : first.TryGetSvLength(out var svLen) != null
                    ? null
                    : BedInterval.Create(first.Position, first.Position + svLen);
    }
}