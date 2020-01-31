using System.ComponentModel;

namespace Ilmn.Das.App.Wittyer.Input
{
    public enum EvaluationMode
    {
        [Description("d")]
        Default = 0,
        [Description("sc")]
        SimpleCounting,
        [Description("cts")]
        CrossTypeAndSimpleCounting
    }
}
