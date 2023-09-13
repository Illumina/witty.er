using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using Ilmn.Das.App.Wittyer.Input;
using Ilmn.Das.App.Wittyer.Vcf.Variants;
using Ilmn.Das.Std.AppUtils.Collections;
using Ilmn.Das.Std.AppUtils.Comparers;
using Ilmn.Das.Std.BioinformaticUtils.Contigs;
using Ilmn.Das.Std.BioinformaticUtils.GenomicFeatures;
using Ilmn.Das.Std.VariantUtils.Vcf.Headers;


namespace Ilmn.Das.App.Wittyer.Results
{
    internal interface IMutableWittyerResult : IWittyerResult
    {
        uint NumEntries { get; }
        void AddTarget(IMutableWittyerSimpleVariant variant);
        void AddUnsupported(IVcfVariant variant);
        IEnumerable<IReadOnlyCollection<IMutableWittyerSimpleVariant>> VariantsInternal { get; }
        IEnumerable<IReadOnlyCollection<IMutableWittyerSimpleVariant>> BreakendPairsInternal { get; }
    }

    /// <summary>
    /// Query set used for comparison. 
    /// Query is everything used for stats
    /// NotSupportedVariants are variants excluded from stats, including invalid variants, filtered out or single breakend etc.
    /// </summary>
    internal class MutableWittyerResult : IMutableWittyerResult
    {
        private readonly ConcurrentQueue<IVcfVariant> _unsupported = new();

        /// <inheritdoc />
        public string SampleName { get; }

        /// <inheritdoc />
        public IReadOnlyList<IContigInfo> Contigs => _contigList.ToReadOnlyList();

        public IReadOnlyCollection<IVcfVariant> NotAssessedVariants => _unsupported;

        private static readonly CustomClassEqualityComparer<IContigInfo> ContigComparer
            = new(
                (c1, c2) => c1.Name.Equals(c2.Name),
                c => HashCodeUtils.Generate(c?.Name));

        private readonly ConcurrentDictionary<IContigInfo, bool> _contigSet = new(ContigComparer);

        private readonly ConcurrentQueue<IContigInfo> _contigList = new();

        internal MutableWittyerResult(string? sampleName, bool isTruth, IVcfHeader vcfHeader)
        {
            // ReSharper disable once AssignNullToNotNullAttribute
            SampleName = sampleName ?? (isTruth
                             ? SamplePair.Default.TruthSampleName
                             : SamplePair.Default.QuerySampleName);
            IsTruth = isTruth;
            VcfHeader = vcfHeader;
        }

        /// <inheritdoc />
        public IVcfHeader VcfHeader { get; }

        /// <inheritdoc />
        public bool IsTruth { get; }

        private long _numEntries;

        public uint NumEntries => (uint)_numEntries;

        public void AddTarget(IMutableWittyerSimpleVariant variant)
        {
            switch (variant)
            {
                case IMutableWittyerBnd bnd:
                    _bndPairs.GetOrAdd(bnd.VariantType, _ => new List<IMutableWittyerBnd>()).Add(bnd);
                    break;
                case IMutableWittyerVariant cast:
                    _variants.GetOrAdd(cast.VariantType, _ => new List<IMutableWittyerVariant>()).Add(cast);
                    break;
                default:
                    throw new InvalidCastException(
                        $"Variant found to not be {nameof(IWittyerBnd)} nor {nameof(IWittyerVariant)}!");
            }

            AddContig(variant);
            Interlocked.Add(ref _numEntries,
                variant.VariantType == WittyerType.TranslocationBreakend
                || variant.VariantType == WittyerType.IntraChromosomeBreakend
                    ? 2
                    : 1);
        }

        private void AddContig(IContigProvider contig)
        {
            if (_contigSet.TryAdd(contig.Contig, false))
                _contigList.Enqueue(contig.Contig);
        }

        public void AddUnsupported(IVcfVariant variant)
        {
            AddContig(variant);
            _unsupported.Enqueue(variant);
            Interlocked.Increment(ref _numEntries);
        }        

        #region Implementation of IWittyerResult

        private readonly ConcurrentDictionary<WittyerType, List<IMutableWittyerVariant>> _variants = new();

        public IEnumerable<IReadOnlyCollection<IMutableWittyerSimpleVariant>> VariantsInternal
            => _variants.Values.AsReadOnly();

        /// <inheritdoc />
        public IReadOnlyDictionary<WittyerType, IReadOnlyList<IWittyerVariant>> Variants
            => ToReadOnly<IMutableWittyerVariant, IWittyerVariant>(_variants);

        private readonly ConcurrentDictionary<WittyerType, List<IMutableWittyerBnd>> _bndPairs = new();

        public IEnumerable<IReadOnlyCollection<IMutableWittyerSimpleVariant>> BreakendPairsInternal
            => _bndPairs.Values.AsReadOnly();

        /// <inheritdoc />
        public IReadOnlyDictionary<WittyerType, IReadOnlyList<IWittyerBnd>> BreakendPairsAndInsertions
            => ToReadOnly<IMutableWittyerBnd, IWittyerBnd>(_bndPairs);

        private static IReadOnlyDictionary<WittyerType, IReadOnlyList<TResult>> ToReadOnly<TSource, TResult>(
            ConcurrentDictionary<WittyerType, List<TSource>> variants) where TSource : TResult
            => new ReadOnlyDictionary<WittyerType, IReadOnlyList<TResult>>(variants.AsEnumerable()
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Cast<TResult>().ToReadOnlyList()));

        #endregion
    }

}