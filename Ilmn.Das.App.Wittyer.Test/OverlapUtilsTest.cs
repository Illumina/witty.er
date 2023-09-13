using System.Collections.Generic;
using System.Linq;
using Bio.Util;
using Ilmn.Das.App.Wittyer.Input;
using Ilmn.Das.App.Wittyer.Results;
using Ilmn.Das.App.Wittyer.Utilities;
using Ilmn.Das.App.Wittyer.Utilities.Enums;
using Ilmn.Das.App.Wittyer.Vcf.Readers;
using Ilmn.Das.App.Wittyer.Vcf.Samples;
using Ilmn.Das.App.Wittyer.Vcf.Variants;
using Ilmn.Das.App.Wittyer.Vcf.Variants.Breakend;
using Ilmn.Das.App.Wittyer.Vcf.Variants.Genotype;
using Ilmn.Das.Core.Tries.Extensions;
using Ilmn.Das.Std.AppUtils.Collections;
using Ilmn.Das.Std.AppUtils.Enums;
using Ilmn.Das.Std.BioinformaticUtils.Contigs;
using Ilmn.Das.Std.BioinformaticUtils.GenomicFeatures;
using Ilmn.Das.Std.VariantUtils.Vcf.Headers;

using Ilmn.Das.Std.XunitUtils;
using Moq;
using Xunit;
using static Ilmn.Das.App.Wittyer.Utilities.WittyerConstants;

namespace Ilmn.Das.App.Wittyer.Test
{
    public class OverlapUtilsTest
    {
        public static readonly IContigInfo PrimaryContig = ContigInfo.Create("chr5");

        public static readonly IContigAndInterval PrimaryContigAndInterval =
            ContigAndInterval.Create(PrimaryContig, 1050, 5600);

        public static readonly IContigAndInterval PrimaryFailedContigAndInterval =
            ContigAndInterval.Create(PrimaryContig, 100, 5200);

        public static readonly IContigInfo SecondaryContig = ContigInfo.Create("chr15");
        
        //public static readonly IContigAndInterval SecondaryContigAndInterval 

        [Theory] //1001, 1500, 5001, 5600
        [InlineData(true, 900, 1100, 5200, 5700, true, FailedReason.Unset, MatchEnum.Coordinate, MatchEnum.Allele, MatchEnum.Genotype)]
        [InlineData(false, 800, 900, 5500, 5555, true, FailedReason.BordersTooFarOff, MatchEnum.Coordinate, MatchEnum.Genotype)]
        [InlineData(false, 100, 110, 5110, 5200, false, FailedReason.BordersTooFarOff, MatchEnum.Coordinate)]
        [InlineData(true, 1000, 1200, 5100, 5700, false, FailedReason.GtMismatch, MatchEnum.Coordinate, MatchEnum.Allele)]
        public void GenerateWhatAndWhyWorksForWittyerVariant(bool isAlleleMatch, uint posStart, uint posEnd, uint endStart, uint endEnd, 
            bool isGtMatch, FailedReason reasonResult, params MatchEnum[] matchResults)
        {
            var originalVariant = GetOriginalCnvGtVariant();

            var otherVariant =
                SetupBasicVariant(isAlleleMatch ? PrimaryContigAndInterval : PrimaryFailedContigAndInterval);

            otherVariant.SetupGet(v => v.PosInterval).Returns(ContigAndInterval.Create(PrimaryContig, posStart, posEnd));
            otherVariant.SetupGet(v => v.EndInterval).Returns(ContigAndInterval.Create(PrimaryContig, endStart, endEnd));
            var otherSample = new Mock<IWittyerGenotypedSample>();

            var gt = new Mock<IGenotypeInfo>();
            if (originalVariant.Sample is IWittyerGenotypedSample gtSample)
            {
                gt.Setup(g => g.Equals(gtSample.Gt)).Returns(isGtMatch);
            }
            
            otherSample.SetupGet(s => s.Gt).Returns(gt.Object);

            otherVariant.SetupGet(v => v.Sample).Returns(otherSample.Object);
            var failedReasons = new List<FailedReason>();
            var (what, why) = OverlappingUtils.GenerateWhatAndWhy(otherVariant.Object, failedReasons,
                originalVariant, OverlappingUtils.VariantMatch, false, false,
                DefaultTandemRepeatSpec);
            Assert.Equal(reasonResult, why);
            Assert.True(what.SetEquals(matchResults));
        }

        [Theory]
        [InlineData(true, 900, 1100, 5200, 5700, true, 3, FailedReason.Unset, MatchEnum.Coordinate, MatchEnum.Allele, MatchEnum.Genotype)]
        [InlineData(true, 900, 1100, 5200, 5700, true, 2, FailedReason.CnMismatch, MatchEnum.Coordinate, MatchEnum.Genotype)]
        [InlineData(false, 800, 900, 5500, 5555, true, 3,  FailedReason.BordersTooFarOff, MatchEnum.Coordinate, MatchEnum.Genotype)]
        [InlineData(false, 100, 110, 5110, 5200, false, 1, FailedReason.BordersTooFarOff, MatchEnum.Coordinate)]
        [InlineData(true, 1000, 1200, 5100, 5700, false, 3, FailedReason.GtMismatch, MatchEnum.Coordinate, MatchEnum.Allele)]
        [InlineData(true, 1000, 1200, 5100, 5700, false, 1, FailedReason.CnMismatch, MatchEnum.Coordinate)]
        public void GenerateWhatAndWhyWorksWithCnSample(bool isAlleleMatch, uint posStart, uint posEnd, uint endStart, uint endEnd,
            bool isGtMatch, uint cn, FailedReason reasonResult, params MatchEnum[] matchResults)
        {
            var originalVariant = GetOriginalCnvGtVariant();
            var otherVariant =
                SetupBasicVariant(isAlleleMatch ? PrimaryContigAndInterval : PrimaryFailedContigAndInterval);

            otherVariant.SetupGet(v => v.PosInterval).Returns(ContigAndInterval.Create(PrimaryContig, posStart, posEnd));
            otherVariant.SetupGet(v => v.EndInterval).Returns(ContigAndInterval.Create(PrimaryContig, endStart, endEnd));
            var otherSample = new Mock<IWittyerGenotypedCopyNumberSample>();

            var gt = new Mock<IGenotypeInfo>();
            if (originalVariant.Sample is IWittyerGenotypedSample gtSample)
                gt.Setup(g => g.Equals(gtSample.Gt)).Returns(isGtMatch);

            otherSample.SetupGet(s => s.Gt).Returns(gt.Object);
            otherSample.SetupGet(s => s.Cn).Returns(cn);

            otherVariant.SetupGet(v => v.Sample).Returns(otherSample.Object);

            var (what, why) = OverlappingUtils.GenerateWhatAndWhy(otherVariant.Object, new List<FailedReason>(),
                originalVariant, OverlappingUtils.VariantMatch, false, false,
                DefaultTandemRepeatSpec);
            
            Assert.Equal(reasonResult, why);
            Assert.Equal(matchResults.OrderBy(it => (int)it).StringJoin(","),
                what.OrderBy(it => (int)it).StringJoin(","));
        }

        
        private static IWittyerVariant GetOriginalCnvGtVariant()
        {
            var variant = SetupBasicVariant(PrimaryContigAndInterval);
            variant.SetupGet(v => v.PosInterval).Returns(ContigAndInterval.Create(PrimaryContig, 1001, 1500));
            variant.SetupGet(v => v.EndInterval).Returns(ContigAndInterval.Create(PrimaryContig, 5001, 5601));
            
            var sample = new Mock<IWittyerGenotypedCopyNumberSample>();
            sample.SetupGet(s => s.Cn).Returns(3);

            sample.SetupGet(s => s.Gt).Returns(new Mock<IGenotypeInfo>().Object);
            variant.SetupGet(v => v.Sample).Returns(sample.Object);

            return variant.Object;
        }

        private static Mock<IWittyerVariant> SetupBasicVariant(IContigAndInterval contigAndInterval)
        {
            var variant = new Mock<IWittyerVariant>();
            variant.SetupGet(v => v.Contig).Returns(contigAndInterval.Contig);
            variant.SetupGet(v => v.Start).Returns(contigAndInterval.Start);
            variant.SetupGet(v => v.Stop).Returns(contigAndInterval.Stop);
            variant.SetupGet(v => v.IsStartInclusive).Returns(contigAndInterval.IsStartInclusive);
            variant.SetupGet(v => v.IsStopInclusive).Returns(contigAndInterval.IsStopInclusive);
            variant.SetupGet(v => v.VariantType).Returns(WittyerType.CopyNumberGain);
            return variant;
        }

        private const string TrueDup =
            "13\t50378035\t15742\tN\t<DUP>\t.\tPASS\tAF=0.25;CHR2=13;END=50379120;IMPRECISE;Kurtosis_quant_start=-1.768175;Kurtosis_quant_stop=-1.765310;RE=12;STD_quant_start=131.872287;STD_quant_stop=134.255726;STRANDS=-+;STRANDS2=5,9,5,9;SUPTYPE=SR;SVLEN=1085;SVMETHOD=Snifflesv1.0.3;SVTYPE=DUP\tGT:DR:DV\t./1:.:.";
        private const string QueryDup =
            "13\t50378134\tMantaDUP:TANDEM:144950:0:1:0:0:0\tC\t<DUP:TANDEM>\t521\tPASS\tCIEND=0,19;SVTYPE=DUP;SVLEN=1089;CIPOS=0,19;END=50379223;HOMSEQ=GAGACTCTGTCTCAAAAAA;HOMLEN=19\tGT:FT:GQ:PL:PR:SR\t0/1:PASS:521:571,0,984:41,8:31,13";

        private const string TrueDup2 =
            "chr2\t4526601\t1832\tN\t<DUP>\t.\tPASS\tAF=0.244898;CHR2=2;END=213782753;Kurtosis_quant_start=1.919276;Kurtosis_quant_stop=-0.621302;PRECISE;RE=12;STD_quant_start=1.183216;STD_quant_stop=1.140175;STRANDS=-+;STRANDS2=7,5,7,5;SUPTYPE=SR;SVLEN=209256152;SVMETHOD=Snifflesv1.0.3;SVTYPE=DUP\tGT:DR:DV\t./1:.:.";
        private const string QueryDup2 =
            "chr2\t158187\tMantaDUP:TANDEM:16508:0:4:0:0:0\tC\t<DUP:TANDEM>\t111\tPASS\tCIEND=0,7;SVTYPE=DUP;SVLEN=196043921;CIPOS=0,7;END=196202108;HOMSEQ=TTAGTTA;HOMLEN=7\tGT:FT:GQ:PL:PR:SR\t0/1:PASS:111:161,0,645:25,2:22,12";


        [Theory]
        [InlineData(TrueDup, QueryDup, true)]
        [InlineData(TrueDup2, QueryDup2, false)]
        public void OverlapWorks_Dup(string truthVar, string queryVar, bool isTp)
        {
            const string sampleName = "blah";
            var vcfVariantParserSettings = VcfVariantParserSettings.Create(new List<string> { sampleName });
            var baseVariant = VcfVariant.TryParse(truthVar, vcfVariantParserSettings).GetOrThrow();
            const bool isCrossTypeOn = true;
            var inputSpecs = InputSpec.GenerateCustomInputSpecs(isCrossTypeOn, null, percentThreshold: PercentDistance).ToDictionary(s => s.VariantType, s => s);
            
            var bndSet = new Dictionary<IGeneralBnd, IVcfVariant>();
            var errorList = new List<string>();
            var truthVs = WittyerVcfReader.CreateVariants(baseVariant, true, sampleName,
                    inputSpecs, bndSet, errorList).OfType<IMutableWittyerSimpleVariant>().Select(v => v).ToList();
            Assert.Equal(1, truthVs.Count);
            baseVariant = VcfVariant.TryParse(queryVar, vcfVariantParserSettings).GetOrThrow();
            var queryVs = WittyerVcfReader.CreateVariants(baseVariant, false, sampleName,
                inputSpecs, bndSet, errorList).OfType<IMutableWittyerVariant>().ToList();
            Assert.Equal(1, queryVs.Count);
            var tree = TruthForest.Create(sampleName, VcfHeader.CreateBuilder(VcfVersion.FourPointOne).Build());
            foreach (var truthV in truthVs)
                tree.AddTarget(truthV);
            foreach (var (queryV, truthV) in queryVs.Zip(truthVs))
            {
                Assert.Equal(WittyerType.Duplication, truthV.VariantType);
                Assert.Equal(WittyerType.Duplication, queryV.VariantType);
                OverlappingUtils.DoOverlapping(tree.VariantTrees, queryV, OverlappingUtils.VariantMatch, isCrossTypeOn, true);
                queryV.Finalize(WitDecision.FalsePositive, EvaluationMode.CrossTypeAndSimpleCounting, null, DefaultMaxMatches);
                truthV.Finalize(WitDecision.FalseNegative, EvaluationMode.CrossTypeAndSimpleCounting, null, DefaultMaxMatches);
                Assert.Equal(isTp ? WitDecision.TruePositive : WitDecision.FalsePositive, queryV.Sample.Wit);
                Assert.Equal(isTp ? WitDecision.TruePositive : WitDecision.FalseNegative, truthV.Sample.Wit);
            }
        }
        
        
        private const string TrueDel =
            "chr21\t15330008\t21029\tN\t<DEL>\t.\tPASS\tAF=0.625;CHR2=21;END=15330076;Kurtosis_quant_start=-0.260669;Kurtosis_quant_stop=-0.539578;PRECISE;RE=15;STD_quant_start=3.464102;STD_quant_stop=3.535534;STRANDS=+-;STRANDS2=5,10,5,10;SUPTYPE=AL;SVLEN=68;SVMETHOD=Snifflesv1.0.3;SVTYPE=DEL\tGT:DR:DV\t./1:.:.";
        private const string QueryDel =
            "chr21\t15330011\tMantaDEL:190335:0:1:0:0:0\tGAGGATTCCATGATTACTGCAAATAATTTGATCCACTGAAGAGTTATACAGGCATAAATATTATGAAAGTC\tG\t540\tPASS\tSVTYPE=DEL;SVLEN=-70;CIPOS=0,1;END=15330081;HOMSEQ=A;HOMLEN=1;CIGAR=1M70D\tGT:FT:GQ:PL:PR:SR\t0/1:PASS:386:590,0,383:10,0:32,16";

        private const string TrueDel2 =
            "chr16\t12002335\t17668\tN\t<DEL>\t.\tPASS\tAF=0.893617;CHR2=16;END=12002660;Kurtosis_quant_start=7.083217;Kurtosis_quant_stop=-1.009518;PRECISE;RE=42;STD_quant_start=8.156213;STD_quant_stop=6.586422;STRANDS=+-;STRANDS2=18,24,18,24;SUPTYPE=AL,SR;SVLEN=325;SVMETHOD=Snifflesv1.0.3;SVTYPE=DEL\tGT:DR:DV\t./1:.:.";
        private const string QueryDel2 =
            "chr16\t12002319\tMantaDEL:162718:0:0:1:0:0\tTAAAAATACAAAAATTAGGCCGGGCGCGGTGGCTCACGCCTGTAATCCCAGCACTTTGGGAGGCCGAGGCGGGCGGATCACGAGGTCAGGAGATCGAGACCATCCCGGCTAAAACGGTGAAACCCCGTCTCTACTAAAAATACAAAAAATTAGCCGGGCGTAGTGGCGGGCGCCTGTAGTCCCAGCTACTTGGGAGACTGAGGCGGGAGAATGGCGTGAACCCGGGAGGCGGAGCTTGCAGTGAGCCGAGATCCCGCCACTGCACTCCAGCCTGGGCGACAGAGCGAGACTCCGTCTCAAAAAAAAAAAAAAAAAAAAAAAAAAAA\tT\t633\tPASS\tSVTYPE=DEL;SVLEN=-325;CIPOS=0,17;END=12002644;HOMSEQ=AAAAATACAAAAATTAG;HOMLEN=17;CIGAR=1M325D\tGT:FT:GQ:PL:PR:SR\t1/1:PASS:59:686,62,0:0,24:0,15";

        private const string TrueDel3 =
            "chr3\t196055975\t4646\tN\t<DEL>\t.\tPASS\tAF=0.72;CHR2=3;END=196056907;IMPRECISE;Kurtosis_quant_start=-0.730920;Kurtosis_quant_stop=-0.745235;RE=18;STD_quant_start=43.155533;STD_quant_stop=45.532406;STRANDS=+-;STRANDS2=11,7,11,7;SUPTYPE=AL,SR;SVLEN=932;SVMETHOD=Snifflesv1.0.3;SVTYPE=DEL\tGT:DR:DV\t./1:.:.";
        private const string QueryDel3 =
            "chr3\t196055918\tMantaDEL:46457:0:1:0:0:0\tGATTCTCCTGCCTCAGCCTCCCAAGTAGCTGGGATTACAGGTGCCCGCCACCATGCCCGGTAATTTTTTGTATTTTTAGTAGAGACGGGGTTTCACCGTGTTAGCCAGGACGGTCTTGATCTCCTGACCTCGTGATCCGCCTGTCTCGGGCTCCCAAAGTGCTGGGATTACAGGCATGAGCCACCGCGCCCGGCCAATAAATGATTTTTTAAGAAGATGACAATTCAGCGGGAAAAAGTCTCTTCCACAAATGGTGCCGGAACAATGCACATGCAAAACAAAACTGCCTTCACTATTACTTCACAACATATTCAAAAATTCACTTGAAATGGAGCATGGGTCTAAAAGCTTTATAAAATTTCTAGAATAAAACAAAGGAGAAAAACACTGGGACCTTGGGTTAGATAAAGGTTTCTTACTAAAATGCAAAAAGTACAATCCACAAAACATGATTAATCAGACTGCTTAAAAATTTAAATGTCAGCTGGGTGCTTGCCTATAATCCCAGCTACTCGGAGGTTGAGGCAGGAAAATCGCTTGAACCCAGGAGGCAGAGGCTGCAGTGAGCTGAGATCGCGCCACTGCACTTCAGCCTGGGCGACAAGAGCAAGCGACTCCATCTAAAAAAAAAAAATCATGCTAAAAGAAATAAGCCAGACACTAAAGGACAAATATTGTATGATTCCACTTATATGAAATATTCAAAATAACAACTTTATAGAGAAAGAAAGTAGACTGGAGGTTATCAGGGACTGTGGGGAGGGAGGAGAGAATGGGGAGTTATTGTTTAATGGTTACAGAGGTTTTCAGGTTTTGTTTTGTGTTGTTTTGTTTTTTTTTTTTGAGACAGAGTCTTGCTCTGTCAGCCAGGCTGGAGTGCAGTGGCGCAATCTCAGCTCACTGCAACCTCTGCCTCCTGGGCTCAAGCA\tG\t480\tPASS\tSVTYPE=DEL;SVLEN=-928;CIPOS=0,33;END=196056846;HOMSEQ=ATTCTCCTGCCTCAGCCTCCCAAGTAGCTGGGA;HOMLEN=33;CIGAR=1M928D\tGT:FT:GQ:PL:PR:SR\t0/1:PASS:480:530,0,680:33,10:25,11";

        private const string TrueDel4 =
            "chr12\t1194331\t14410_2\tN\t<DEL>\t.\tPASS\tAF=0.458333;CHR2=12;END=1200054;IMPRECISE;Kurtosis_quant_start=2.500400;Kurtosis_quant_stop=2.497848;RE=11;STD_quant_start=63.878791;STD_quant_stop=70.860426;STRANDS=+-;STRANDS2=6,5,6,5;SUPTYPE=SR;SVLEN=5723;SVMETHOD=Snifflesv1.0.3;SVTYPE=DEL\tGT:DR:DV\t./1:.:.";
        private const string QueryDel4 =
            "chr12\t1194873\tMantaDEL:132955:0:2:1:0:0\tG\t<DEL>\t413\tPASS\tSVTYPE=DEL;SVINSLEN=9;SVINSSEQ=TTGTTGTTG;SVLEN=-5180;END=1200053\tGT:FT:GQ:PL:PR:SR\t0/1:PASS:47:463,0,44:5,2:0,12";

        [Theory]
        [InlineData(TrueDel, QueryDel, true)]
        [InlineData(TrueDel2, QueryDel2, true)]
        [InlineData(TrueDel3, QueryDel3, true)]
        [InlineData(TrueDel4, QueryDel4, true)]
        public void OverlapWorks_Del(string truthVar, string queryVar, bool isTp)
        {
            const string sampleName = "blah";
            var vcfVariantParserSettings = VcfVariantParserSettings.Create(new List<string> { sampleName });
            var baseVariant = VcfVariant.TryParse(truthVar, vcfVariantParserSettings).GetOrThrow();
            const bool isCrossTypeOn = true;
            var inputSpecs = InputSpec.GenerateCustomInputSpecs(isCrossTypeOn, null, percentThreshold: PercentDistance).ToDictionary(s => s.VariantType, s => s);
            
            var bndSet = new Dictionary<IGeneralBnd, IVcfVariant>();
            var errorList = new List<string>();
            var truthVs = WittyerVcfReader.CreateVariants(baseVariant, true, sampleName,
                    inputSpecs, bndSet, errorList).OfType<IMutableWittyerSimpleVariant>().Select(v => v).ToList();
            Assert.Equal(1, truthVs.Count);
            baseVariant = VcfVariant.TryParse(queryVar, vcfVariantParserSettings).GetOrThrow();
            var queryVs = WittyerVcfReader.CreateVariants(baseVariant, false, sampleName,
                inputSpecs, bndSet, errorList).OfType<IMutableWittyerVariant>().ToList();
            Assert.Equal(1, queryVs.Count);
            var tree = TruthForest.Create(sampleName, VcfHeader.CreateBuilder(VcfVersion.FourPointOne).Build());
            foreach (var truthV in truthVs)
                tree.AddTarget(truthV);
            foreach (var (queryV, truthV) in queryVs.Zip(truthVs))
            {
                Assert.Equal(WittyerType.Deletion, truthV.VariantType);
                Assert.Equal(WittyerType.Deletion, queryV.VariantType);
                OverlappingUtils.DoOverlapping(tree.VariantTrees, queryV, OverlappingUtils.VariantMatch, isCrossTypeOn, true);
                queryV.Finalize(WitDecision.FalsePositive, EvaluationMode.CrossTypeAndSimpleCounting, null, DefaultMaxMatches);
                truthV.Finalize(WitDecision.FalseNegative, EvaluationMode.CrossTypeAndSimpleCounting, null, DefaultMaxMatches);
                Assert.Equal(isTp ? WitDecision.TruePositive : WitDecision.FalsePositive, queryV.Sample.Wit);
                Assert.Equal(isTp ? WitDecision.TruePositive : WitDecision.FalseNegative, truthV.Sample.Wit);
            }
        }

        private const string TrueInv =
            "chr13\t82356873\t15960_1\tN\t<INV>\t.\tPASS\tAF=0.6875;CHR2=13;END=82356960;Kurtosis_quant_start=-0.283624;Kurtosis_quant_stop=-0.293709;PRECISE;RE=11;STD_quant_start=3.847077;STD_quant_stop=9.038805;STRANDS=++;STRANDS2=6,5,5,6;SUPTYPE=SR;SVLEN=87;SVMETHOD=Snifflesv1.0.3;SVTYPE=INV\tGT:DR:DV\t./1:.:.";
        private const string QueryInv =
            "chr13\t82356868\tMantaINV:147163:0:0:0:1:0\tT\t<INV>\t999\tPASS\tCIEND=-4,0;SVTYPE=INV;EVENT=MantaINV:147163:0:0:0:1:0;SVLEN=95;CIPOS=0,4;JUNCTION_QUAL=999;END=82356963;HOMSEQ=TATA;HOMLEN=4;INV3\tGT:FT:GQ:PL:PR:SR\t0/1:PASS:332:999,0,329:14,5:0,34";

        [Theory]
        [InlineData(TrueInv, QueryInv, true)]
        public void OverlapWorks_Inv(string truthVar, string queryVar, bool isTp)
        {
            const string sampleName = "blah";
            var vcfVariantParserSettings = VcfVariantParserSettings.Create(new List<string> { sampleName });
            var baseVariant = VcfVariant.TryParse(truthVar, vcfVariantParserSettings).GetOrThrow();
            const bool isCrossTypeOn = true;
            var inputSpecs = InputSpec.GenerateCustomInputSpecs(isCrossTypeOn, null, percentThreshold: PercentDistance).ToDictionary(s => s.VariantType, s => s);
            
            var bndSet = new Dictionary<IGeneralBnd, IVcfVariant>();
            var errorList = new List<string>();
            var truthVs = WittyerVcfReader.CreateVariants(baseVariant, true, sampleName,
                    inputSpecs, bndSet, errorList).OfType<IMutableWittyerSimpleVariant>().Select(v => v).ToList();
            Assert.Equal(1, truthVs.Count);
            baseVariant = VcfVariant.TryParse(queryVar, vcfVariantParserSettings).GetOrThrow();
            var queryVs = WittyerVcfReader.CreateVariants(baseVariant, false, sampleName,
                inputSpecs, bndSet, errorList).OfType<IMutableWittyerVariant>().ToList();
            Assert.Equal(1, queryVs.Count);
            var tree = TruthForest.Create(sampleName, VcfHeader.CreateBuilder(VcfVersion.FourPointOne).Build());
            foreach (var truthV in truthVs)
                tree.AddTarget(truthV);
            foreach (var (queryV, truthV) in queryVs.Zip(truthVs))
            {
                Assert.Equal(WittyerType.Inversion, truthV.VariantType);
                Assert.Equal(WittyerType.Inversion, queryV.VariantType);
                OverlappingUtils.DoOverlapping(tree.VariantTrees, queryV, OverlappingUtils.VariantMatch, isCrossTypeOn, true);
                queryV.Finalize(WitDecision.FalsePositive, EvaluationMode.CrossTypeAndSimpleCounting, null, DefaultMaxMatches);
                truthV.Finalize(WitDecision.FalseNegative, EvaluationMode.CrossTypeAndSimpleCounting, null, DefaultMaxMatches);
                Assert.Equal(isTp ? WitDecision.TruePositive : WitDecision.FalsePositive, queryV.Sample.Wit);
                Assert.Equal(isTp ? WitDecision.TruePositive : WitDecision.FalseNegative, truthV.Sample.Wit);
            }
        }
        
        private const string TrueVntrPerfect = "0\t60913\tperfect_match\tA\t<CNV:TR>,<CNV:TR>\t.\tPASS\tSVTYPE=CNV;EVENTTYPE=VNTR;SVLEN=137,137;END=61050;RUS=TGAG…;RUL=71;CN=0.75,0.75;REFRUC=10;RUC=1.50,1.50\tGT:PS:CN\t1/2:125098:1.50";
        private const string TrueVntrAllele = "0\t61060\tallele_match\tA\t<CNV:TR>,<CNV:TR>\t.\tPASS\tSVTYPE=CNV;EVENTTYPE=VNTR;SVLEN=137,137;END=61913;RUS=TGAG…;RUL=71;CN=0.75,0.75;RUC=1.50,1.50\tGT:PS:CN\t1/1:125098:1.50";
        private const string TrueVntrLocal = "0\t61963\tlocal_match\tA\t<CNV:TR>,<CNV:TR>\t.\tPASS\tSVTYPE=CNV;EVENTTYPE=VNTR;SVLEN=137,137;END=62060;RUS=TGAG…;RUL=71;CN=0.75,0.75;RUC=3.50,1.50\tGT:PS:CN\t0/1:125098:1.50";
        private const string TrueVntrThreshold1 = "0\t62160\tthreshold_match_1\tA\t<CNV:TR>,<CNV:TR>\t.\tPASS\tSVTYPE=CNV;EVENTTYPE=VNTR;SVLEN=137,137;END=62963;RUS=TGAG…;RUL=71;CN=0.75,0.75;RUC=2.00,1.50\tGT:PS:CN\t1/1:125098:1.50";
        private const string TrueVntrThreshold2 = "0\t63053\tthreshold_match_2\tA\t<CNV:TR>,<CNV:TR>\t.\tPASS\tSVTYPE=CNV;EVENTTYPE=VNTR;SVLEN=137,137;END=64060;RUS=TGAG…;RUL=71;CN=0.75,0.75;RUC=1.50,1.00\tGT:PS:CN\t1/2:125098:1.50";
        
        private const string QueryVntrPerfect = "0\t60913\tperfect_match\tA\t<CNV:TR>,<CNV:TR>\t.\tPASS\tSVTYPE=CNV;EVENTTYPE=VNTR;SVLEN=137,137;END=61050;RUS=TGAG…;RUL=71;CN=0.75,0.75;REFRUC=10;RUC=1.50,1.50\tGT:PS:CN\t1/2:125098:1.50";
        private const string QueryVntrPerfectExceptRefRuc = "0\t60913\tperfect_match\tA\t<CNV:TR>,<CNV:TR>\t.\tPASS\tSVTYPE=CNV;EVENTTYPE=VNTR;SVLEN=137,137;END=61050;RUS=TGAG…;RUL=71;CN=0.75,0.75;REFRUC=1;RUC=1.50,1.50\tGT:PS:CN\t1/2:125098:1.50";
        private const string QueryVntrAllele = "0\t61060\tallele_match\tA\t<CNV:TR>,<CNV:TR>\t.\tPASS\tSVTYPE=CNV;EVENTTYPE=VNTR;SVLEN=137,137;END=61913;RUS=TGAG…;RUL=71;CN=0.75,0.75;RUC=1.50,1.50\tGT:PS:CN\t0/1:125098:1.50";
        private const string QueryVntrLocal = "0\t61963\tlocal_match\tA\t<CNV:TR>,<CNV:TR>\t.\tPASS\tSVTYPE=CNV;EVENTTYPE=VNTR;SVLEN=137,137;END=62060;RUS=TGAG…;RUL=71;CN=0.75,0.75;RUC=1.50,1.50\tGT:PS:CN\t0/2:125098:1.50";
        private const string QueryVntrThreshold1 = "0\t62160\tthreshold_match_1\tA\t<CNV:TR>,<CNV:TR>\t.\tPASS\tSVTYPE=CNV;EVENTTYPE=VNTR;SVLEN=137,137;END=62963;RUS=TGAG…;RUL=71;CN=0.75,0.75;RUC=1.50,1.50\tGT:PS:CN\t1/1:125098:1.50";
        private const string QueryVntrThreshold2 = "0\t63053\tthreshold_match_2\tA\t<CNV:TR>,<CNV:TR>\t.\tPASS\tSVTYPE=CNV;EVENTTYPE=VNTR;SVLEN=137;END=64060;RUS=TGAG…;RUL=71;CN=0.75,0.75;RUC=1.50,1.50\tGT:PS:CN\t1/2:125098:1.50";
        
        [Theory]
        [InlineData(TrueVntrPerfect, QueryVntrPerfect, true, 2)]
        [InlineData(TrueVntrPerfect, QueryVntrPerfectExceptRefRuc, true, 2)]
        [InlineData(TrueVntrAllele, QueryVntrAllele, false, 1)]
        [InlineData(TrueVntrLocal, QueryVntrLocal, false, 1)]
        [InlineData(TrueVntrThreshold1, QueryVntrThreshold1, true, 1)]
        [InlineData(TrueVntrThreshold2, QueryVntrThreshold2, true, 2)]
        public void OverlapWorks_Vntr(string truthVar, string queryVar, bool isTp, int count)
        {
            const string sampleName = "blah";
            var vcfVariantParserSettings = VcfVariantParserSettings.Create(new List<string> { sampleName });
            var baseVariant = VcfVariant.TryParse(truthVar, vcfVariantParserSettings).GetOrThrow();
            const bool isCrossTypeOn = true;
            var inputSpecs = InputSpec.GenerateCustomInputSpecs(isCrossTypeOn, null).ToDictionary(s => s.VariantType, s => s);
            
            var bndSet = new Dictionary<IGeneralBnd, IVcfVariant>();
            var errorList = new List<string>();
            var truthVs = WittyerVcfReader.CreateVariants(baseVariant, true, sampleName,
                    inputSpecs, bndSet, errorList).OfType<IMutableWittyerSimpleVariant>().Select(v => v).ToList();
            MultiAssert.Equal(count, truthVs.Count);
            baseVariant = VcfVariant.TryParse(queryVar, vcfVariantParserSettings).GetOrThrow();
            var queryVs = WittyerVcfReader.CreateVariants(baseVariant, false, sampleName,
                inputSpecs, bndSet, errorList).OfType<IMutableWittyerVariant>().ToList();
            MultiAssert.Equal(count, queryVs.Count);
            var tree = TruthForest.Create(sampleName, VcfHeader.CreateBuilder(VcfVersion.FourPointOne).Build());
            foreach (var truthV in truthVs)
                tree.AddTarget(truthV);
            foreach (var (queryV, truthV) in queryVs.Zip(truthVs))
            {
                MultiAssert.Equal(WittyerType.CopyNumberTandemRepeat, truthV.VariantType);
                MultiAssert.Equal(WittyerType.CopyNumberTandemRepeat, queryV.VariantType);
                OverlappingUtils.DoOverlapping(tree.VariantTrees, queryV, OverlappingUtils.VariantMatch, isCrossTypeOn, true);
                queryV.Finalize(WitDecision.FalsePositive, EvaluationMode.GenotypeMatching, null, DefaultMaxMatches);
                truthV.Finalize(WitDecision.FalseNegative, EvaluationMode.GenotypeMatching, null, DefaultMaxMatches);
                MultiAssert.Equal(isTp ? WitDecision.TruePositive : WitDecision.FalsePositive, queryV.Sample.Wit);
                MultiAssert.Equal(isTp ? WitDecision.TruePositive : WitDecision.FalseNegative, truthV.Sample.Wit);
            }
            MultiAssert.AssertAll();
        }

        private const string TrueIns =
            "chr19\t8817471\t19838_1\tN\t<INS>\t.\tPASS\tAF=0.818182;CHR2=19;END=8817518;IMPRECISE;Kurtosis_quant_start=3.424836;Kurtosis_quant_stop=-0.210366;RE=27;STD_quant_start=15.711338;STD_quant_stop=41.131122;STRANDS=+-;STRANDS2=16,11,16,11;SUPTYPE=AL;SVLEN=129;SVMETHOD=Snifflesv1.0.3;SVTYPE=INS\tGT:DR:DV\t./1:.:.";
        private const string QueryIns =
            "chr19\t8817571\tMantaINS:180650:0:0:2:0:0\tT\tTATATAATATATTTTATATTATATAATATATAATATATATAATATATTATATAATATATAATATATATAATATATTATATAATATATTATATAATATATTATATAATATATTTTTATATTATATAATATATAATATAA\t203\tPASS\tSVTYPE=INS;SVLEN=137;END=8817571;CIGAR=1M137I\tGT:FT:GQ:PL:PR:SR\t1/1:PASS:15:256,18,0:0,0:0,8";

        private const string TrueIns2 =
            "chr17\t39886379\tMantaINS:170945:0:0:0:0:1\tA\tAAGAAAGAAAGAAAGAAAGAAAGAAAGAAAGAAGGAAAGAAAGAAAGAAAGAAGGAAAG\t507\tPASS\tSVTYPE=INS;CIPOS=0,1;END=39886379;HOMSEQ=A;HOMLEN=1;CIGAR=1M58I\tGT:FT:GQ:PL:PR:SR\t1/1:PASS:35:560,38,0:0,0:0,15";
        private const string QueryIns2 =
            "chr17\t39886275\t18619\tN\t<INS>\t.\tPASS\tAF=0.909091;CHR2=17;END=39886303;Kurtosis_quant_start=0.004589;Kurtosis_quant_stop=1.257711;PRECISE;RE=30;STD_quant_start=7.366591;STD_quant_stop=6.562520;STRANDS=+-;STRANDS2=16,14,16,14;SUPTYPE=AL;SVLEN=55;SVMETHOD=Snifflesv1.0.3;SVTYPE=INS\tGT:DR:DV\t./1:.:.";

        private const string TrueIns3 =
            "chr6\t152171109\t8501\tN\t<INS>\t.\tPASS\tAF=0.777778;CHR2=6;END=152171158;IMPRECISE;Kurtosis_quant_start=-0.645802;Kurtosis_quant_stop=31.034996;RE=42;STD_quant_start=11.623048;STD_quant_stop=30.093505;STRANDS=+-;STRANDS2=27,15,27,15;SUPTYPE=AL;SVLEN=105;SVMETHOD=Snifflesv1.0.3;SVTYPE=INS\tGT:DR:DV\t./1:.:.";
        private const string QueryIns3 =
            "chr6\t152171249\tMantaINS:82429:0:0:3:1:0\tT\tTTATATATATATTATTTTATATGCATATAAAATAATATATATATAATTTTATATGCATATAAAATAATATATATATATTATTTTATATGCATATAAAATAATATA\t763\tPASS\tSVTYPE=INS;SVLEN=104;CIPOS=0,11;END=152171249;HOMSEQ=TATATATATAT;HOMLEN=11;CIGAR=1M104I\tGT:FT:GQ:PL:PR:SR\t1/1:PASS:50:816,53,0:0,2:0,19";

        private const string TrueIns4 =
            "chr6\t152171253\tMantaINS:82429:0:0:3:1:0\tT\tTATATGTATGTATACAATACACACACATATAACA\t763\tPASS\tSVTYPE=INS;SVLEN=34;CIPOS=0,11;END=152171253;HOMSEQ=TATATATATAT;HOMLEN=11;CIGAR=1M104I\tGT:FT:GQ:PL:PR:SR\t1/1:PASS:50:816,53,0:0,2:0,19";
        private const string QueryIns4 =
            "chr6\t152171249\tMantaINS:82429:0:0:3:1:0\tT\tTGTATGTATACAATACAACACATATAACTATA\t763\tPASS\tSVTYPE=INS;SVLEN=32;CIPOS=0,11;END=152171249;HOMSEQ=TATATATATAT;HOMLEN=11;CIGAR=1M104I\tGT:FT:GQ:PL:PR:SR\t1/1:PASS:50:816,53,0:0,2:0,19";
        
        private const string TrueIns5 =
            "chr6\t152171249\tMantaINS:82429:0:0:3:1:0\tT\tTATATGTATGTATACAATACACACACATATAACA\t763\tPASS\tSVTYPE=INS;SVLEN=34;CIPOS=0,11;END=152171249;HOMSEQ=TATATATATAT;HOMLEN=11;CIGAR=1M104I\tGT:FT:GQ:PL:PR:SR\t1/1:PASS:50:816,53,0:0,2:0,19";
        private const string QueryIns5 =
            "chr6\t152171249\tMantaINS:82429:0:0:3:1:0\tT\tTGTATGTATACAATACAACACATATAACTATA\t763\tPASS\tSVTYPE=INS;SVLEN=32;CIPOS=0,11;END=152171249;HOMSEQ=TATATATATAT;HOMLEN=11;CIGAR=1M104I\tGT:FT:GQ:PL:PR:SR\t1/1:PASS:50:816,53,0:0,2:0,19";
        
        private const string TrueIns6 =
            "chr19\t8817471\t19838_1\tN\t<INS>\t.\tPASS\tAF=0.818182;CHR2=19;END=8817518;IMPRECISE;Kurtosis_quant_start=3.424836;Kurtosis_quant_stop=-0.210366;RE=27;STD_quant_start=15.711338;STD_quant_stop=41.131122;STRANDS=+-;STRANDS2=16,11,16,11;SUPTYPE=AL;SVLEN=109;SVMETHOD=Snifflesv1.0.3;SVTYPE=INS\tGT:DR:DV\t./1:.:.";
        private const string QueryIns6 =
            "chr19\t8817571\tMantaINS:180650:0:0:2:0:0\tT\tTATATAATATATTTTATATTATATAATATATAATATATATAATATATTATATAATATATAATATATATAATATATTATATAATATATTATATAATATATTATATAATATATTTTTATATTATATAATATATAATATAA\t203\tPASS\tSVTYPE=INS;SVLEN=137;END=8817571;CIGAR=1M137I\tGT:FT:GQ:PL:PR:SR\t1/1:PASS:15:256,18,0:0,0:0,8";

        private const double PercentDistance = 0.05;

        [Theory]
        [InlineData(TrueIns, QueryIns, nameof(WittyerType.Insertion), true)]
        [InlineData(QueryIns, TrueIns, nameof(WittyerType.Insertion), true)]
        [InlineData(QueryIns2, TrueIns2, nameof(WittyerType.Insertion), true)]
        [InlineData(TrueIns2, QueryIns2, nameof(WittyerType.Insertion), true)]
        [InlineData(TrueIns3, QueryIns3, nameof(WittyerType.Insertion), true)]
        [InlineData(TrueIns4, QueryIns4, nameof(WittyerType.Insertion), true)]
        [InlineData(TrueIns5, QueryIns5, nameof(WittyerType.Insertion), true)]  // seq mismatch still TP we don't care about sequence for headline stats
        [InlineData(TrueIns6, QueryIns6, nameof(WittyerType.Insertion), true)]  // size mismatch still TP we don't care about sequence for headline stats
        public void OverlapWorks_InsBnd(string truthVar, string queryVar, string type, bool isTp)
        {
            const string sampleName = "blah";
            var vcfVariantParserSettings = VcfVariantParserSettings.Create(new List<string> { sampleName });
            var baseVariant = VcfVariant.TryParse(truthVar, vcfVariantParserSettings).GetOrThrow();
            const bool isCrossTypeOn = true;
            var wittyerType = WittyerType.Parse(type);
            var inputSpecs = InputSpec
                .GenerateCustomInputSpecs(isCrossTypeOn, null, percentThreshold: PercentDistance)
                .ToDictionary(s => s.VariantType, s => s);
            if (wittyerType == WittyerType.Insertion)
            {
                var inputSpec = inputSpecs[wittyerType];
                inputSpecs[wittyerType] = InputSpec.Create(wittyerType, inputSpec.BinSizes,
                    100, inputSpec.PercentThreshold,
                    inputSpec.ExcludedFilters, inputSpec.IncludedFilters, null);
            }

            var bndSet = new Dictionary<IGeneralBnd, IVcfVariant>();
            var errorList = new List<string>();
            var truthVs = WittyerVcfReader.CreateVariants(baseVariant, true, sampleName,
                inputSpecs, bndSet, errorList).OfType<IMutableWittyerBnd>().Select(v => v).ToList();
            baseVariant = VcfVariant.TryParse(queryVar, vcfVariantParserSettings).GetOrThrow();
            var queryVs = WittyerVcfReader.CreateVariants(baseVariant, false, sampleName,
                inputSpecs, bndSet, errorList).OfType<IMutableWittyerBnd>();
            var tree = TruthForest.Create(sampleName, VcfHeader.CreateBuilder(VcfVersion.FourPointOne).Build());
            foreach (var truthV in truthVs)
                tree.AddTarget(truthV);
            foreach (var (queryV, truthV) in queryVs.Zip(truthVs))
            {
                OverlappingUtils.DoOverlapping(tree.BpInsTrees, queryV, OverlappingUtils.MatchBnd,
                    isCrossTypeOn, true, similarityThreshold: 0.85);
                queryV.Finalize(WitDecision.FalsePositive, EvaluationMode.CrossTypeAndSimpleCounting, null, DefaultMaxMatches);
                truthV.Finalize(WitDecision.FalseNegative, EvaluationMode.CrossTypeAndSimpleCounting, null, DefaultMaxMatches);
                if ((isTp ? WitDecision.TruePositive : WitDecision.FalsePositive) != queryV.Sample.Wit)
                    Assert.Equal(MatchSet.AlleleMatch.Select(it => it.ToStringDescription()).StringJoin("|"), queryV.Sample.What.StringJoin(","));
                if ((isTp ? WitDecision.TruePositive : WitDecision.FalseNegative) != truthV.Sample.Wit)
                    Assert.Equal(MatchSet.AlleleMatch.Select(it => it.ToStringDescription()).StringJoin("|"), truthV.Sample.What.StringJoin(","));
            }
        }
    }
}