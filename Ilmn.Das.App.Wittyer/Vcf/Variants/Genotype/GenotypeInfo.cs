using System.Collections.Immutable;
using System.IO;
using System.Linq;
using Ilmn.Das.Std.AppUtils.Collections;
using Ilmn.Das.Std.AppUtils.Comparers;
using Ilmn.Das.Std.AppUtils.Misc;
using Ilmn.Das.Std.VariantUtils.Vcf;
using Ilmn.Das.Std.VariantUtils.Vcf.Variants.Samples;
using JetBrains.Annotations;

namespace Ilmn.Das.App.Wittyer.Vcf.Variants.Genotype
{
    /// <summary>
    /// The default implementation of <see cref="IGenotypeInfo"/>
    /// </summary>
    /// <seealso cref="IGenotypeInfo" />
    public class GenotypeInfo : IGenotypeInfo
    {
        private GenotypeInfo(string originalGtString, bool isPhased, IImmutableList<string> genotypeIndices)
        {
            OriginalGtString = originalGtString;
            IsPhased = isPhased;
            GenotypeIndices = genotypeIndices;
        }
        
        /// <inheritdoc />
        public string OriginalGtString { get; }

        /// <inheritdoc />
        public bool IsPhased { get; }

        /// <inheritdoc />
        public IImmutableList<string> GenotypeIndices { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="GenotypeInfo"/> class.
        /// </summary>
        /// <param name="originalGtString">The original gt string.</param>
        /// <param name="isPhased">if set to <c>true</c> [is phased].</param>
        /// <param name="genotypeIndices">The genotype indices.</param>
        [Pure]
        public static IGenotypeInfo Create(string originalGtString, bool isPhased,
            IImmutableList<string> genotypeIndices)
            => new GenotypeInfo(originalGtString, isPhased, genotypeIndices);

        /// <summary>
        /// Initializes a new instance of the <see cref="GenotypeInfo"/> class based on the given <see cref="IVcfSample"/>.
        /// </summary>
        /// <param name="sample">The sample.</param>
        /// <returns></returns>
        /// <exception cref="InvalidDataException">
        /// </exception>
        [Pure]
        public static IGenotypeInfo CreateFromSample(IVcfSample sample)
        {
            if (!sample.SampleDictionary.ContainsKey(VcfConstants.GenotypeKey))
                throw new InvalidDataException($"{sample} has not GT field for this variant {sample}");

            var gtString = sample.SampleDictionary[VcfConstants.GenotypeKey];
            var isPhased = gtString.Contains(VcfConstants.GtPhasedValueDelimiter);

            var gtIndices = ImmutableList<string>.Empty.ToBuilder();
            foreach (var gt in gtString.Split(VcfConstants.GtPhasedValueDelimiter[0],
                VcfConstants.GtUnphasedValueDelimiter[0]))
            {
                if (uint.TryParse(gt, out _) || gt == ".")
                    gtIndices.Add(gt);
                else
                    throw new InvalidDataException($"{gtString} is not a valid {VcfConstants.GenotypeKey}");
            }

            return Create(gtString, isPhased, gtIndices.ToImmutable());
        }

        internal static IGenotypeInfo CreateRef(int ploidy, bool isPhased)
            => TypeCache<int, IGenotypeInfo>.GetOrAdd(isPhased ? -ploidy : ploidy, () =>
            {
                var del = isPhased ? VcfConstants.GtPhasedValueDelimiter : VcfConstants.GtUnphasedValueDelimiter;
                var gtIndicies = Enumerable.Repeat("0", ploidy).ToImmutableList();
                var originalGtString = gtIndicies.StringJoin(del);
                return Create(originalGtString, isPhased, gtIndicies);
            });

        /// <inheritdoc />
        public override bool Equals(object? obj)
            => ReferenceEquals(this, obj) || obj is IGenotypeInfo info && Equals(info);

        /// <inheritdoc />
        public bool Equals(IGenotypeInfo? other)
            => IsPhased.Equals(other?.IsPhased)
               && (IsPhased
                   ? GenotypeIndices.SequenceEqual(other.GenotypeIndices)
                   : GenotypeIndices.IsScrambledEquals(other.GenotypeIndices));

        /// <inheritdoc />
        public override int GetHashCode()
        {
            var hashCode = 161077207;
            hashCode = hashCode * -1521134295 + IsPhased.GetHashCode();
            hashCode = hashCode * -1521134295 + HashCodeUtils.GenerateForEnumerables(GenotypeIndices, IsPhased);
            return hashCode;
        }
    }
}