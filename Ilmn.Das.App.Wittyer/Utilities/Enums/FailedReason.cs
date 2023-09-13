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
        /// False because CN mismatch
        /// </summary>
        CnMismatch,

        /// <summary>
        /// False because RUC mismatch
        /// </summary>
        RucMismatch,

        /// <summary>
        /// False because RUC not found or is an invalid value in VNTR
        /// </summary>
        RucNotFoundOrInvalid,

        /// <summary>
        /// False because RUL/RUS not found or is an invalid value or is mismatched in VNTR
        /// </summary>
        RulMismatch,

        /// <summary>
        /// False because REFRUC not found or is an invalid value or is mismatched in VNTR
        /// </summary>
        RefRucMismatch,

        /// <summary>
        /// False because number of VNTR alleles are different
        /// </summary>
        RucAlleleCountDiff,

        /// <summary>
        /// Truth has some error in their VNTR allele's RUC or query had an issue with REFRUC.
        /// </summary>
        RucAlleleTruthError,

        /// <summary>
        /// False because the BND partial matched (only one end matched)
        /// </summary>
        BndPartialMatch,
        
        /// <summary>
        /// False because the sequences of the INS were mismatched, otherwise everything else is good.
        /// </summary>
        SequenceMismatch,
        
        /// <summary>
        /// TP, but the sequences of the INS were not assessed since this was not provided
        /// </summary>
        SequenceUnassessed,
        
        /// <summary>
        /// Size of the INS were not similar enough.
        /// </summary>
        LengthMismatch,
        
        /// <summary>
        /// Size of the sequences of the INS were not assessed
        /// </summary>
        LengthUnassessed,

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
        /// Not Assessed because the CNV didn't have CN and the ALT field was unsupported.
        /// </summary>
        CnvWithUnsupportedAlt,

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

        /// <summary>
        /// Not Assessed because the entry has an invalid SVTYPE INFO key and we couldn't figure it out from other clues.
        /// </summary>
        InvalidSvType,

        /// <summary>
        /// Not Assessed because the BND was a single breakend
        /// </summary>
        UnpairedBnd,
        
        /// <summary>
        /// Not Assessed because the BND was a invalid or not parseable by Wittyer.
        /// </summary>
        UnsupportedBnd,

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