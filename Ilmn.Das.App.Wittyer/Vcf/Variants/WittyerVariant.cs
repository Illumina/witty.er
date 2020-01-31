using System.Collections.Generic;
using System.IO;
using Ilmn.Das.App.Wittyer.Utilities;
using Ilmn.Das.App.Wittyer.Vcf.Samples;
using Ilmn.Das.App.Wittyer.Vcf.Variants.Annotations;
using Ilmn.Das.Std.AppUtils.Intervals;
using Ilmn.Das.Std.BioinformaticUtils.Bed;
using Ilmn.Das.Std.BioinformaticUtils.Contigs;
using Ilmn.Das.Std.BioinformaticUtils.GenomicFeatures;
using Ilmn.Das.Std.VariantUtils.Vcf;
using Ilmn.Das.Std.VariantUtils.Vcf.Variants;
using JetBrains.Annotations;

namespace Ilmn.Das.App.Wittyer.Vcf.Variants
{
    /// <summary>
    ///     WittyerVariant, apply to DEL/DUP/INS/INV/CNV
    /// </summary>
    /// <seealso cref="IWittyerSimpleVariant" />
    public static class WittyerVariant
    {
        internal class WittyerVariantInternal : IWittyerVariant
        {
            private readonly IInterval<uint> _baseInterval;

            private WittyerVariantInternal([NotNull] IVcfVariant baseVariant, IInterval<uint> baseInterval,
                WittyerVariantType svType,
                IContigAndInterval posInterval, Winner win,
                List<OverlapAnnotation> overlapInfo, IWittyerSample sample, IContigAndInterval endInterval)
            {
                OriginalVariant = baseVariant;
                Contig = baseVariant.Contig;
                VariantType = svType;
                Win = win;
                OverlapInfo = overlapInfo;
                Sample = sample;
                _baseInterval = baseInterval;
                PosInterval = posInterval;
                EndInterval = endInterval;
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
            public WittyerVariantType VariantType { get; }

            /// <inheritdoc />
            public Winner Win { get; }

            /// <inheritdoc />
            public IContigAndInterval EndInterval { get; }

            /// <inheritdoc />
            public List<OverlapAnnotation> OverlapInfo { get; }

            /// <inheritdoc />
            public IWittyerSample Sample { get; internal set; }

            /// <inheritdoc />
            public IVcfVariant OriginalVariant { get; }

            /// <inheritdoc />
            public void AddToOverlapInfo(OverlapAnnotation newAnnotation) => OverlapInfo.Add(newAnnotation);

            [NotNull]
            internal static WittyerVariantInternal Create([NotNull] IVcfVariant baseVariant,
                IInterval<uint> baseInterval,
                WittyerVariantType svType,
                IContigAndInterval startInterval, Winner win,
                List<OverlapAnnotation> overlapInfo, IWittyerSample sample, IContigAndInterval endInterval)
                => new WittyerVariantInternal(baseVariant, baseInterval, svType, startInterval, win,
                    overlapInfo, sample, endInterval);


            [NotNull]
            internal static IWittyerVariant Create([NotNull] IVcfVariant baseVariant,
                string sample, double percentageDistance, uint basepairDistance,
                IReadOnlyList<uint> bins, WittyerVariantType svType)
            {
                if (svType == WittyerVariantType.Invalid)
                    throw new InvalidDataException(
                        $"Invalid {VcfConstants.SvTypeKey} in variant: \n{baseVariant}\nNot sure why you got here though. Check with a witty.er developer!");

                var end = baseVariant.Position + baseVariant.GetSvLength();
                var baseInterval = BedInterval.Create(baseVariant.Position, end);

                var borderInterval =
                    baseVariant.Position.CalculateBorderInterval(baseInterval,
                        baseVariant.ParseCi(WittyerConstants.Cipos),
                        percentageDistance, basepairDistance);

                // wittyerVariant should all have end border, it's a matter of how to find it, 
                // either END key in INFO field, sort out through SVLEN or other ways, details can be defined in FindEndBorder() later 
                var endInterval = (end - 1).CalculateBorderInterval(baseInterval,
                    baseVariant.ParseCi(WittyerConstants.Ciend),
                    percentageDistance, basepairDistance);

                var posContigAndInterval =
                    ContigAndInterval.Create(baseVariant.Contig, borderInterval.Start, borderInterval.Stop + 1);
                var endContigAndInterval =
                    ContigAndInterval.Create(baseVariant.Contig, endInterval.Start, endInterval.Stop + 1);

                return Create(baseVariant, baseInterval, svType, posContigAndInterval,
                    Winner.Create(svType, baseInterval, bins), new List<OverlapAnnotation>(),
                    WittyerSample.CreateOverall(baseVariant, sample, svType == WittyerVariantType.CopyNumberReference),
                    endContigAndInterval);
            }
        }
    }
}