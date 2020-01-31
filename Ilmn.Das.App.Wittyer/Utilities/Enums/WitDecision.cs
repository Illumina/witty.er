using System.ComponentModel;

namespace Ilmn.Das.App.Wittyer.Utilities.Enums
{
    public enum WitDecision
    {
        [Description("N")]
        NotAssessed,

        [Description("TP")]
        TruePositive,

        [Description("FP")]
        FalsePositive,

        [Description("FN")]
        FalseNegative
    }
}
