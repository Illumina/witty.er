using System.ComponentModel;

namespace Ilmn.Das.App.Wittyer.Vcf.Variants
{
    public enum WittyerVariantType
    {
        Invalid = 0,
        [Description("DEL")]
        Deletion,
        [Description("DUP")]
        Duplication,
        [Description("INS")]
        Insertion,
        [Description("INV")]
        Inversion,
        TranslocationBreakend,
        IntraChromosomeBreakend,
        [Description("CNV")]
        Cnv,
        CopyNumberReference
    }
}
