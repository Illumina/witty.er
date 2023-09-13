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
using Ilmn.Das.Std.VariantUtils.Vcf.Variants.Samples;

using JetBrains.Annotations;

namespace Ilmn.Das.App.Wittyer.Vcf.Variants
{
    internal class WittyerBndInternal : IMutableWittyerBnd
    {
        private readonly IInterval<uint> _posInterval;

        private WittyerBndInternal(WittyerType svType, IVcfVariant baseVariant, 
            IInterval<uint> posInterval, IInterval<uint> ciPosInterval, 
            IVcfVariant endOriginalVariant, IContigAndInterval endInterval, 
            IInterval<uint> ciEndInterval, Winner win, IWittyerSample sample, IInterval<uint>? svLenInterval)
        {
            Contig = baseVariant.Contig;
            _posInterval = posInterval;
            EndInterval = endInterval;
            Sample = sample;
            SvLenInterval = svLenInterval;
            OriginalVariant = baseVariant;
            EndOriginalVariant = endOriginalVariant;
            Win = win;
            VariantType = svType;
            CiPosInterval = ciPosInterval;
            CiEndInterval = ciEndInterval;
            EndRefPos = endOriginalVariant.Position + 1;
        }

        public IContigInfo Contig { get; }

        public int CompareTo(IInterval<uint>? other) => _posInterval.CompareTo(other);

        public bool Equals(IInterval<uint>? other) => _posInterval.Equals(other);

        /// <inheritdoc />
        public uint Start => _posInterval.Start;

        /// <inheritdoc />
        public uint Stop => _posInterval.Stop;

        /// <inheritdoc />
        public bool IsStartInclusive => _posInterval.IsStartInclusive;

        /// <inheritdoc />
        public bool IsStopInclusive => _posInterval.IsStopInclusive;

        /// <inheritdoc />
        public int CompareTo(IContigAndInterval? other) => ContigAndIntervalComparer.Default.Compare(this, other);

        /// <inheritdoc />
        public bool Equals(IContigAndInterval? other) 
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

        /// <inheritdoc />
        public uint EndRefPos { get; }

        /// <inheritdoc />
        public List<OverlapAnnotation> OverlapInfo { get; } = new();

        /// <inheritdoc />
        IReadOnlyList<OverlapAnnotation> IWittyerSimpleVariant.OverlapInfo => OverlapInfo.AsReadOnly();

        /// <inheritdoc />
        public IWittyerSample Sample { get; }

        /// <inheritdoc />
        public IVcfVariant OriginalVariant { get; }

        public IInterval<uint>? SvLenInterval { get; }

        /// <inheritdoc />
        public void AddToOverlapInfo(OverlapAnnotation newAnnotation) => OverlapInfo.Add(newAnnotation);

        /// <inheritdoc />
        public void Finalize(WitDecision falseDecision, EvaluationMode mode,
            GenomeIntervalTree<IContigAndInterval>? includedRegions, int? maxMatches)
        {
            bool? isIncluded = null;
            if (includedRegions != null)
                isIncluded = includedRegions.TryGetValue(OriginalVariant.Contig, out var tree)
                             && tree.Search(CiPosInterval).Any() // or there's overlap in bed regions
                             && (ReferenceEquals(OriginalVariant, EndOriginalVariant)
                                 || Equals(OriginalVariant.Contig, EndOriginalVariant.Contig)
                                 || includedRegions.TryGetValue(EndOriginalVariant.Contig, out tree)
                                 && tree.Search(CiEndInterval).Any()); // or end overlaps.

            WittyerVariantInternal.Finalize(this, OverlapInfo, falseDecision, mode, isIncluded, maxMatches);
        }

        public IVcfVariant EndOriginalVariant { get; }

        [Pure]
        internal static IWittyerBnd Create(IVcfVariant first, IVcfSample? originalSample,
            WittyerType wittyerType, IReadOnlyList<(uint start, bool skip)> bins, uint bpd, double? percentageDistance,
            IVcfVariant second)
        {
            if (wittyerType != WittyerType.Insertion)
                (first, second) = FindBndEntriesOrder(in first, in second);

            var ciPosInterval = first.Position.ConvertPositionToCiInterval(first, WittyerConstants.Cipos);
            var ciEndInterval = ReferenceEquals(first, second) 
                ? ciPosInterval // same variant means same intervals.
                : second.Position.ConvertPositionToCiInterval(second, WittyerConstants.Cipos);

            IInterval<uint>? insertionInterval = null;
            IContigAndInterval posInterval, endInterval;
            if (wittyerType == WittyerType.Insertion) // insertions need trimming and stuff.
            {
                var trimmed = first.TryNormalizeVariant(VariantNormalizer.TrimCommonBases, 0).GetOrThrow();
                var tuple = (bpd, bpd);
                var (posStart, posStop) = trimmed.Position.ConvertPositionToCiInterval(tuple);
                WittyerUtils.GetBetterInterval(ciPosInterval, ref posStart, ref posStop);
                posInterval = endInterval = ContigAndInterval.Create(first.Contig, posStart, posStop);
                insertionInterval = GetInsertionInterval(first);
            }
            else
            {
                var isIntra = wittyerType == WittyerType.IntraChromosomeBreakend;
                
                var isTr = (first.Info.TryGetValue(WittyerConstants.EventTypeInfoKey, out var eventType)
                           || second.Info.TryGetValue(WittyerConstants.EventTypeInfoKey, out eventType))
                           && eventType == "TR";
                (posInterval, endInterval) = WittyerUtils.GetPosAndEndInterval(first.Contig,
                    isIntra ? percentageDistance : null, bpd,
                    ciPosInterval, first.Position, ciEndInterval, second.Position, isTr, second.Contig);
                if (isIntra)
                { 
                    var start = first.Position;
                    if (start > 0)
                        start--;
                    insertionInterval = BedInterval.Create(start, second.Position);
                }
            }

            var winner = Winner.Create(wittyerType, insertionInterval, bins);

            var sample = WittyerSample.CreateFromVariant(first, originalSample, false);
            return new WittyerBndInternal(wittyerType, first, posInterval, ciPosInterval,
                second, endInterval, ciEndInterval, winner, sample, insertionInterval);

            (IVcfVariant first, IVcfVariant second) FindBndEntriesOrder(in IVcfVariant variantA,
                in IVcfVariant variantB)
                => ContigAndPositionComparer.Default.Compare(variantA, variantB) > 0
                    ? (variantB, variantA)
                    : (variantA, variantB);
        }

        internal static IInterval<uint>? GetInsertionInterval(IVcfVariant first, int altIndex = 0)
            => first.IsAltSimpleSequence(0)
                ? first.ToBedInterval(false, out _, out _, altIndex)
                : first.TryGetSvLength(out var svLen, altIndex) != null
                    ? null
                    : BedInterval.Create(first.Position, first.Position + svLen);
    }
}