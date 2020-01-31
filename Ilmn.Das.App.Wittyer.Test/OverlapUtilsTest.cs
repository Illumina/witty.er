using System.Collections.Generic;
using System.Linq;
using Ilmn.Das.App.Wittyer.Input;
using Ilmn.Das.App.Wittyer.Results;
using Ilmn.Das.App.Wittyer.Utilities;
using Ilmn.Das.App.Wittyer.Utilities.Enums;
using Ilmn.Das.App.Wittyer.Vcf;
using Ilmn.Das.App.Wittyer.Vcf.Samples;
using Ilmn.Das.App.Wittyer.Vcf.Variants;
using Ilmn.Das.App.Wittyer.Vcf.Variants.Breakend;
using Ilmn.Das.App.Wittyer.Vcf.Variants.Genotype;
using Ilmn.Das.Core.Tries.Extensions;
using Ilmn.Das.Std.BioinformaticUtils.Contigs;
using Ilmn.Das.Std.BioinformaticUtils.GenomicFeatures;
using Ilmn.Das.Std.VariantUtils.Vcf.Headers;
using Ilmn.Das.Std.VariantUtils.Vcf.Parsers;
using Ilmn.Das.Std.VariantUtils.Vcf.Variants;
using JetBrains.Annotations;
using Moq;
using Xunit;

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
        [InlineData(true, 900, 1100, 5200, 5700, true, MatchEnum.AlleleAndGenotypeMatch, FailedReason.Unset)]
        [InlineData(false, 800, 900, 5500, 5555, true, MatchEnum.LocalAndGenotypeMatch, FailedReason.BordersTooFarOff)]
        [InlineData(false, 100, 110, 5110, 5200, false, MatchEnum.LocalMatch, FailedReason.BordersTooFarOff)]
        [InlineData(true, 1000, 1200, 5100, 5700, false, MatchEnum.AlleleMatch, FailedReason.GtMismatch)]
        public void GenerateWhatAndWhyWorksForWittyerVariant(bool isAlleleMatch, uint posStart, uint posEnd, uint endStart, uint endEnd, 
            bool isGtMatch, MatchEnum matchResult, FailedReason reasonResult)
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
            var actual = OverlappingUtils.GenerateWhatAndWhy(otherVariant.Object, failedReasons,
                originalVariant, OverlappingUtils.IsVariantAlleleMatch, false);
            Assert.Equal((matchResult, reasonResult), actual);
        }

        [Theory]
        [InlineData(true, 900, 1100, 5200, 5700, true, 3, MatchEnum.AlleleAndGenotypeMatch, FailedReason.Unset)]
        [InlineData(true, 900, 1100, 5200, 5700, true, 2, MatchEnum.LocalAndGenotypeMatch, FailedReason.CnMismatch)]
        [InlineData(false, 800, 900, 5500, 5555, true, 3, MatchEnum.LocalAndGenotypeMatch, FailedReason.BordersTooFarOff)]
        [InlineData(false, 100, 110, 5110, 5200, false, 1, MatchEnum.LocalMatch, FailedReason.BordersTooFarOff)]
        [InlineData(true, 1000, 1200, 5100, 5700, false, 3, MatchEnum.AlleleMatch, FailedReason.GtMismatch)]
        [InlineData(true, 1000, 1200, 5100, 5700, false, 1, MatchEnum.LocalMatch, FailedReason.CnMismatch)]
        public void GenerateWhatAndWhyWorksWithCnSample(bool isAlleleMatch, uint posStart, uint posEnd, uint endStart, uint endEnd,
            bool isGtMatch, uint cn, MatchEnum matchResult, FailedReason reasonResult)
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

            var actual = OverlappingUtils.GenerateWhatAndWhy(otherVariant.Object, new List<FailedReason>(),
                originalVariant, OverlappingUtils.IsVariantAlleleMatch, false);
            Assert.Equal((matchResult, reasonResult), actual);
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

        [NotNull]
        private static Mock<IWittyerVariant> SetupBasicVariant([NotNull] IContigAndInterval contigAndInterval)
        {
            var variant = new Mock<IWittyerVariant>();
            variant.SetupGet(v => v.Contig).Returns(contigAndInterval.Contig);
            variant.SetupGet(v => v.Start).Returns(contigAndInterval.Start);
            variant.SetupGet(v => v.Stop).Returns(contigAndInterval.Stop);
            variant.SetupGet(v => v.IsStartInclusive).Returns(contigAndInterval.IsStartInclusive);
            variant.SetupGet(v => v.IsStopInclusive).Returns(contigAndInterval.IsStopInclusive);
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

        private const string TrueInv =
            "chr13\t82356873\t15960_1\tN\t<INV>\t.\tPASS\tAF=0.6875;CHR2=13;END=82356960;Kurtosis_quant_start=-0.283624;Kurtosis_quant_stop=-0.293709;PRECISE;RE=11;STD_quant_start=3.847077;STD_quant_stop=9.038805;STRANDS=++;STRANDS2=6,5,5,6;SUPTYPE=SR;SVLEN=87;SVMETHOD=Snifflesv1.0.3;SVTYPE=INV\tGT:DR:DV\t./1:.:.";
        private const string QueryInv =
            "chr13\t82356868\tMantaINV:147163:0:0:0:1:0\tT\t<INV>\t999\tPASS\tCIEND=-4,0;SVTYPE=INV;EVENT=MantaINV:147163:0:0:0:1:0;SVLEN=95;CIPOS=0,4;JUNCTION_QUAL=999;END=82356963;HOMSEQ=TATA;HOMLEN=4;INV3\tGT:FT:GQ:PL:PR:SR\t0/1:PASS:332:999,0,329:14,5:0,34";

        [Theory]
        [InlineData(TrueDup, QueryDup, nameof(WittyerType.Duplication), true)]
        [InlineData(TrueDup2, QueryDup2, nameof(WittyerType.Duplication), false)]
        [InlineData(TrueDel, QueryDel, nameof(WittyerType.Deletion), true)]
        [InlineData(TrueDel2, QueryDel2, nameof(WittyerType.Deletion), true)]
        [InlineData(TrueDel3, QueryDel3, nameof(WittyerType.Deletion), true)]
        [InlineData(TrueDel4, QueryDel4, nameof(WittyerType.Deletion), true)]
        [InlineData(TrueInv, QueryInv, nameof(WittyerType.Inversion), true)]
        public void OverlapWorks_DupDel([NotNull] string truthVar, [NotNull] string queryVar, string type, bool isTp)
        {
            const string sampleName = "blah";
            var vcfVariantParserSettings = VcfVariantParserSettings.Create(new List<string> { sampleName });
            var baseVariant = VcfVariant.TryParse(truthVar, vcfVariantParserSettings).GetOrThrow();
            const bool isCrossTypeOn = true;
            var wittyerType = WittyerType.Parse(type);
            var inputSpecs = InputSpec.GenerateCustomInputSpecs(!isCrossTypeOn, new[] { wittyerType }, percentDistance: PercentDistance).ToDictionary(s => s.VariantType, s => s);
            
            var bndSet = new Dictionary<IGeneralBnd, IVcfVariant>();
            var errorList = new List<string>();
            var truthV = (IMutableWittyerSimpleVariant) WittyerVcfReader.CreateVariant(baseVariant, baseVariant.Samples.First().Value, true, sampleName,
                    inputSpecs, bndSet, errorList, isCrossTypeOn);
            baseVariant = VcfVariant.TryParse(queryVar, vcfVariantParserSettings).GetOrThrow();
            var queryV = (IMutableWittyerVariant) WittyerVcfReader.CreateVariant(baseVariant, baseVariant.Samples.First().Value, false, sampleName,
                inputSpecs, bndSet, errorList, isCrossTypeOn);
            var tree = TruthForest.Create(sampleName, VcfHeader.CreateBuilder(VcfVersion.FourPointOne).Build());
            tree.AddTarget(truthV);
            OverlappingUtils.DoOverlapping(tree.VariantTrees, queryV, OverlappingUtils.IsVariantAlleleMatch, isCrossTypeOn, true);
            queryV.Finalize(WitDecision.FalsePositive, EvaluationMode.CrossTypeAndSimpleCounting, null);
            truthV.Finalize(WitDecision.FalseNegative, EvaluationMode.CrossTypeAndSimpleCounting, null);
            Assert.Equal(isTp ? WitDecision.TruePositive : WitDecision.FalsePositive, queryV.Sample.Wit);
            Assert.Equal(isTp ? WitDecision.TruePositive : WitDecision.FalseNegative, truthV.Sample.Wit);
        }

        private const string TrueIns =
            "chr19\t8817471\t19838_1\tN\t<INS>\t.\tPASS\tAF=0.818182;CHR2=19;END=8817518;IMPRECISE;Kurtosis_quant_start=3.424836;Kurtosis_quant_stop=-0.210366;RE=27;STD_quant_start=15.711338;STD_quant_stop=41.131122;STRANDS=+-;STRANDS2=16,11,16,11;SUPTYPE=AL;SVLEN=99;SVMETHOD=Snifflesv1.0.3;SVTYPE=INS\tGT:DR:DV\t./1:.:.";
        private const string QueryIns =
            "chr19\t8817571\tMantaINS:180650:0:0:2:0:0\tT\tTATATAATATATTTTATATTATATAATATATAATATATATAATATATTATATAATATATAATATATATAATATATTATATAATATATTATATAATATATTATATAATATATTTTTATATTATATAATATATAATATAA\t203\tPASS\tSVTYPE=INS;SVLEN=137;END=8817571;CIGAR=1M137I\tGT:FT:GQ:PL:PR:SR\t1/1:PASS:15:256,18,0:0,0:0,8";

        private const string TrueIns2 =
            "chr17\t39886379\tMantaINS:170945:0:0:0:0:1\tA\tAAGAAAGAAAGAAAGAAAGAAAGAAAGAAAGAAGGAAAGAAAGAAAGAAAGAAGGAAAG\t507\tPASS\tSVTYPE=INS;SVLEN=58;CIPOS=0,1;END=39886379;HOMSEQ=A;HOMLEN=1;CIGAR=1M58I\tGT:FT:GQ:PL:PR:SR\t1/1:PASS:35:560,38,0:0,0:0,15";
        private const string QueryIns2 =
            "chr17\t39886275\t18619\tN\t<INS>\t.\tPASS\tAF=0.909091;CHR2=17;END=39886303;Kurtosis_quant_start=0.004589;Kurtosis_quant_stop=1.257711;PRECISE;RE=30;STD_quant_start=7.366591;STD_quant_stop=6.562520;STRANDS=+-;STRANDS2=16,14,16,14;SUPTYPE=AL;SVLEN=88;SVMETHOD=Snifflesv1.0.3;SVTYPE=INS\tGT:DR:DV\t./1:.:.";

        private const string TrueIns3 =
            "chr6\t152171109\t8501\tN\t<INS>\t.\tPASS\tAF=0.777778;CHR2=6;END=152171158;IMPRECISE;Kurtosis_quant_start=-0.645802;Kurtosis_quant_stop=31.034996;RE=42;STD_quant_start=11.623048;STD_quant_stop=30.093505;STRANDS=+-;STRANDS2=27,15,27,15;SUPTYPE=AL;SVLEN=55;SVMETHOD=Snifflesv1.0.3;SVTYPE=INS\tGT:DR:DV\t./1:.:.";
        private const string QueryIns3 =
            "chr6\t152171249\tMantaINS:82429:0:0:3:1:0\tT\tTTATATATATATTATTTTATATGCATATAAAATAATATATATATAATTTTATATGCATATAAAATAATATATATATATTATTTTATATGCATATAAAATAATATA\t763\tPASS\tSVTYPE=INS;SVLEN=104;CIPOS=0,11;END=152171249;HOMSEQ=TATATATATAT;HOMLEN=11;CIGAR=1M104I\tGT:FT:GQ:PL:PR:SR\t1/1:PASS:50:816,53,0:0,2:0,19";

        private const double PercentDistance = 0.05;

        [Theory]
        [InlineData(TrueIns, QueryIns, nameof(WittyerType.Insertion))]
        [InlineData(TrueIns2, QueryIns2, nameof(WittyerType.Insertion))]
        [InlineData(TrueIns3, QueryIns3, nameof(WittyerType.Insertion))]
        public void OverlapWorks_InsBnd([NotNull] string truthVar, [NotNull] string queryVar, string type)
        {
            const string sampleName = "blah";
            var vcfVariantParserSettings = VcfVariantParserSettings.Create(new List<string> { sampleName });
            var baseVariant = VcfVariant.TryParse(truthVar, vcfVariantParserSettings).GetOrThrow();
            const bool isCrossTypeOn = true;
            var wittyerType = WittyerType.Parse(type);
            var inputSpecs = InputSpec
                .GenerateCustomInputSpecs(!isCrossTypeOn, new[] {wittyerType}, percentDistance: PercentDistance)
                .ToDictionary(s => s.VariantType, s => s);
            if (wittyerType == WittyerType.Insertion)
            {
                var inputSpec = inputSpecs[wittyerType];
                inputSpecs[wittyerType] = InputSpec.Create(wittyerType, inputSpec.BinSizes,
                    100, inputSpec.PercentDistance,
                    inputSpec.ExcludedFilters, inputSpec.IncludedFilters, null);
            }

            var bndSet = new Dictionary<IGeneralBnd, IVcfVariant>();
            var errorList = new List<string>();
            var truthV = (IMutableWittyerBnd) WittyerVcfReader.CreateVariant(baseVariant, baseVariant.Samples.First().Value, true, sampleName,
                inputSpecs, bndSet, errorList, isCrossTypeOn);
            baseVariant = VcfVariant.TryParse(queryVar, vcfVariantParserSettings).GetOrThrow();
            var queryV = (IMutableWittyerBnd) WittyerVcfReader.CreateVariant(baseVariant, baseVariant.Samples.First().Value, false, sampleName,
                inputSpecs, bndSet, errorList, isCrossTypeOn);
            var tree = TruthForest.Create(sampleName, VcfHeader.CreateBuilder(VcfVersion.FourPointOne).Build());
            tree.AddTarget(truthV);
            OverlappingUtils.DoOverlapping(tree.BpInsTrees, queryV, OverlappingUtils.IsBndAlleleMatch, isCrossTypeOn, true);
            queryV.Finalize(WitDecision.FalsePositive, EvaluationMode.CrossTypeAndSimpleCounting, null);
            truthV.Finalize(WitDecision.FalseNegative, EvaluationMode.CrossTypeAndSimpleCounting, null);
            Assert.Equal(WitDecision.TruePositive, queryV.Sample.Wit);
            Assert.Equal(WitDecision.TruePositive, truthV.Sample.Wit);
        }
    }
}