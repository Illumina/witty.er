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
using Ilmn.Das.Std.VariantUtils.Vcf;
using Ilmn.Das.Std.VariantUtils.Vcf.Variants.Samples;
using JetBrains.Annotations;

namespace Ilmn.Das.App.Wittyer.Vcf.Variants
{
    internal class WittyerVariantInternal : IMutableWittyerVariant
    {
        private readonly IInterval<uint> _baseInterval;
        private static readonly ImmutableList<FailedReason> EmptyTrueWhy 
            = ImmutableList.Create(FailedReason.Unset);
        private static readonly ImmutableList<FailedReason> EmptyFalseWhy 
            = ImmutableList.Create(FailedReason.NoOverlap);
        private static readonly IImmutableList<FailedReason> EmptyOutsideBedWhy 
            = ImmutableList.Create(FailedReason.OutsideBedRegion);

        private WittyerVariantInternal(WittyerType svType, IVcfVariant baseVariant, 
            IInterval<uint> baseInterval, Winner win, 
            IContigAndInterval posInterval, IInterval<uint> ciPosInterval, 
            IContigAndInterval endInterval, IInterval<uint> ciEndInterval, 
            IWittyerSample sample, uint endRefPos, IInterval<uint>? svLenInterval = null)
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
            EndRefPos = endRefPos;
            SvLenInterval = svLenInterval ?? baseInterval;
        }

        /// <inheritdoc />
        public IContigInfo Contig { get; }

        /// <inheritdoc />
        public int CompareTo(IInterval<uint>? other) => _baseInterval.CompareTo(other);

        /// <inheritdoc />
        public bool Equals(IInterval<uint>? other) => _baseInterval.Equals(other);

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
        public int CompareTo(IContigAndInterval? other) => ContigAndIntervalComparer.Default.Compare(this, other);

        /// <inheritdoc />
        public bool Equals(IContigAndInterval? other) => ContigAndIntervalComparer.Default.Equals(this, other);

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

        public IInterval<uint> SvLenInterval { get; }

        /// <inheritdoc />
        public void AddToOverlapInfo(OverlapAnnotation newAnnotation) => OverlapInfo.Add(newAnnotation);

        /// <inheritdoc />
        public void Finalize(WitDecision falseDecision, EvaluationMode mode,
            GenomeIntervalTree<IContigAndInterval>? includedRegions, int? maxMatches)
        {
            bool? isIncluded = null;
            if (includedRegions != null)
            {
                isIncluded = false;
                if (includedRegions.TryGetValue(Contig, out var tree))
                {
                    var startPosition = CiPosInterval.Stop - 1;
                    var endPosition = CiEndInterval.Start;
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

            Finalize(this, OverlapInfo, falseDecision, mode, isIncluded, maxMatches);
        }

        internal static void Finalize(IMutableWittyerSimpleVariant variant,
            List<OverlapAnnotation> annotations, WitDecision falseDecision, EvaluationMode mode,
            bool? isIncluded, int? maxMatches)
        {
            var isTruth = falseDecision == WitDecision.FalseNegative;
            if (isTruth)
            {
                annotations.Sort();
                var max = maxMatches ?? ((variant.Sample as IWittyerGenotypedSample)?.Gt.GenotypeIndices.Count ?? 2);
                if (max < annotations.Count)
                    annotations.RemoveRange(max, annotations.Count - max);
            }

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
            unwrappedSample.What = ImmutableList<MatchSet>.Empty;
            unwrappedSample.Why = ImmutableList<FailedReason>.Empty;
            var isTp = false;
            var isSymbolic = variant.OriginalVariant.Alts.FirstOrDefault()?.StartsWith("<") ?? false;
            for (var i = 0; i < variant.OverlapInfo.Count; i++)
            {
                var what = variant.OverlapInfo[i].What;
                unwrappedSample.What = unwrappedSample.What.Add(what);
                isTp = isTp
                       || what.Contains(MatchEnum.Allele)
                       && (mode is EvaluationMode.SimpleCounting or EvaluationMode.CrossTypeAndSimpleCounting
                           || what.Contains(MatchEnum.Genotype));
                
                var why = variant.OverlapInfo[i].Why;
                if (isIncluded == false)
                    why = FailedReason.OutsideBedRegion;
                else if (why == FailedReason.Unset
                         && (what.Count == 0 || what.Count == 1 && what.First() == MatchEnum.Unmatched))
                    why = FailedReason.NoOverlap;
                unwrappedSample.Why = unwrappedSample.Why.Add(why);
            }

            if (unwrappedSample.Why.Count == 0)
                unwrappedSample.Why = isIncluded == false ? EmptyOutsideBedWhy : isTp ? EmptyTrueWhy : EmptyFalseWhy;
            else if (isIncluded == false)
                unwrappedSample.Why = unwrappedSample.Why.SetItem(0, FailedReason.OutsideBedRegion);

            unwrappedSample.Wit = isIncluded == null || isIncluded.Value
                ? isTp
                    ? WitDecision.TruePositive
                    : falseDecision
                : WitDecision.NotAssessed;
        }

        [Pure]
        internal static IWittyerVariant Create(IVcfVariant baseVariant,
            IVcfSample? sample, WittyerType wittyerType,
            IReadOnlyList<(uint start, bool skip)> bins, double? percentageDistance,
            uint basepairDistance, int? altIndex)
        {
            // originalInterval is needed to adjust CIPOS and CIEND against for PD/BPD, but it won't be used for actual reflen and binning.
            var baseInterval = baseVariant.ToBedInterval(true, out var originalEnd, out var sharedFirstBase, altIndex);
            if (baseInterval == null)
                throw new InvalidOperationException(
                    $"Expected failure of {nameof(WittyerUtils.ToBedInterval)} to throw, but didn't...");

            if (wittyerType == WittyerType.CopyNumberTandemRepeat)
            {
                var posInt = ContigAndInterval.Create(baseVariant.Contig, baseInterval.Start, baseInterval.Start + 1U);
                var endInt = ContigAndInterval.Create(baseVariant.Contig, baseInterval.Stop - 1U, baseInterval.Stop);

                uint? cnTrStop = null;
                if (baseVariant.Info.TryGetValue(VcfConstants.CnSampleFieldKey, out var cnStr)
                    && decimal.TryParse(cnStr.Split(VcfConstants.InfoFieldValueDelimiter)[0], out var cnVal))
                {
                    var extraLength = Math.Round((cnVal - 1M) * baseInterval.GetLength());
                    cnTrStop = (uint)(baseInterval.Start + (long)extraLength);
                }
                else if (baseVariant.Samples.Count > 0
                         && baseVariant.Samples[0].SampleDictionary
                             .TryGetValue(VcfConstants.CnSampleFieldKey, out cnStr)
                         && decimal.TryParse(cnStr, out cnVal))
                {
                    var extraLength = Math.Round(cnVal -
                                                 (baseVariant.Samples[0].SampleDictionary
                                                     .TryGetValue(VcfConstants.GenotypeKey, out var gt)
                                                     ? gt.Count(it =>
                                                         it == VcfConstants.GtPhasedValueDelimiter[0]
                                                         || it == VcfConstants.GtUnphasedValueDelimiter[0])
                                                     : 2) * baseInterval.GetLength());
                    cnTrStop = (uint)(baseInterval.Start + (long)extraLength);
                }

                if (cnTrStop == baseInterval.Start)
                    cnTrStop = baseInterval.Start + 1;

                return new WittyerVariantInternal(wittyerType, baseVariant, baseInterval,
                    Winner.Create(wittyerType, baseInterval, bins),
                    posInt,
                    posInt, endInt, endInt,
                    WittyerSample.CreateFromVariant(baseVariant, sample, false),
                    originalEnd, cnTrStop == null
                        ? null
                        : baseInterval.Start > cnTrStop
                            ? BedInterval.Create(cnTrStop.Value, baseInterval.Start)
                            : BedInterval.Create(baseInterval.Start, cnTrStop.Value));
            }

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
            var isTr = baseVariant.Info.TryGetValue(WittyerConstants.EventTypeInfoKey, out var eventType)
                       && eventType == "TR";
            var (posInterval, endInterval) = WittyerUtils.GetPosAndEndInterval(baseVariant.Contig, percentageDistance,
                basepairDistance, ciPosInterval, baseStart, ciEndInterval, baseInterval.Stop, isTr);

            if (isTr)
                baseInterval = BedInterval.Create(posInterval.Start, endInterval.Stop);
            return new WittyerVariantInternal(wittyerType, baseVariant, baseInterval,
                Winner.Create(wittyerType, baseInterval, bins),
                posInterval, ciPosInterval, endInterval, ciEndInterval,
                WittyerSample.CreateFromVariant(baseVariant, sample, wittyerType == WittyerType.CopyNumberReference),
                originalEnd);
        }
    }
}