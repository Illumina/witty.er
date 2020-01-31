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
using Ilmn.Das.Std.VariantUtils.Vcf.Variants;
using JetBrains.Annotations;

namespace Ilmn.Das.App.Wittyer.Results
{
    internal interface IMutableWittyerResult : IWittyerResult
    {
        uint NumEntries { get; }
        void AddTarget([NotNull] IMutableWittyerSimpleVariant variant);
        void AddUnsupported([NotNull] IVcfVariant variant);
        [NotNull, ItemNotNull]
        IEnumerable<IReadOnlyCollection<IMutableWittyerVariant>> VariantsInternal { get; }
        [NotNull, ItemNotNull]
        IEnumerable<IReadOnlyCollection<IMutableWittyerBnd>> BreakendPairsInternal { get; }
    }

    /// <summary>
    /// Query set used for comparison. 
    /// Query is everything used for stats
    /// NotSupportedVariants are variants excluded from stats, including invalid variants, filtered out or single breakend etc.
    /// </summary>
    internal class MutableWittyerResult : IMutableWittyerResult
    {
        private readonly ConcurrentQueue<IVcfVariant> _unsupported = new ConcurrentQueue<IVcfVariant>();

        /// <inheritdoc />
        public string SampleName { get; }

        /// <inheritdoc />
        public IReadOnlyList<IContigInfo> Contigs => _contigList.ToReadOnlyList();

        public IReadOnlyCollection<IVcfVariant> NotAssessedVariants => _unsupported;

        private static readonly CustomClassEqualityComparer<IContigInfo> ContigComparer
            = new CustomClassEqualityComparer<IContigInfo>(
                (c1, c2) => c1.Name.Equals(c2.Name),
                c => HashCodeUtils.Generate(c?.Name));

        private readonly ConcurrentDictionary<IContigInfo, bool> _contigSet
            = new ConcurrentDictionary<IContigInfo, bool>(ContigComparer);

        private readonly ConcurrentQueue<IContigInfo> _contigList = new ConcurrentQueue<IContigInfo>();

        internal MutableWittyerResult([CanBeNull] string sampleName, bool isTruth, [NotNull] IVcfHeader vcfHeader)
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

        private void AddContig([NotNull] IContigProvider contig)
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

        private readonly ConcurrentDictionary<WittyerType, List<IMutableWittyerVariant>> _variants
            = new ConcurrentDictionary<WittyerType, List<IMutableWittyerVariant>>();

        public IEnumerable<IReadOnlyCollection<IMutableWittyerVariant>> VariantsInternal
            => _variants.Values.AsReadOnly();

        /// <inheritdoc />
        public IReadOnlyDictionary<WittyerType, IReadOnlyList<IWittyerVariant>> Variants
            => ToReadOnly<IMutableWittyerVariant, IWittyerVariant>(_variants);

        private readonly ConcurrentDictionary<WittyerType, List<IMutableWittyerBnd>> _bndPairs
            = new ConcurrentDictionary<WittyerType, List<IMutableWittyerBnd>>();

        public IEnumerable<IReadOnlyCollection<IMutableWittyerBnd>> BreakendPairsInternal
            => _bndPairs.Values.AsReadOnly();

        /// <inheritdoc />
        public IReadOnlyDictionary<WittyerType, IReadOnlyList<IWittyerBnd>> BreakendPairsAndInsertions
            => ToReadOnly<IMutableWittyerBnd, IWittyerBnd>(_bndPairs);

        [NotNull]
        private static IReadOnlyDictionary<WittyerType, IReadOnlyList<TResult>> ToReadOnly<TSource, TResult>(
            ConcurrentDictionary<WittyerType, List<TSource>> variants) where TSource : TResult
            => new ReadOnlyDictionary<WittyerType, IReadOnlyList<TResult>>(variants.AsEnumerable()
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Cast<TResult>().ToReadOnlyList()));

        #endregion
    }

}