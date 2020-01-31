using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Ilmn.Das.App.Wittyer.Input;
using Ilmn.Das.App.Wittyer.Utilities;
using Ilmn.Das.App.Wittyer.Utilities.Enums;
using Ilmn.Das.App.Wittyer.Vcf;
using Ilmn.Das.App.Wittyer.Vcf.Samples;
using Ilmn.Das.App.Wittyer.Vcf.Variants;
using Ilmn.Das.App.Wittyer.Vcf.Variants.Breakend;
using Ilmn.Das.Core.Tries.Extensions;
using Ilmn.Das.Std.BioinformaticUtils.Bed;
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

        private const string RefCall1 =
            "chr1\t988572\trs552519714\tG\t<DEL>\t.\tPASS\tSVTYPE=DEL;END=988623;\tGT\t0/0\t0/0";

        private const string ReferenceCnvVariant =
            "chr1\t988572\trs552519714\tG\t<DUP>\t.\tPASS\tSVTYPE=DUP;END=988623;\tCN\t2\t2";

        private const string RefCnvVariantNoSvType =
            "chr1\t988572\trs552519714\tG\t<CNV>\t.\tPASS\tEND=988623;\tCN\t2\t2";

        private const string FirstAndLastBaseShared =
            "chr1\t181183\tpacbio_samtools:chr1_181165_0_118\tG\tGGCAGGCGCAGAGAGGCGCGCCGCGCCGGCGCAGGCGCAGAGAGGCGCGCCGCGCCGGCGCAGGCGCAGAGAGGCGCGCCGCGCCGGCGCAGGCGCAGAGAAGGCGCGCCGCGCCGGCG\t.\tPASS\tHAMMING=0;MINOR_AF=0.0105;NUM_INCONSI=2;SVTYPE=INS;NON_REF_IN_KIDS=42;PASS_RATE=0.9979;SOURCE=pb_hg002;PEDIGREE=no_vector;HWE_FISHER=1;CALL_RATE=0.9979\tGT:FT\t0/1:PASS\t0/1:PASS";

        private const double PercentDistance = 0.05;

        private static readonly IReadOnlyList<uint> Bins = ImmutableList.Create(1000U, 10000U);

        private const uint BasepairDistance = 500;

        [Theory]
        [InlineData(ReferenceCnvVariant)]
        [InlineData(RefCall1)]
        [InlineData(RefCnvVariantNoSvType)]
        public static void ParseReferenceVariantWorks([NotNull] string inputVariant)
        {
            var vcfVariant = VcfVariant.TryParse(inputVariant,
                    VcfVariantParserSettings.Create(ImmutableList.Create("NA12878", "haha"), GenomeAssembly.Hg19))
                .GetOrThrowDebug();

            WittyerType.ParseFromVariant(vcfVariant, false, "NA12878", out var actualType);
            Assert.Equal(WittyerType.CopyNumberReference, actualType);
        }

        [Theory]
        [InlineData(BasicVariant, 23675844, 23676845, "normal", 24600630, 24601631,
            nameof(WittyerType.Inversion) + "|10000+")]
        [InlineData(BasicVariant, 23675844, 23676845, "tumor", 24600630, 24601631,
            nameof(WittyerType.Inversion) + "|10000+")]
        [InlineData(GenotypedVariant, 3350089, 3350098, "normal", 3350168, 3350177,
            nameof(WittyerType.Deletion) + "|1-1000")]
        [InlineData(GenotypedVariant, 3350089, 3350098, "tumor", 3350168, 3350177,
            nameof(WittyerType.CopyNumberReference) + "|1-1000")]
        [InlineData(GenotypedCnvVariant, 16819902, 16820903, "tumor", 50780345, 50782036,
            nameof(WittyerType.CopyNumberGain) + "|10000+")]
        [InlineData(FirstAndLastBaseShared, 181176, 181189, "tumor", 181294, 181307,
            nameof(WittyerType.Insertion) + "|1-1000")]
        [InlineData(RefSiteUndeterminedGt, 86670881, 86671160, "normal", 86673652, 86673931,
            nameof(WittyerType.CopyNumberReference) + "|1000-10000")]
        [InlineData(RefCnvVariantNoSvType, 988568, 988575, "normal", 988619, 988626,
            nameof(WittyerType.CopyNumberReference) + "|1-1000")]
        [InlineData(ReferenceCnvVariant, 988568, 988575, "normal", 988619, 988626,
            nameof(WittyerType.CopyNumberReference) + "|1-1000")]
        [InlineData(RefCall, 2704532, 2706014, "normal", 4123598, 4124956,
            nameof(WittyerType.CopyNumberReference) + "|10000+")]
        public static void WittyerVariantCreateCorrectly([NotNull] string variant, uint posStart, uint posEnd, string sampleName,
            uint endStart, uint endEnd, string winner)
        {
            var vcfVariant = VcfVariant.TryParse(variant,
                    VcfVariantParserSettings.Create(ImmutableList.Create("normal", "tumor"), GenomeAssembly.Hg38))
                .GetOrThrowDebug();

            var _ = WittyerType.ParseFromVariant(vcfVariant, false, sampleName, out var type);
            if (type == null)
                throw new NotSupportedException("This test does not handle svType null");
            var wittyerVariant = WittyerVariantInternal
                .Create(vcfVariant, vcfVariant.Samples[sampleName], type, Bins, PercentDistance, BasepairDistance);

            var expectedStart = ContigAndInterval.Create(vcfVariant.Contig, posStart, posEnd);
            var expectedEnd = ContigAndInterval.Create(vcfVariant.Contig, endStart, endEnd);

            MultiAssert.Equal(expectedStart, wittyerVariant.PosInterval);
            MultiAssert.Equal(expectedEnd, wittyerVariant.EndInterval);
            MultiAssert.Equal(winner, wittyerVariant.Win.ToString());
            MultiAssert.AssertAll();
        }

        [Theory]
        [InlineData(BasicVariant, 23676345, 24601131, 23676103, 23676588, 24600990, 24601272)]
        [InlineData(GenotypedVariant, 3350094, 3350173, 3350094, 3350095, 3350172, 3350173)]
        [InlineData(GenotypedCnvVariant, 16820403, 50781191, 16820064, 16820743, 50780345, 50782036)]
        [InlineData(RefSiteUndeterminedGt, 86671020, 86673792, 86671020, 86671021, 86673791, 86673792)]
        [InlineData(RefCnvVariantNoSvType, 988572, 988623, 988572, 988573, 988622, 988623)]
        [InlineData(ReferenceCnvVariant, 988572, 988623, 988572, 988573, 988622, 988623)]
        [InlineData(RefCall, 2705514, 4124099, 2704532, 2705997, 4123641, 4124956)]
        [InlineData(Cnv, 348501, 402501, 348501, 348502, 402500, 402501)]
        public static void WittyerVariantIntervalCorrect([NotNull] string variant, uint start, uint end, 
            uint posStart, uint posEnd, uint endStart, uint endEnd)
        {
            const string sampleName = "tumor";
            var vcfVariant = VcfVariant.TryParse(variant,
                    VcfVariantParserSettings.Create(ImmutableList.Create("normal", sampleName), GenomeAssembly.Hg38))
                .GetOrThrowDebug();

            var _ = WittyerType.ParseFromVariant(vcfVariant, false, sampleName, out var type);
            if (type == null)
                throw new NotSupportedException("This test does not handle svType null");
            var wittyerVariant = WittyerVariantInternal
                .Create(vcfVariant, vcfVariant.Samples[sampleName], type, Bins, PercentDistance, BasepairDistance);

            var expectedStart = ContigAndInterval.Create(vcfVariant.Contig, start, end);
            var expectedPos = BedInterval.Create(posStart, posEnd);
            var expectedEnd = BedInterval.Create(endStart, endEnd);

            MultiAssert.Equal(expectedStart, wittyerVariant);
            MultiAssert.Equal(expectedPos, wittyerVariant.CiPosInterval);
            MultiAssert.Equal(expectedEnd, wittyerVariant.CiEndInterval);
            MultiAssert.AssertAll();
        }

        [Fact]
        public static void WittyerVariantReaderWorks()
        {
            var vcfSettings =
                VcfVariantParserSettings.Create(ImmutableList.Create("proband", "father"), GenomeAssembly.Grch37);
            var ref1 = VcfVariant.TryParse(RefSiteUndeterminedGt, vcfSettings).GetOrThrowDebug();
            WittyerVcfReader.CreateVariant(ref1, ref1.Samples.First().Value, false, "proband",
                new Dictionary<WittyerType, InputSpec>
                {
                    {
                        WittyerType.CopyNumberReference,
                        InputSpec.GenerateCustomInputSpecs(false, new[] {WittyerType.CopyNumberReference}, percentDistance: 0.05).First()
                    }
                }, new Dictionary<IGeneralBnd, IVcfVariant>(), new List<string>(), true);
        }

        [Fact]
        public static void WittyerBndCreateCorrectly()
        {
            var vcfSettings =
                VcfVariantParserSettings.Create(ImmutableList.Create("proband", "father"), GenomeAssembly.Grch37);
            var bnd1 = VcfVariant.TryParse(GenotypedBnd, vcfSettings).GetOrThrowDebug();

            var bnd2 = VcfVariant.TryParse(GenotypedBndPair, vcfSettings).GetOrThrowDebug();
            var wittyerBnd = WittyerBndInternal
                .Create(bnd2, bnd2.Samples["father"], WittyerType.TranslocationBreakend, Bins,
                    BasepairDistance, PercentDistance, bnd1);

            var expectedContig = ContigInfo.Create("1");
            var expectedEndInterval = ContigAndInterval.Create(ContigInfo.Create("4"), 191034451, 191035452);

            MultiAssert.Equal(expectedContig, wittyerBnd.Contig);
            MultiAssert.Equal(expectedEndInterval, wittyerBnd.EndInterval);
            MultiAssert.Equal(230675U, wittyerBnd.Start);
            MultiAssert.Equal(231676U, wittyerBnd.Stop);
            MultiAssert.Equal(WittyerConstants.StartingBin, wittyerBnd.Win.Start);
            MultiAssert.Equal(null, wittyerBnd.Win.End);
            MultiAssert.AssertAll();
        }

        [Fact]
        public static void WittyerIntraBndWorkCorrectly()
        {
            var vcfSettings =
                VcfVariantParserSettings.Create(ImmutableList.Create("proband", "father"), GenomeAssembly.Grch37);
            var bnd1 = VcfVariant.TryParse(GenotypedIntraBnd, vcfSettings).GetOrThrowDebug();

            var bnd2 = VcfVariant.TryParse(GenotypedIntraBndPair, vcfSettings).GetOrThrowDebug();
            var wittyerBnd = WittyerBndInternal
                .Create(bnd2, bnd2.Samples["father"], WittyerType.IntraChromosomeBreakend, Bins, BasepairDistance,
                    PercentDistance, bnd1);

            var distance = Math.Round(Math.Abs(bnd1.Position - bnd2.Position) * PercentDistance);
            var expectedEndInterval = ContigAndInterval.Create(wittyerBnd.Contig, bnd1.Position - (uint) distance - 1,
                bnd1.Position + (uint) distance);

            MultiAssert.Equal(expectedEndInterval, wittyerBnd.EndInterval);
            MultiAssert.Equal(10000U, wittyerBnd.Win.End);
            MultiAssert.AssertAll();

            Assert.IsType<WittyerGenotypedSample>(wittyerBnd.Sample);
        }


        private const string Cnv =
            "1\t348501\tDRAGEN:LOSS:348501:402501\tN\t<DEL>\t3\tPASS\tSVTYPE=CNV;END=402501;SVLEN=54000\tSM:CN:BC:PE\t0.754052:1:48:25,20\t0.754052:2:48:25,20";

        private const string SvCnvDel =
            "1\t348501\tDRAGEN:LOSS:348501:402501\tN\t<DEL>\t3\tPASS\tSVTYPE=DEL;END=402501;SVLEN=54000\tSM:CN:BC:PE\t0.754052:1:48:25,20\t0.754052:2:48:25,20";

        private const string SvCnvDup =
            "1\t348501\tDRAGEN:GAIN:348501:402501\tN\t<DUP>\t3\tPASS\tSVTYPE=DUP;END=402501;SVLEN=54000\tSM:CN:BC:PE\t0.754052:4:48:25,20\t0.754052:2:48:25,20";

        private const string SvDup =
            "1\t348501\tDRAGEN:LOSS:348501:402501\tN\t<DUP>\t3\tPASS\tSVTYPE=DUP;END=402501;SVLEN=54000\tSM:BC:PE\t0.754052:48:25,20\t0.754052:48:25,20";

        private const string SvDel =
            "1\t348501\tDRAGEN:LOSS:348501:402501\tN\t<DEL>\t3\tPASS\tSVTYPE=DEL;END=402501;SVLEN=54000\tSM:BC:PE\t0.754052:48:25,20\t0.754052:48:25,20";

        private const string RefSite =
            "1\t2705514\tCanvas:REF:1:2705514-4124099\tN\t.\t16.22\tPASS\tEND=4124099;CIPOS=-482,482;CIEND=-457,457\t.\t.\t.";

        private const string GtCnRef =
            "X\t2705514\tCanvas:REF:1:2705514-4124099\tN\t<DUP>\t16.22\tPASS\tSVTYPE=DUP;END=4124099;CIPOS=-482,482;CIEND=-457,457\tGT:CN\t1:1\t1:2";

        private const string CnRef =
            "1\t2705514\tCanvas:REF:1:2705514-4124099\tN\t<DUP>\t16.22\tPASS\tSVTYPE=DUP;END=4124099;CIPOS=-482,482;CIEND=-457,457\tCN\t2\t3";

        private const string RefCall =
            "1\t2705514\tCanvas:GAIN:1:2705514-4124099\tN\t<DUP>\t16.22\tPASS\tSVTYPE=DUP;END=4124099;CIPOS=-982,482;CIEND=-457,857\tGT\t0/0\t0";

        private const string DupIsActuallyDel =
            "X\t2705514\tCanvas:REF:1:2705514-4124099\tN\t<DUP>\t16.22\tPASS\tSVTYPE=DUP;END=4124099;CIPOS=-482,482;CIEND=-457,457\tGT:CN\t1:0\t1:2";

        private const string DupIsActuallyUndetermined =
            "X\t2705514\tCanvas:REF:1:2705514-4124099\tN\t<DUP>\t16.22\tPASS\tSVTYPE=DUP;END=4124099;CIPOS=-482,482;CIEND=-457,457\tGT:CN\t1:.\t1:.";

        private const string RefSiteUndeterminedGt =
            "chr1\t86671021\t.\tN\t.\t100\tPASS\tEND=86673792\tGT:CN\t./.:2\t./.:2";

        [Theory]
        [InlineData(RefSite, false, nameof(WittyerType.CopyNumberReference))]
        [InlineData(RefSite, true, nameof(WittyerType.CopyNumberReference))]
        [InlineData(GtCnRef, false, nameof(WittyerType.CopyNumberReference))]
        [InlineData(GtCnRef, true, nameof(WittyerType.CopyNumberReference))]
        [InlineData(CnRef, false, nameof(WittyerType.CopyNumberReference))]
        [InlineData(CnRef, true, nameof(WittyerType.CopyNumberReference))]
        [InlineData(RefCall, false, nameof(WittyerType.CopyNumberReference))]
        [InlineData(RefCall, true, nameof(WittyerType.CopyNumberReference))]
        [InlineData(DupIsActuallyDel, false, nameof(WittyerType.CopyNumberLoss))]
        [InlineData(DupIsActuallyDel, true, nameof(WittyerType.Deletion))]
        [InlineData(Cnv, false, nameof(WittyerType.CopyNumberLoss))]
        [InlineData(Cnv, true, nameof(WittyerType.Deletion))]
        [InlineData(SvCnvDel, false, nameof(WittyerType.CopyNumberLoss))]
        [InlineData(SvCnvDel, true, nameof(WittyerType.Deletion))]
        [InlineData(SvCnvDup, false, nameof(WittyerType.CopyNumberGain))]
        [InlineData(SvCnvDup, true, nameof(WittyerType.Duplication))]
        [InlineData(SvDel, false, nameof(WittyerType.Deletion))]
        [InlineData(SvDel, true, nameof(WittyerType.Deletion))]
        [InlineData(SvDup, false, nameof(WittyerType.Duplication))]
        [InlineData(SvDup, true, nameof(WittyerType.Duplication))]
        [InlineData(DupIsActuallyUndetermined, false, null)]
        [InlineData(DupIsActuallyUndetermined, true, null)]
        [InlineData(RefSiteUndeterminedGt, true, nameof(WittyerType.CopyNumberReference))]
        public static void ParseWittyerVariantType_AssignCorrectType([NotNull] string vcfString, bool isCrossTypeOn,
            [CanBeNull] string expected)
        {
            const string sampleName = "s1";
            var variant = VcfVariant.TryParse(vcfString,
                    VcfVariantParserSettings.Create(ImmutableList.Create(sampleName, "s2"), GenomeAssembly.Hg38))
                .GetOrThrowDebug();
            var reason = WittyerType.ParseFromVariant(variant, isCrossTypeOn, sampleName, out var assignedType);
            if (expected == null)
                Assert.Equal(FailedReason.UndeterminedCn, reason);
            else
                Assert.Equal(WittyerType.Parse(expected), assignedType);
        }
    }
}