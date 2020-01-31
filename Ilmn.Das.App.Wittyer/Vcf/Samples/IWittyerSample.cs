using System.Collections.Immutable;
using Ilmn.Das.App.Wittyer.Utilities.Enums;
using Ilmn.Das.Std.VariantUtils.Vcf.Variants.Samples;
using JetBrains.Annotations;

namespace Ilmn.Das.App.Wittyer.Vcf.Samples
{
    public interface IWittyerSample
    {
        [CanBeNull] IVcfSample GetOriginalSample();

        /// <summary>
        /// WIT tag, decision from wittyer: TP/TN/FP/FN
        /// </summary>
        /// <value>
        /// The wit.
        /// </value>
        WitDecision Wit { get; }

        /// <summary>
        /// WHAT tag, extension of WIT: lm/lgm/am/agm
        /// </summary>
        /// <value>
        /// The what.
        /// </value>
        [NotNull]
        IImmutableList<MatchEnum> What { get; }

        /// <summary>
        /// WHY tag: reason for FP and N
        /// </summary>
        /// <value>
        /// The why.
        /// </value>
        [NotNull]
        IImmutableList<FailedReason> Why { get; }

    }
}