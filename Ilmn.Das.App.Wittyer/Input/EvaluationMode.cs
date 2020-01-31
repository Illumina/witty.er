using System;

namespace Ilmn.Das.App.Wittyer.Input
{
    /// <summary>
    /// The evaluation mode
    /// </summary>
    [Flags]
    public enum EvaluationMode
    {
        /// <summary>
        /// The default aka Genotype comparison and not-crosstype
        /// </summary>
        Default = 0,
        /// <summary>
        /// Simple counting mode
        /// </summary>
        SimpleCounting = 1,
        /// <summary>
        /// cross type and simple counting mode
        /// </summary>
        CrossTypeAndSimpleCounting
    }
}