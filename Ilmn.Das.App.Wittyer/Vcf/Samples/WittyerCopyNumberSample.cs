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
        [NotNull] internal readonly WittyerSampleInternal BaseSample;
        public uint? Cn { get; }

        private WittyerCopyNumberSample([NotNull] WittyerSampleInternal baseSample, uint? cn)
        {
            BaseSample = baseSample;
            Cn = cn;
        }

        [Pure]
        [NotNull]
        internal static WittyerCopyNumberSample Create([NotNull] WittyerSampleInternal baseSample, uint? cn) 
            => new WittyerCopyNumberSample(baseSample, cn);

        #region Implementation of IWittyerSample

        /// <inheritdoc />
        public IVcfSample GetOriginalSample() => BaseSample.GetOriginalSample();

        /// <inheritdoc />
        public WitDecision Wit => BaseSample.Wit;

        /// <inheritdoc />
        public IImmutableList<MatchEnum> What => BaseSample.What;

        /// <inheritdoc />
        public IImmutableList<FailedReason> Why => BaseSample.Why;



        #endregion
    }
}