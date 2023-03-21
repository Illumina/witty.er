using System.ComponentModel;

namespace Ilmn.Das.App.Wittyer.Utilities.Enums
{
    /// <summary>
    /// The failed reasons possible for the WHYtag
    /// </summary>
    public enum FailedReason
    {
        /// <summary>
        /// No Failed Reason, basically is a TN or TP
        /// </summary>
        [Description(".")]
        Unset = 0,

        /// <summary>
        /// False because borders were too far off
        /// </summary>
        BordersTooFarOff,

        /// <summary>
        /// False because gt mismatch
        /// </summary>
        GtMismatch,

        /// <summary>
        /// False because cn mismatch
        /// </summary>
        CnMismatch,

        /// <summary>
        /// False because the BND partial matched (only one end matched)
        /// </summary>
        BndPartialMatch,

        /// <summary>
        /// False because there was no overlap
        /// </summary>
        NoOverlap,




        //NotAssessedReasons

        /// <summary>
        /// Not Assessed because the CNV didn't have cn
        /// </summary>
        CnvWithoutCn,

        /// <summary>
        /// Not Assessed because the CNV had CN=.
        /// </summary>
        UndeterminedCn,

        /// <summary>
        /// Not Assessed because the entry had an excluded or non-included filter
        /// </summary>
        FilteredBySettings,

        /// <summary>
        /// Not Assessed because the included filter included PASS and Sample FT was not PASS
        /// </summary>
        FailedSampleFilter,

        /// <summary>
        /// Not Assessed because the entry was a reference call on a type other than CNV/DUP/DEL
        /// </summary>
        UnsupportedRefCall,

        /// <summary>
        /// Not Assessed because the had an invalid sv length (0)
        /// </summary>
        InvalidSvLen,
        
        InvalidSvType,

        /// <summary>
        /// Not Assessed because the BND was a single breakend
        /// </summary>
        UnpairedBnd,

        /// <summary>
        /// Not Assessed because the variant type was not included in the settings
        /// </summary>
        VariantTypeSkipped,

        /// <summary>
        /// Not Assessed because the variant type was outside of the included bed regions
        /// </summary>
        OutsideBedRegion,

        /// <summary>
        /// Not Assessed or False for some other reason.
        /// </summary>
        Other
    }
}
