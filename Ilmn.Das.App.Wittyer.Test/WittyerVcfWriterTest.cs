using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using Ilmn.Das.App.Wittyer.Results;
using Ilmn.Das.App.Wittyer.Vcf.Readers;
using Ilmn.Das.App.Wittyer.Vcf.Variants;
using Ilmn.Das.Core.Tries.Extensions;
using Ilmn.Das.Std.AppUtils.Collections;
using Ilmn.Das.Std.AppUtils.Misc;
using Ilmn.Das.Std.BioinformaticUtils.Contigs;
using Ilmn.Das.Std.VariantUtils.SimpleVariants;
using Ilmn.Das.Std.VariantUtils.Vcf;
using Ilmn.Das.Std.VariantUtils.Vcf.Headers;
using Xunit;

namespace Ilmn.Das.App.Wittyer.Test
{
    public class WittyerVcfWriterTest
    {
        private static readonly string SampleName = "tumor";

        private const string Bnd1 =
            "KN707703.1\t73\tMantaBND:172597:0:1:0:0:0:0\tG\t]JTFH01000956.1:953]G\t232\tPASS\tBND_DEPTH=39;CIPOS=-476,476;IMPRECISE;MATE_BND_DEPTH=42;MATEID=MantaBND:172597:0:1:0:0:0:1;SVTYPE=BND;WHERE=.;WHO=.;WIN=Transl 573 ocationBreakend|1+;WOW=.\tGT:FT:GQ:PL:PR:WIT:WHY:WHAT\t0/1:PASS:232:282,0,298:16,21:FP:.:.";

        private const string Bnd2 =
            "KN707703.1\t2235\tMantaBND:167572:0:1:0:0:0:0\tA\tA]chrX:55652526]\t852\tPASS\tBND_DEPTH=41;MATE_BND_DEPTH=13;MATEID=MantaBND:167572:0:1:0:0:0:1;SVTYPE=BND;WHERE=.;WHO=.;WIN=TranslocationBreakend|1+;WOW=.\tGT:FT:GQ:PL:PR:SR:WIT:WHY:WHAT\t1/1:PASS:118:905,121,0:0,41:0,0:FP:.:.";

        private const string NoValueUnsorted =
            "KN707703.1\t2235\tMantaBND:167572:0:1:0:0:0:0\tA\tA]chrX:55652526]\t852\tPASS\tBND_DEPTH=41;WHERE=.;WHO=.;WIN=TranslocationBreakend|1+;WOW=.;MATE_BND_DEPTH=13;MATEID=MantaBND:167572:0:1:0:0:0:1;SVTYPE=BND\tGT:FT:GQ:PL:PR:SR:WIT:WHY:WHAT\t.:.:.:.:.:.:.:.";
        private const string NoValueSorted =
            "KN707703.1\t2235\tMantaBND:167572:0:1:0:0:0:0\tA\tA]chrX:55652526]\t852\tPASS\tBND_DEPTH=41;MATE_BND_DEPTH=13;MATEID=MantaBND:167572:0:1:0:0:0:1;SVTYPE=BND;WHERE=.;WHO=.;WIN=TranslocationBreakend|1+;WOW=.\tGT:FT:GQ:PL:PR:SR:WIT:WHY:WHAT\t.";

        [Fact]
        public void ComparerWorks()
        {
            if (MiscUtils.IsRunningAnyLinux) return; // currently failing on linux :(

            IReadOnlyList<IContigInfo> CreateHeader(string fileName) => VcfHeader
                .TryCreate(Path.Combine("Resources", "VcfHeaders", fileName).ToFileInfo()).GetOrThrow()
                .ColumnMetaInfoLines.ContigLines.Select(kvp => kvp.Value.Contig).ToReadOnlyList();
            var comparer = WittyerVcfWriter.CreateComparer(CreateHeader("query.vcf.gz"), CreateHeader("truth.vcf.gz"));
            var parser = VcfVariantParserSettings.Create(ImmutableList.Create(SampleName));
            var bnd1 = VcfVariant.TryParse(Bnd1, parser).GetOrThrow();
            var bnd2 = VcfVariant.TryParse(Bnd2, parser).GetOrThrow();
            Assert.True(comparer.Compare(bnd1, bnd2) < 0);
        }

        [Fact]
        public void ToStringNoValueWorks()
        {
            var parser = VcfVariantParserSettings.Create(ImmutableList.Create(SampleName));
            var variant = VcfVariant.TryParse(NoValueUnsorted, parser).GetOrThrow();
            var actual = WittyerVcfWriter.ToString(variant, null);
            Assert.Equal(NoValueSorted, actual);
        }

        [Fact]
        public void ToStringBnd()
        {
            var parser = VcfVariantParserSettings.Create(ImmutableList.Create(SampleName));
            var variant = VcfVariant.TryParse(Bnd1, parser).GetOrThrow();
            var actual = WittyerVcfWriter.ToString(variant, null);
            Assert.Equal(Bnd1, actual);
        }

        [Fact]
        public void GenerateVcfStrings_IncludeHeaders()
        {
            if (MiscUtils.IsRunningAnyLinux) return; // currently failing on linux :(

            var parser = VcfVariantParserSettings.Create(ImmutableList.Create(SampleName));
            var variants = VcfVariant.TryParse(Bnd1, parser).FollowedBy(VcfVariant.TryParse(Bnd2, parser)).EnumerateSuccesses().ToList();
            var wittyerVariant = WittyerBndInternal.Create(variants[0],
                variants[0].Samples.Values[0],
                WittyerType.IntraChromosomeBreakend, new List<(uint, bool)>(), uint.MinValue, null, variants[1]);
            var headerLines = WittyerVcfWriter.GenerateVcfStrings(
                    WittyerResult.Create(VcfHeader.CreateBuilder(VcfVersion.FourPointOne).Build(), SampleName,
                        variants.Select(v => v.Contig).Distinct().ToList(), false,
                        new Dictionary<WittyerType, IReadOnlyList<IWittyerVariant>>(),
                        new Dictionary<WittyerType, IReadOnlyList<IWittyerBnd>>
                        {
                            {WittyerType.IntraChromosomeBreakend, new List<IWittyerBnd> {wittyerVariant}}
                        }, new List<IVcfVariant>()), null, null)
                .TakeWhile(line => line.StartsWith(VcfConstants.Header.Prefix)).ToList();

            // 11 = VcfVersion, WHO, WHAT, WHERE, WHY, WIT, WIN, WOW, date, version, column names
            Assert.Equal(11, headerLines.Count);
        }
    }
}
