using System;
using System.ComponentModel;

namespace Ilmn.Das.App.Wittyer.Input
{
    /// <summary>
    /// The evaluation mode
    /// </summary>
    [Flags]
    public enum EvaluationMode
    {
        /// <summary>
        /// Simple counting mode
        /// </summary>
        [Description("sc")]
        SimpleCounting = 0,
        /// <summary>
        /// The Genotype comparison mode when Genotype needs to be exactly the same before we count them as a match and not-crosstype
        /// </summary>
        [Description("gm")]
        GenotypeMatching,
        /// <summary>
        /// cross type and simple counting mode
        /// </summary>
        [Description("cts")]
        CrossTypeAndSimpleCounting
    }
}