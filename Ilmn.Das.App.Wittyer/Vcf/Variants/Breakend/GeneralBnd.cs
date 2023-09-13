using System.Collections.Generic;
using System.IO;
using Ilmn.Das.App.Wittyer.Utilities;
using Ilmn.Das.App.Wittyer.Vcf.Samples;
using Ilmn.Das.Core.Tries;
using Ilmn.Das.Std.AppUtils.Intervals;
using Ilmn.Das.Std.BioinformaticUtils.Bed;
using Ilmn.Das.Std.BioinformaticUtils.Contigs;
using Ilmn.Das.Std.BioinformaticUtils.GenomicFeatures;
using Ilmn.Das.Std.BioinformaticUtils.Nucleotides;
using Ilmn.Das.Std.VariantUtils.SimpleVariants;
using Ilmn.Das.Std.VariantUtils.Vcf.Variants.Alleles.BreakEnds;
using JetBrains.Annotations;

namespace Ilmn.Das.App.Wittyer.Vcf.Variants.Breakend
{
    /// <summary>
    /// The default implentation of <see cref="IGeneralBnd"/>
    /// </summary>
    /// <seealso cref="IGeneralBnd" />
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

        /// <inheritdoc />
        public IContigInfo Contig => _baseVariant.Contig;

        /// <inheritdoc />
        public uint Position => _baseVariant.Position;

        /// <inheritdoc />
        public bool Equals(ISimpleVariant? other) 
            => SimpleVariantEqualityComparer.Instance.Equals(this, other);

        /// <inheritdoc />
        public IReadOnlyList<string> Alts => _baseVariant.Alts;

        /// <inheritdoc />
        public DnaString Ref => _baseVariant.Ref;

        /// <inheritdoc />
        public bool Equals(IVcfVariant? other) 
            => VcfVariantEqualityComparer.Instance.Equals(this, other);

        /// <inheritdoc />
        public IReadOnlyList<string> Ids => _baseVariant.Ids;

        /// <inheritdoc />
        public ITry<double> Quality => _baseVariant.Quality;

        /// <inheritdoc />
        public IReadOnlyList<string> Filters => _baseVariant.Filters;

        /// <inheritdoc />
        public IReadOnlyDictionary<string, string> Info => _baseVariant.Info;

        /// <inheritdoc />
        public SampleDictionaries Samples => _baseVariant.Samples;

        /// <inheritdoc />
        public bool Is3Prime { get; }

        /// <inheritdoc />
        public ISimpleBreakEnd Mate { get; }

        /// <inheritdoc />
        public int CompareTo(IInterval<uint>? other) => _interval.CompareTo(other);
        
        /// <inheritdoc />
        public bool Equals(IInterval<uint>? other) => _interval.Equals(other);

        /// <inheritdoc />
        public uint Start => _interval.Start;

        /// <inheritdoc />
        public uint Stop => _interval.Stop;

        /// <inheritdoc />
        public bool IsStartInclusive => _interval.IsStartInclusive;

        /// <inheritdoc />
        public bool IsStopInclusive => _interval.IsStopInclusive;

        /// <inheritdoc />
        public int CompareTo(IContigAndInterval? other) 
            => ContigAndIntervalComparer.Default.Compare(this, other);
        
        /// <inheritdoc />
        public bool Equals(IContigAndInterval? other) 
            => ContigAndIntervalComparer.Default.Equals(this, other);

        /// <summary>
        /// Creates a new instance of <see cref="IGeneralBnd"/> from a base variant.
        /// </summary>
        /// <param name="variant">The variant.</param>
        /// <returns></returns>
        /// <exception cref="InvalidDataException">Invalid breakend because neither the alt didn't start or end with ref's first base: {variant}</exception>
        [Pure]
        public static IGeneralBnd CreateFromVariant(IVcfVariant variant)
        {
            var altBnd = variant.GetSingleAlt();

            var thisRef = variant.Ref[0];

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