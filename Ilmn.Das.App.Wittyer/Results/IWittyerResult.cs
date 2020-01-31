using System.Collections.Generic;
using Ilmn.Das.App.Wittyer.Vcf.Variants;
using Ilmn.Das.Std.BioinformaticUtils.Contigs;
using Ilmn.Das.Std.VariantUtils.Vcf.Headers;
using Ilmn.Das.Std.VariantUtils.Vcf.Variants;
using JetBrains.Annotations;

namespace Ilmn.Das.App.Wittyer.Results
{
    /// <summary>
    /// The result (truth or query)
    /// </summary>
    public interface IWittyerResult
    {
        /// <summary>
        /// Gets the <see cref="IVcfHeader"/> from the vcf File that produced this result.
        /// </summary>
        /// <value>
        /// The VCF header.
        /// </value>
        [NotNull]
        IVcfHeader VcfHeader { get; }

        /// <summary>
        /// Gets the name of the sample.
        /// </summary>
        /// <value>
        /// The name of the sample.
        /// </value>
        [NotNull]
        string SampleName { get; }

        /// <summary>
        /// Gets the full set of contigs that are present in all variants.
        /// </summary>
        /// <value>
        /// The contigs.
        /// </value>
        [NotNull]
        IReadOnlyList<IContigInfo> Contigs { get; }

        /// <summary>
        /// Gets a value indicating whether this instance is the truth result.
        /// </summary>
        /// <value>
        ///   <c>true</c> if this instance is truth result; otherwise, <c>false</c>.
        /// </value>
        bool IsTruth { get; }

        /// <summary>
        /// Gets the <see cref="IWittyerVariant"/>s evaluated.
        /// </summary>
        /// <value>
        /// The normal variants.
        /// </value>
        [NotNull]
        IReadOnlyDictionary<WittyerType, IReadOnlyList<IWittyerVariant>> Variants { get; }

        /// <summary>
        /// Gets the <see cref="IWittyerBnd"/> pairs evaluated (can be <see cref="WittyerType.Insertion"/>s as well).
        /// </summary>
        /// <value>
        /// The breakend pairs.
        /// </value>
        [NotNull]
        IReadOnlyDictionary<WittyerType, IReadOnlyList<IWittyerBnd>> BreakendPairsAndInsertions { get; }

        /// <summary>
        /// Gets the <see cref="IVcfVariant"/>s that were not assessed.
        /// </summary>
        /// <value>
        /// The not assessed variants.
        /// </value>
        [NotNull]
        IReadOnlyCollection<IVcfVariant> NotAssessedVariants { get; }
    }

    /// <inheritdoc />
    /// <summary>
    /// The default implementation of <see cref="T:Ilmn.Das.App.Wittyer.Results.IWittyerResult" />
    /// </summary>
    /// <seealso cref="T:Ilmn.Das.App.Wittyer.Results.IWittyerResult" />
    public class WittyerResult : IWittyerResult
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="WittyerResult"/> class.
        /// </summary>
        /// <param name="vcfHeader">The VCF header.</param>
        /// <param name="sampleName">Name of the sample.</param>
        /// <param name="contigs">The list of contigs for these variants in the correct sort order (this is used for outputting the vcf in correct order).</param>
        /// <param name="isTruth">if set to <c>true</c> [is truth].</param>
        /// <param name="variants">The variants.</param>
        /// <param name="breakendPairsAndInsertions">The breakend pairs and insertions.</param>
        /// <param name="notAssessedVariants">The not assessed variants.</param>
        [NotNull]
        [Pure]
        public static WittyerResult Create([NotNull] IVcfHeader vcfHeader, [NotNull] string sampleName, 
            [NotNull] IReadOnlyList<IContigInfo> contigs, bool isTruth, 
            [NotNull] IReadOnlyDictionary<WittyerType, IReadOnlyList<IWittyerVariant>> variants,
            [NotNull] IReadOnlyDictionary<WittyerType, IReadOnlyList<IWittyerBnd>> breakendPairsAndInsertions,
            [NotNull] IReadOnlyCollection<IVcfVariant> notAssessedVariants)
            => new WittyerResult(vcfHeader, sampleName, contigs, isTruth, variants, breakendPairsAndInsertions,
                notAssessedVariants);

        private WittyerResult([NotNull] IVcfHeader vcfHeader, [NotNull] string sampleName, 
            [NotNull] IReadOnlyList<IContigInfo> contigs, bool isTruth, 
            [NotNull] IReadOnlyDictionary<WittyerType, IReadOnlyList<IWittyerVariant>> variants, 
            [NotNull] IReadOnlyDictionary<WittyerType, IReadOnlyList<IWittyerBnd>> breakendPairsAndInsertions, 
            [NotNull] IReadOnlyCollection<IVcfVariant> notAssessedVariants)
        {
            VcfHeader = vcfHeader;
            SampleName = sampleName;
            Contigs = contigs;
            IsTruth = isTruth;
            Variants = variants;
            BreakendPairsAndInsertions = breakendPairsAndInsertions;
            NotAssessedVariants = notAssessedVariants;
        }

        #region Implementation of IWittyerResult

        /// <inheritdoc />
        public IVcfHeader VcfHeader { get; }

        /// <inheritdoc />
        public string SampleName { get; }

        /// <inheritdoc />
        public IReadOnlyList<IContigInfo> Contigs { get; }

        /// <inheritdoc />
        public bool IsTruth { get; }

        /// <inheritdoc />
        public IReadOnlyDictionary<WittyerType, IReadOnlyList<IWittyerVariant>> Variants { get; }

        /// <inheritdoc />
        public IReadOnlyDictionary<WittyerType, IReadOnlyList<IWittyerBnd>> BreakendPairsAndInsertions { get; }

        /// <inheritdoc />
        public IReadOnlyCollection<IVcfVariant> NotAssessedVariants { get; }

        #endregion
    }
}