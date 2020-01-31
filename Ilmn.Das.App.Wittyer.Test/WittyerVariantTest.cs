using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Ilmn.Das.App.Wittyer.Utilities;
using Ilmn.Das.App.Wittyer.Vcf.Samples;
using Ilmn.Das.App.Wittyer.Vcf.Variants;
using Ilmn.Das.Core.Tries.Extensions;
using Ilmn.Das.Std.BioinformaticUtils.Contigs;
using Ilmn.Das.Std.BioinformaticUtils.Genomes;
using Ilmn.Das.Std.BioinformaticUtils.GenomicFeatures;
using Ilmn.Das.Std.VariantUtils.Vcf.Parsers;
using Ilmn.Das.Std.VariantUtils.Vcf.Variants;
using Ilmn.Das.Std.XunitUtils;
using JetBrains.Annotations;
using Xunit;

namespace Ilmn.Das.App.Wittyer.Test
{
    public static class WittyerVariantTest
    {
        private const string BasicVariant =
            "chr22\t23676345\tMantaINV:1584:0:1:0:0:0\tG\t<INV>\t.\tPASS\tEND=24601131;SVTYPE=INV;SVLEN=924786;IMPRECISE;CIPOS=-242,242;CIEND=-140,141;INV5;SOMATIC;SOMATICSCORE=13\tPR\t11,0\t42,5";

        private const string GenotypedVariant =
            "chr1\t3350094\tMantaDEL:403:0:1:0:0:0\tCTGGGATGGCCCGCCCTGCCCACGCGCTCACCTGCCTGTCTGGGATGGCCCGCCCTGCCCACGCGCTCACCTGCCTGTCT\tC\t490\tPASS\tEND=3350173;SVTYPE=DEL;SVLEN=-79;CIGAR=1M1I79D;GMAF=C|0.4143\tGT:FT:GQ:PL:PR:SR:DQ\t0/1:PASS:140:190,0,285:7,0:20,8:0\t0/0:PASS:143:0,93,593:12,0:36,0:.";

        private const string GenotypedBnd =
            "1\t231176\tMantaBND:0:4499:4500:0:0:0:1\tA\t[4:191034952[GA\t303\tPASS\tSVTYPE=BND;MATEID=MantaBND:0:4499:4500:0:0:0:0;SVINSLEN=1;SVINSSEQ=G;BND_DEPTH=327;MATE_BND_DEPTH=82\tGT:FT:GQ:PL:PR:SR:DQ\t0/0:PASS:35:15,0,999:36,0:68,7:0\t0/1:PASS:187:237,0,999:24,1:82,11:.";

        private const string GenotypedBndPair =
            "4\t191034952\tMantaBND:0:4499:4500:0:0:0:0\tC\t[1:231176[CC\t303\tPASS\tSVTYPE=BND;MATEID=MantaBND:0:4499:4500:0:0:0:1;SVINSLEN=1;SVINSSEQ=C;BND_DEPTH=82;MATE_BND_DEPTH=327\tGT:FT:GQ:PL:PR:SR:DQ\t0/0:PASS:35:15,0,999:36,0:68,7:0\t0/1:PASS:187:237,0,999:24,1:82,11:.";

        private const string GenotypedIntraBnd =
            "1\t10103\tMantaBND:0:4499:4500:0:0:0:0\tC\t[1:2311[CC\t303\tPASS\tSVTYPE=BND;MATEID=MantaBND:0:4499:4500:0:0:0:1;SVINSLEN=1;SVINSSEQ=C;BND_DEPTH=82;MATE_BND_DEPTH=327\tGT:FT:GQ:PL:PR:SR:DQ\t0/0:PASS:35:15,0,999:36,0:68,7:0\t0/1:PASS:187:237,0,999:24,1:82,11:.";

        private const string GenotypedIntraBndPair =
            "1\t2311\tMantaBND:0:4499:4500:0:0:0:1\tA\t[1:10103[GA\t303\tPASS\tSVTYPE=BND;MATEID=MantaBND:0:4499:4500:0:0:0:0;SVINSLEN=1;SVINSSEQ=G;BND_DEPTH=327;MATE_BND_DEPTH=82\tGT:FT:GQ:PL:PR:SR:DQ\t0/0:PASS:35:15,0,999:36,0:68,7:0\t0/1:PASS:187:237,0,999:24,1:82,11:.";

        private const string GenotypedCnvVariant =
            "chr22\t16820403\tCanvas:GAIN:chr22:16820404-50781191\tN\t<CN3>,<CN4>\t14\tPASS\tSVTYPE=CNV;END=50781191;CNVLEN=33960788;CIPOS=-339,339;CIEND=-845,845;\tGT:RC:BC:CN:MCC:MCCQ:QS:FT\t.\t1/2:86.00:40297:7:4:.:14.00:PASS";

        private const string RefernceVariant =
            "chr1\t988572\trs552519714\tG\t<DEL>\t.\tPASS\tSVTYPE=DEL;END=988623;\tGT\t0/0";

        private const string ReferencdCnvVarianat = 
            "chr1\t988572\trs552519714\tG\t<DUP>\t.\tPASS\tSVTYPE=DUP;END=988623;\tCN\t2";

        private const string ReferencdCnvVarianatNoSvType =
            "chr1\t988572\trs552519714\tG\t<CNV>\t.\tPASS\tEND=988623;\tCN\t2";

        private const double PercentDistance = 0.05;

        private static readonly IReadOnlyList<uint> Bins = ImmutableList.Create(1000U, 10000U);

        private const uint BasepairDistance = 500;

        [Theory]
        [InlineData(ReferencdCnvVarianat)]
        [InlineData(RefernceVariant)]
        [InlineData(ReferencdCnvVarianatNoSvType)]
        public static void ParseReferenceVariantWorks([NotNull] string inputVariant)
        {
            var vcfVariant = VcfVariant.TryParse(inputVariant,
                    VcfVariantParserSettings.Create(ImmutableList.Create("NA12878"), GenomeAssembly.Hg19))
                .GetOrThrowDebug();

            var actualType = vcfVariant.ParseWittyerVariantType("NA12878");
            Assert.Equal(WittyerVariantType.CopyNumberReference, actualType);
        }

        [Theory]
        [InlineData(BasicVariant, 23675845, 23676846, 24600630, 24601631, "INV|10000+")]
        [InlineData(GenotypedVariant, 3350090, 3350099, 3350168, 3350177, "DEL|1-1000")]
        [InlineData(GenotypedCnvVariant, 16819903, 16820904, 50780345, 50782036, "CNV|10000+")]
        public static void WittyerVariantCreateCorrectly(string variant, uint posStart, uint posEnd, uint endStart, uint endEnd, string winner)
        {
            var vcfVariant = VcfVariant.TryParse(variant,
                VcfVariantParserSettings.Create(ImmutableList.Create("normal", "tumor"), GenomeAssembly.Hg38)).GetOrThrowDebug();

            var wittyerVariant = WittyerVariant.WittyerVariantInternal
                .Create(vcfVariant, "tumor", PercentDistance, BasepairDistance, Bins, vcfVariant.ParseWittyerVariantType(null));

            var expectedStart = ContigAndInterval.Create(vcfVariant.Contig, posStart, posEnd);
            var expectedEnd = ContigAndInterval.Create(vcfVariant.Contig, endStart, endEnd);

            //cannot put "null" in inline data

            MultiAssert.Equal(expectedStart, wittyerVariant.PosInterval);
            MultiAssert.Equal(expectedEnd, wittyerVariant.EndInterval);
            MultiAssert.Equal(winner, wittyerVariant.Win.ToString());
            MultiAssert.AssertAll();
        }

        [Fact]
        public static void WittyerBndCreateCorrectly()
        {
            var vcfSettings =
                VcfVariantParserSettings.Create(ImmutableList.Create("proband", "father"), GenomeAssembly.Grch37);
            var bnd1 = VcfVariant.TryParse(GenotypedBnd, vcfSettings).GetOrThrowDebug();

            var bnd2 = VcfVariant.TryParse(GenotypedBndPair, vcfSettings).GetOrThrowDebug();
            var wittyerBnd = WittyerBnd.WittyerBndInternal
                .Create(bnd2, bnd1, "father", PercentDistance, BasepairDistance, Bins);

            var expectedContig = ContigInfo.Create("1");
            var expectedEndInterval = ContigAndInterval.Create(ContigInfo.Create("4"), 191034452, 191035452);

            MultiAssert.Equal(expectedContig, wittyerBnd.Contig);
            MultiAssert.Equal(wittyerBnd.EndInterval, expectedEndInterval);
            MultiAssert.Equal(wittyerBnd.Start, 230676U);
            MultiAssert.Equal(wittyerBnd.Stop, 231676U);
            MultiAssert.Equal(wittyerBnd.Win.Start, WittyerConstants.StartingBin);
            MultiAssert.Equal(wittyerBnd.Win.End, null);
            MultiAssert.AssertAll();
        }

        [Fact]
        public static void WittyerIntraBndWorkCorrectly()
        {
            var vcfSettings =
                VcfVariantParserSettings.Create(ImmutableList.Create("proband", "father"), GenomeAssembly.Grch37);
            var bnd1 = VcfVariant.TryParse(GenotypedIntraBnd, vcfSettings).GetOrThrowDebug();

            var bnd2 = VcfVariant.TryParse(GenotypedIntraBndPair, vcfSettings).GetOrThrowDebug();
            var wittyerBnd = WittyerBnd.WittyerBndInternal
                .Create(bnd2, bnd1, "father", PercentDistance, BasepairDistance, Bins);

            var distance = Math.Round(Math.Abs(bnd1.Position - bnd2.Position) * PercentDistance);
            var expectedEndInterval = ContigAndInterval.Create(wittyerBnd.Contig, bnd1.Position - (uint) distance,
                bnd1.Position + (uint) distance);

            MultiAssert.Equal(wittyerBnd.EndInterval, expectedEndInterval);
            MultiAssert.Equal(wittyerBnd.Win.End, 10000U);
            MultiAssert.AssertAll();

            Assert.IsType<WittyerGenotypedSample>(wittyerBnd.Sample);
        }
    }
}