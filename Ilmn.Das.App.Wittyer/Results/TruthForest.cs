using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Ilmn.Das.App.Wittyer.Vcf.Variants;
using Ilmn.Das.Std.BioinformaticUtils.Contigs;
using Ilmn.Das.Std.BioinformaticUtils.GenomicFeatures;
using Ilmn.Das.Std.VariantUtils.Vcf.Headers;


namespace Ilmn.Das.App.Wittyer.Results
{
    internal class TruthForest : IMutableWittyerResult
    {
        private readonly IMutableWittyerResult _baseResult;

        internal readonly ConcurrentDictionary<WittyerType, GenomeIntervalTree<IMutableWittyerSimpleVariant>> VariantTrees
            = new();

        internal readonly ConcurrentDictionary<WittyerType, GenomeIntervalTree<IMutableWittyerSimpleVariant>> BpInsTrees = new();

        private TruthForest(string? sampleName, IVcfHeader vcfHeader)
            => _baseResult = new MutableWittyerResult(sampleName, true, vcfHeader);

        internal static TruthForest Create(string? sampleName, IVcfHeader vcfHeader) => new(sampleName, vcfHeader);

        #region Implementation of IWittyerResult

        /// <inheritdoc />
        public string SampleName => _baseResult.SampleName;

        /// <inheritdoc />
        public IReadOnlyList<IContigInfo> Contigs => _baseResult.Contigs;

        /// <inheritdoc />
        public bool IsTruth => _baseResult.IsTruth;

        /// <inheritdoc />
        public IReadOnlyDictionary<WittyerType, IReadOnlyList<IWittyerVariant>> Variants
            => _baseResult.Variants;

        /// <inheritdoc />
        public IReadOnlyDictionary<WittyerType, IReadOnlyList<IWittyerBnd>> BreakendPairsAndInsertions
            => _baseResult.BreakendPairsAndInsertions;

        /// <inheritdoc />
        public IReadOnlyCollection<IVcfVariant> NotAssessedVariants
            => _baseResult.NotAssessedVariants;

        #endregion

        #region Implementation of IMutableWittyerResult

        /// <inheritdoc />
        public IVcfHeader VcfHeader => _baseResult.VcfHeader;

        /// <inheritdoc />
        public uint NumEntries => _baseResult.NumEntries;

        /// <inheritdoc />
        public void AddTarget(IMutableWittyerSimpleVariant variant)
        {
            _baseResult.AddTarget(variant);
            switch (variant)
            {
                case IMutableWittyerBnd bnd:
                    BpInsTrees.GetOrAdd(variant.VariantType, 
                            _ => GenomeIntervalTree<IMutableWittyerSimpleVariant>.Create()).Add(bnd);
                    break;
                case IMutableWittyerVariant cast:
                    VariantTrees.GetOrAdd(variant.VariantType,
                        _ => GenomeIntervalTree<IMutableWittyerSimpleVariant>.Create()).Add(cast);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(
                        $"Unexepected Variant found that was neither {nameof(IWittyerBnd)} nor {nameof(IWittyerVariant)}!");
            }
        }

        /// <inheritdoc />
        public void AddUnsupported(IVcfVariant variant) => _baseResult.AddUnsupported(variant);

        /// <inheritdoc />
        public IEnumerable<IReadOnlyCollection<IMutableWittyerSimpleVariant>> VariantsInternal 
            => _baseResult.VariantsInternal;

        /// <inheritdoc />
        public IEnumerable<IReadOnlyCollection<IMutableWittyerSimpleVariant>> BreakendPairsInternal 
            => _baseResult.BreakendPairsInternal;

        #endregion
    }
}