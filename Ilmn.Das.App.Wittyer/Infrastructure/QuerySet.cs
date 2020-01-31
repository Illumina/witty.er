using System.Collections.Generic;
using System.Collections.Immutable;
using Ilmn.Das.App.Wittyer.Vcf.Variants;
using Ilmn.Das.Std.AppUtils.Misc;
using Ilmn.Das.Std.VariantUtils.Vcf.Variants;
using JetBrains.Annotations;

namespace Ilmn.Das.App.Wittyer.Infrastructure
{
    /// <summary>
    /// Query set used for comparison. 
    /// Query is everything used for stats
    /// NotSupportedVariants are variants excluded from stats, including invalid variants, filtered out or single breakend etc.
    /// </summary>
    internal class QuerySet
    {
        public IReadOnlyList<IWittyerSimpleVariant> Query { get; }

        public IReadOnlyList<IVcfVariant> NotSupportedVariants { get; }

        public string SampleName { get; }

        private QuerySet(IReadOnlyList<IWittyerSimpleVariant> query, IReadOnlyList<IVcfVariant> notSupported, string sampleName)
        {
            Query = query;
            NotSupportedVariants = notSupported;
            SampleName = sampleName;
        }

        public static QuerySet Create([NotNull, ItemNotNull] IReadOnlyList<IWittyerSimpleVariant> query,
            [NotNull, ItemNotNull] IReadOnlyList<IVcfVariant> notSupported, string sampleName) 
            => new QuerySet(query, notSupported, sampleName);

        internal class Builder : IBuilder<QuerySet>
        {
            private IImmutableList<IWittyerSimpleVariant> _query;
            private IImmutableList<IVcfVariant> _notSupported;
            private static string _sampleName;

            private Builder(IImmutableList<IWittyerSimpleVariant> query, IImmutableList<IVcfVariant> notSupported, string sampleName)
            {
                _query = query;
                _notSupported = notSupported;
                _sampleName = sampleName;
            }

            internal static Builder Create(string sampleName) 
                => new Builder(ImmutableList<IWittyerSimpleVariant>.Empty, ImmutableList<IVcfVariant>.Empty, sampleName);

            internal Builder AddQuery(IWittyerSimpleVariant variant)
            {
                _query = _query.Add(variant);
                return this;
            }

            internal Builder AddNonSupported(IVcfVariant variant)
            {
                _notSupported = _notSupported.Add(variant);
                return this;
            }

            public QuerySet Build() => new QuerySet(_query, _notSupported, _sampleName);
        }
    }
}
