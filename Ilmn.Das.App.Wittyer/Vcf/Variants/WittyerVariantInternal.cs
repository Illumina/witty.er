using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using Ilmn.Das.App.Wittyer.Input;
using Ilmn.Das.App.Wittyer.Utilities;
using Ilmn.Das.App.Wittyer.Utilities.Enums;
using Ilmn.Das.App.Wittyer.Vcf.Samples;
using Ilmn.Das.App.Wittyer.Vcf.Variants.Annotations;
using Ilmn.Das.Std.AppUtils.Intervals;
using Ilmn.Das.Std.BioinformaticUtils.Bed;
using Ilmn.Das.Std.BioinformaticUtils.Contigs;
using Ilmn.Das.Std.BioinformaticUtils.GenomicFeatures;
using Ilmn.Das.Std.VariantUtils.Vcf.Variants;
using Ilmn.Das.Std.VariantUtils.Vcf.Variants.Samples;
using JetBrains.Annotations;

namespace Ilmn.Das.App.Wittyer.Vcf.Variants
{
    internal class WittyerVariantInternal : IMutableWittyerVariant
    {
        [NotNull] private readonly IInterval<uint> _baseInterval;
        private static readonly ImmutableList<MatchEnum> EmptyWhat = ImmutableList.Create(MatchEnum.Unmatched);
        private static readonly ImmutableList<FailedReason> EmptyTrueWhy 
            = ImmutableList.Create(FailedReason.Unset);
        private static readonly ImmutableList<FailedReason> EmptyFalseWhy 
            = ImmutableList.Create(FailedReason.NoOverlap);
        private static readonly ImmutableList<FailedReason> EmptyOutsideBedWhy 
            = ImmutableList.Create(FailedReason.OutsideBedRegion);

        private WittyerVariantInternal([NotNull] WittyerType svType, [NotNull] IVcfVariant baseVariant, 
            [NotNull] IInterval<uint> baseInterval, [NotNull] Winner win, 
            [NotNull] IContigAndInterval posInterval, [NotNull] IInterval<uint> ciPosInterval, 
            [NotNull] IContigAndInterval endInterval, [NotNull] IInterval<uint> ciEndInterval, 
            [NotNull] IWittyerSample sample)
        {
            OriginalVariant = baseVariant;
            Contig = baseVariant.Contig;
            VariantType = svType;
            Win = win;
            Sample = sample;
            _baseInterval = baseInterval;
            PosInterval = posInterval;
            EndInterval = endInterval;
            CiPosInterval = ciPosInterval;
            CiEndInterval = ciEndInterval;
        }

        /// <inheritdoc />
        public IContigInfo Contig { get; }

        /// <inheritdoc />
        public int CompareTo(IInterval<uint> other) => _baseInterval.CompareTo(other);

        /// <inheritdoc />
        public bool Equals(IInterval<uint> other) => _baseInterval.Equals(other);

        /// <inheritdoc />
        public uint Start => _baseInterval.Start;

        /// <inheritdoc />
        public uint Stop => _baseInterval.Stop;

        /// <inheritdoc />
        public bool IsStartInclusive => _baseInterval.IsStartInclusive;

        /// <inheritdoc />
        public bool IsStopInclusive => _baseInterval.IsStopInclusive;

        /// <inheritdoc />
        public IContigAndInterval PosInterval { get; }

        /// <inheritdoc />
        public int CompareTo(IContigAndInterval other) => ContigAndIntervalComparer.Default.Compare(this, other);

        /// <inheritdoc />
        public bool Equals(IContigAndInterval other) => ContigAndIntervalComparer.Default.Equals(this, other);

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
            {
                isIncluded = false;
                if (includedRegions.TryGetValue(Contig, out var tree))
                {
                    var startPosition = CiPosInterval.Stop - 1;
                    var endPosition = CiEndInterval.Start + 1;
                    if (startPosition >= endPosition)
                        // means cipos or ciend goes past each other
                        // so any overlap with Start to Stop should mean included.
                        isIncluded = tree.Search(this).Any();
                    else
                        // ReSharper disable once LoopCanBeConvertedToQuery // prevent closure allocation
                        foreach (var overlap in tree.Search(startPosition))
                        {
                            if (!overlap.Contains(endPosition)) 
                                continue;
                            isIncluded = true;
                            break;
                        }
                }
            }

            Finalize(this, _overlapInfo, falseDecision, mode, isIncluded);
        }

        internal static void Finalize([NotNull] IMutableWittyerSimpleVariant variant,
            [NotNull] List<OverlapAnnotation> annotations, WitDecision falseDecision, EvaluationMode mode,
            bool? isIncluded)
        {
            annotations.Sort();

            WittyerSampleInternal unwrappedSample;
            switch (variant.Sample)
            {
                case WittyerSampleInternal simple:
                    unwrappedSample = simple;
                    break;
                case WittyerCopyNumberSample cnSample:
                    unwrappedSample = cnSample.BaseSample;
                    break;
                case WittyerGenotypedSample gtSample:
                    unwrappedSample = gtSample.BaseSample;
                    break;
                case WittyerGenotypedCopyNumberSample gtCnSample:
                    unwrappedSample = gtCnSample.BaseSample.BaseSample;
                    break;
                default:
                    throw new InvalidDataException(
                        "Not sure how we get here, you must have created some non-existed wittyer sample type, check with developer!");
            }

            // expected results:
            // isIncluded == null means no bedRegion, so everything is normal.
            // isIncluded != null means bedRegion, so if false, override the results as such:
            // Wit = NotAssessed.
            // Why[0] if Unset = OutsideBedRegion 
            unwrappedSample.What = ImmutableList<MatchEnum>.Empty;
            unwrappedSample.Why = ImmutableList<FailedReason>.Empty;
            var isTp = false;
            for (var i = 0; i < variant.OverlapInfo.Count; i++)
            {
                var what = variant.OverlapInfo[i].What;
                unwrappedSample.What = unwrappedSample.What.Add(what);
                isTp = isTp
                       || what == MatchEnum.AlleleAndGenotypeMatch
                       || (mode == EvaluationMode.SimpleCounting || mode == EvaluationMode.CrossTypeAndSimpleCounting)
                       && what == MatchEnum.AlleleMatch;
                
                var why = variant.OverlapInfo[i].Why;
                if (why == FailedReason.Unset)
                    why = i == 0 && isIncluded == false
                        ? FailedReason.OutsideBedRegion
                        : what == MatchEnum.Unmatched
                            ? FailedReason.NoOverlap
                            : FailedReason.Other;
                unwrappedSample.Why = unwrappedSample.Why.Add(why);
            }

            if (unwrappedSample.What.Count == 0)
                unwrappedSample.What = EmptyWhat;

            if (unwrappedSample.Why.Count == 0)
                unwrappedSample.Why = isIncluded == false ? EmptyOutsideBedWhy : isTp ? EmptyTrueWhy : EmptyFalseWhy;

            unwrappedSample.Wit = isIncluded == null || isIncluded.Value
                ? isTp
                    ? WitDecision.TruePositive
                    : falseDecision
                : WitDecision.NotAssessed;
        }

        [NotNull]
        [Pure]
        internal static IWittyerVariant Create([NotNull] IVcfVariant baseVariant,
            [CanBeNull] IVcfSample sample, [NotNull] WittyerType svType,
            [NotNull] IReadOnlyList<uint> bins, [CanBeNull] double? percentageDistance,
            uint basepairDistance)
        {
            // originalInterval is needed to adjust CIPOS and CIEND against for PD/BPD, but it won't be used for actual reflen and binning.
            var baseInterval = baseVariant.ToBedInterval(true, out var originalEnd, out var sharedFirstBase);
            if (baseInterval == null)
                throw new InvalidOperationException(
                    $"Expected failure of {nameof(WittyerUtils.ToBedInterval)} to throw, but didn't...");

            // CI intervals are always based on the original POS/END
            var posStart = baseVariant.Position;
            if (sharedFirstBase)
                posStart++;
            var ciPosInterval = posStart.ConvertPositionToCiInterval(baseVariant, WittyerConstants.Cipos);
            var ciEndInterval = originalEnd.ConvertPositionToCiInterval(baseVariant, WittyerConstants.Ciend);

            var baseStart = sharedFirstBase
                ? baseInterval.Start
                : baseInterval.Start + 1; // not sharing first base (ref site or complex types,  etc) need adjustment

            // the pd/bpd intervals are based on the trimmed variant's coordinates.
            var (posInterval, endInterval) = WittyerUtils.GetPosAndEndInterval(baseVariant.Contig, percentageDistance,
                basepairDistance, ciPosInterval, baseStart, ciEndInterval, baseInterval.Stop);

            return new WittyerVariantInternal(svType, baseVariant, baseInterval,
                Winner.Create(svType, baseInterval, bins),
                posInterval, ciPosInterval, endInterval, ciEndInterval,
                WittyerSample.CreateFromVariant(baseVariant, sample, svType == WittyerType.CopyNumberReference));
        }
    }
}