using System.Collections.Generic;
using Ilmn.Das.App.Wittyer.Utilities;
using Ilmn.Das.App.Wittyer.Vcf.Samples;
using Ilmn.Das.App.Wittyer.Vcf.Variants.Annotations;
using Ilmn.Das.Std.AppUtils.Intervals;
using Ilmn.Das.Std.BioinformaticUtils.Bed;
using Ilmn.Das.Std.BioinformaticUtils.Contigs;
using Ilmn.Das.Std.BioinformaticUtils.GenomicFeatures;
using Ilmn.Das.Std.VariantUtils.Vcf.Variants;
using JetBrains.Annotations;

namespace Ilmn.Das.App.Wittyer.Vcf.Variants
{
    public static class WittyerBnd
    {
        internal class WittyerBndInternal : IWittyerBnd
        {
            private readonly IInterval<uint> _posInterval;

            private WittyerBndInternal([NotNull] IVcfVariant baseVariant, IInterval<uint> posInterval, Winner win,
                IContigAndInterval endInterval,
                List<OverlapAnnotation> overlapInfo, IWittyerSample sample, IVcfVariant endOriginalVariant, WittyerVariantType svType)
            {
                Contig = baseVariant.Contig;
                _posInterval = posInterval;
                EndInterval = endInterval;
                OverlapInfo = overlapInfo;
                Sample = sample;
                OriginalVariant = baseVariant;
                EndOriginalVariant = endOriginalVariant;
                Win = win;
                VariantType = svType;
            }

            public IContigInfo Contig { get; }

            public int CompareTo(IInterval<uint> other)
            {
                return _posInterval.CompareTo(other);
            }

            public bool Equals(IInterval<uint> other)
            {
                return _posInterval.Equals(other);
            }

            public uint Start => _posInterval.Start;
            public uint Stop => _posInterval.Stop;
            public bool IsStartInclusive => _posInterval.IsStartInclusive;
            public bool IsStopInclusive => _posInterval.IsStopInclusive;

            public int CompareTo(IContigAndInterval other)
            {
                return ContigAndIntervalComparer.Default.Compare(this, other);
            }

            public bool Equals(IContigAndInterval other)
            {
                return ContigAndIntervalComparer.Default.Equals(this, other);
            }

            public WittyerVariantType VariantType { get; }
            public Winner Win { get; }
            public IContigAndInterval EndInterval { get; }

            public List<OverlapAnnotation> OverlapInfo { get; }

            public IWittyerSample Sample { get; internal set; }
            public IVcfVariant OriginalVariant { get; }

            public void AddToOverlapInfo(OverlapAnnotation newAnnotation)
            {
                OverlapInfo.Add(newAnnotation);
            }

            public IVcfVariant EndOriginalVariant { get; }

            [NotNull]
            internal static IWittyerBnd Create([NotNull] IVcfVariant baseVariant, IInterval<uint> posInterval,
                Winner win, IContigAndInterval endInterval, List<OverlapAnnotation> overlapInfo,
                IWittyerSample sample, IVcfVariant endOriginalVariant, WittyerVariantType svType)
                => new WittyerBndInternal(baseVariant, posInterval, win, endInterval, overlapInfo, sample,
                    endOriginalVariant, svType);

            [NotNull]
            internal static IWittyerBnd Create([NotNull] IVcfVariant variant,
                IVcfVariant secondVariant, [CanBeNull] string sampleName,
                double percentageDistance, uint basepairDistance, IReadOnlyList<uint> bins)
            {
                var (first, second) = MiscUtils.FindBndEntriesOrder(variant, secondVariant);

                var posInterval = first.CalculateBndBorderInterval(second,
                    first.ParseCi(WittyerConstants.Cipos), percentageDistance, basepairDistance);

                var endInterval = second.CalculateBndBorderInterval(first,
                    second.ParseCi(WittyerConstants.Cipos), percentageDistance,
                    basepairDistance);

                var svType = variant.ParseWittyerVariantType(sampleName);
                var winner = GetWinner();

                var overlapInfo = new List<OverlapAnnotation>();

                var sample = WittyerSample.CreateOverall(variant, sampleName, false);

                return Create(first, posInterval, winner, endInterval, overlapInfo, sample, second, svType);

                Winner GetWinner()
                {
                    if (svType == WittyerVariantType.TranslocationBreakend)
                        return Winner.Create(svType);
                    if (svType != WittyerVariantType.Insertion)
                        return Winner.Create(svType, BedInterval.Create(first.Position, second.Position + 1), bins);

                    uint? end = null;
                    // insertion, try sequences first
                    if (variant.IsSimpleSequence(out var length))
                        end = length;

                    // try svlength, but if not, assume unknown length.
                    else if (variant.TryGetSvLength(out length) == null)
                        end = length;

                    return Winner.Create(svType, end == null ? null : BedInterval.Create(variant.Position, variant.Position + end.Value), bins);
                }
            }
        }
    }
}