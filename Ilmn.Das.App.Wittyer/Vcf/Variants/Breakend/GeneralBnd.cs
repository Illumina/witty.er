using System.Collections.Generic;
using System.IO;
using Ilmn.Das.Core.Tries;
using Ilmn.Das.Std.AppUtils.Intervals;
using Ilmn.Das.Std.BioinformaticUtils.Bed;
using Ilmn.Das.Std.BioinformaticUtils.Contigs;
using Ilmn.Das.Std.BioinformaticUtils.GenomicFeatures;
using Ilmn.Das.Std.BioinformaticUtils.Nucleotides;
using Ilmn.Das.Std.VariantUtils.SimpleVariants;
using Ilmn.Das.Std.VariantUtils.Vcf;
using Ilmn.Das.Std.VariantUtils.Vcf.Variants;
using Ilmn.Das.Std.VariantUtils.Vcf.Variants.Alleles.BreakEnds;
using Ilmn.Das.Std.VariantUtils.Vcf.Variants.Samples;
using JetBrains.Annotations;

namespace Ilmn.Das.App.Wittyer.Vcf.Variants.Breakend
{
    public class GeneralBnd : IGeneralBnd
    {
        private readonly IVcfVariant _baseVariant;
        private readonly IInterval<uint> _interval;

        private GeneralBnd(IVcfVariant variant, IInterval<uint> interval, bool is3Prime, ISimpleBreakEnd mate)
        {
            _baseVariant = variant;
            _interval = interval;
            Is3Prime = is3Prime;
            Mate = mate;
        }

        public IContigInfo Contig => _baseVariant.Contig;
        public uint Position => _baseVariant.Position;

        public bool Equals(ISimpleVariant other)
        {
            return SimpleVariantEqualityComparer.Instance.Equals(this, other);
        }

        public IReadOnlyList<string> Alts => _baseVariant.Alts;
        public DnaString Ref => _baseVariant.Ref;

        public bool Equals(IVcfVariant other)
        {
            return VcfVariantEqualityComparer.Instance.Equals(this, other);
        }

        public IReadOnlyList<string> Ids => _baseVariant.Ids;
        public ITry<double> Quality =>_baseVariant.Quality;
        public IReadOnlyList<string> Filters => _baseVariant.Filters;
        public IReadOnlyDictionary<string, string> Info => _baseVariant.Info;
        public SampleDictionaries Samples => _baseVariant.Samples;

        public bool Is3Prime { get; }

        public ISimpleBreakEnd Mate { get; }


        public int CompareTo(IInterval<uint> other)
        {
            return _interval.CompareTo(other);
        }

        public bool Equals(IInterval<uint> other)
        {
            return _interval.Equals(other);
        }

        public uint Start => _interval.Start;
        public uint Stop => _interval.Stop;
        public bool IsStartInclusive => _interval.IsStartInclusive;
        public bool IsStopInclusive => _interval.IsStopInclusive;

        public int CompareTo(IContigAndInterval other)
        {
            return ContigAndIntervalComparer.Default.Compare(this, other);
        }

        public bool Equals(IContigAndInterval other)
        {
            return ContigAndIntervalComparer.Default.Equals(this, other);
        }

        [NotNull]
        public static GeneralBnd Create([NotNull] IVcfVariant variant)
        {
            if (variant.Alts.Count != 1)
                throw new InvalidDataException(
                    $"Only support breakend with one ALT for now, double check this one {variant}");

            var altBnd = variant.Alts[0];

            var thisRef = variant.Ref.ToString();

            var mate = SimpleBreakEnd.Parse(altBnd, out var firstField, out var lastField);

            var is3Prime = !string.IsNullOrWhiteSpace(firstField);

            if (is3Prime && !firstField.StartsWith(thisRef)
                || !is3Prime && !lastField.EndsWith(thisRef))
                throw new InvalidDataException(
                    $"Invalid breakend because neither the alt didn't start or end with ref's first base: {variant}");

            var interval = BedInterval.Create(variant.Position - 1, variant.Position);

            return new GeneralBnd(variant, interval, is3Prime, mate);
        }
    }
}