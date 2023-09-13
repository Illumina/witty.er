using System.Collections.Immutable;
using Ilmn.Das.App.Wittyer.Utilities.Enums;
using Ilmn.Das.Std.VariantUtils.Vcf.Variants.Samples;
using JetBrains.Annotations;

namespace Ilmn.Das.App.Wittyer.Vcf.Samples
{
    public interface IWittyerCopyNumberSample : IWittyerSample, ICopyNumberProvider
    {        
    }

    internal class WittyerCopyNumberSample : IWittyerCopyNumberSample
    {
        internal readonly WittyerSampleInternal BaseSample;
        public decimal? Cn { get; }

        private WittyerCopyNumberSample(WittyerSampleInternal baseSample, decimal? cn)
        {
            BaseSample = baseSample;
            Cn = cn;
        }

        [Pure]
        internal static WittyerCopyNumberSample Create(WittyerSampleInternal baseSample, decimal? cn) 
            => new(baseSample, cn);

        #region Implementation of IWittyerSample

        /// <inheritdoc />
        public IVcfSample OriginalSample => BaseSample.OriginalSample;

        /// <inheritdoc />
        public WitDecision Wit => BaseSample.Wit;

        /// <inheritdoc />
        public IImmutableList<MatchSet> What => BaseSample.What;

        /// <inheritdoc />
        public IImmutableList<FailedReason> Why => BaseSample.Why;



        #endregion
    }
}