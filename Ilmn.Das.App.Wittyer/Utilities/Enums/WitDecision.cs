using System.ComponentModel;

namespace Ilmn.Das.App.Wittyer.Utilities.Enums
{
    /// <summary>
    /// The decision that was made
    /// </summary>
    public enum WitDecision
    {
        /// <summary>
        /// Not Assessed
        /// </summary>
        [Description("N")]
        NotAssessed,

        /// <summary>
        /// True Positive
        /// </summary>
        [Description("TP")]
        TruePositive,

        /// <summary>
        /// False Positive
        /// </summary>
        [Description("FP")]
        FalsePositive,

        /// <summary>
        /// False Negative
        /// </summary>
        [Description("FN")]
        FalseNegative
    }
}
