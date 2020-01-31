using System.Collections.Immutable;
using Ilmn.Das.App.Wittyer.Vcf.Variants;
using Ilmn.Das.Core.Tries.Extensions;
using Ilmn.Das.Std.AppUtils.Intervals;
using Ilmn.Das.Std.VariantUtils.Vcf.Parsers;
using Ilmn.Das.Std.VariantUtils.Vcf.Variants;
using Ilmn.Das.Std.XunitUtils;
using Xunit;

namespace Ilmn.Das.App.Wittyer.Test
{
    public class WittyerBndInternalTest
    {
        private const string SymbolicInsertion =
            "chr1\t137221\t.\tT\t<INS>\t30\tPASS\tEND=137221;SVTYPE=INS;SVLEN=118;CONTIG=chr1:96179-176179/0;CONTIG_START=40355;CONTIG_END=40473;SEQ=GGGCAGGCTCACTGACCTCTCTCGCGTGGAGGGGCCGGTGGGCAGGGCTCACGCCCTTCCGTGGAGGCCGGGTGAGCAAGGGTCACCTGACCTCTCCAGCGTGGGAGGGGGCCGGTGG;IS_TRF=TR;SVCLASS=Complex;CALLSET=pacbio;UNION=PacBio\tGT\t1/1";

        private const string UnknownLength =
            "chr1\t1925144\tMantaINS:200:0:0:0:2:0\tG\t<INS>\t999\tPASS\tEND=1925144;SVTYPE=INS;CIPOS=0,14;CIEND=0,14;HOMLEN=14;HOMSEQ=GGCACAGTGGCTCA;LEFT_SVINSSEQ=GGCACAGTGGCTCATGCCTGTAATCCCAGCAACATGGGAGCCTGAGGTGGGAGGCTCTCTTGAGGCCAGGAGTTTGAGACCAGCCTGGGCAACATAGTGAGACCC;RIGHT_SVINSSEQ=CCAGGAGTTTGAGAACCGCCTGGGCAACATAGTGAGACCCCCCACCCCCCGCCATTTCTAGGAAAAAAAAAAAAAGTGGCCA\tGT:FT:GQ:PL:PR:SR\t1/1:PASS:50:999,53,0:0,5:0,23";

        [Fact]
        public void GetInsertionIntervalSymbolicIns()
        {
            var variant = VcfVariant
                .TryParse(SymbolicInsertion, VcfVariantParserSettings.Create(ImmutableList.Create("blah"))).GetOrThrow();
            var bedInterval = WittyerBndInternal.GetInsertionInterval(variant);

            MultiAssert.Equal(118U, bedInterval?.GetLength());
            MultiAssert.AssertAll();
        }

        [Fact]
        public void GetInsertionIntervalNoLenIns()
        {
            var variant = VcfVariant
                .TryParse(UnknownLength, VcfVariantParserSettings.Create(ImmutableList.Create("blah"))).GetOrThrow();
            var bedInterval = WittyerBndInternal.GetInsertionInterval(variant);

            MultiAssert.Equal(null, bedInterval?.GetLength());
            MultiAssert.AssertAll();
        }
    }
}