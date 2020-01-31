using System.ComponentModel;

namespace Ilmn.Das.App.Wittyer.Utilities.Enums
{
    public enum FailedReason
    {
        /// <summary>
        /// Not applied, for TN and TP
        /// </summary>
        [Description(".")]
        Unset = 0,

        FailedBoundary,

        GtMismatch,

        CnMismatch,

        BndPartialMatch,

        NoOverlap,

        //NotAssessedReason

        CnvWithoutCn,

        UndeterminedCn,

        FailedFilter,

        UnassessedRefCall,

        InvalidSvLen,

        UnpairedBnd,

        Other
    }
}
