using System.Collections.Immutable;
using Ilmn.Das.App.Wittyer.Utilities.Enums;
using Ilmn.Das.Std.VariantUtils.Vcf.Variants.Samples;


namespace Ilmn.Das.App.Wittyer.Vcf.Samples
{
    public interface IWittyerSample
    {
        IVcfSample? OriginalSample { get; }

        /// <summary>
        /// WIT tag, decision from wittyer: TP/TN/FP/FN
        /// </summary>
        /// <value>
        /// The wit.
        /// </value>
        WitDecision Wit { get; }

        /// <summary>
        /// WHAT tag, extension of WIT to tell what types of matches there were.
        /// </summary>
        /// <value>
        /// The what.
        /// </value>
        IImmutableList<MatchSet> What { get; }

        /// <summary>
        /// WHY tag: reason for FP and N
        /// </summary>
        /// <value>
        /// The why.
        /// </value>
        IImmutableList<FailedReason> Why { get; }
    }
}