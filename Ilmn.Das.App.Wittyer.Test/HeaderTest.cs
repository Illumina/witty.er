using System.Collections.Immutable;
using System.IO;
using System.Linq;
using Ilmn.Das.App.Wittyer.Input;
using Ilmn.Das.App.Wittyer.Results;
using Ilmn.Das.App.Wittyer.Utilities;
using Ilmn.Das.Core.Tries.Extensions;
using Ilmn.Das.Std.AppUtils.Misc;
using Ilmn.Das.Std.VariantUtils.Vcf;
using Ilmn.Das.Std.VariantUtils.Vcf.Headers;
using Ilmn.Das.Std.XunitUtils;
using Xunit;

namespace Ilmn.Das.App.Wittyer.Test
{
    public class HeaderTest
    {
        [Fact]
        public void MergeHeaderWorks()
        {
            string GetPrefix(string line)
                => line.Split(new[] {VcfConstants.Header.MetaInfoLines.KeyValueDelimiter[0]}, 4)
                    .Where(it => it.Length > 2).Skip(2).FirstOrDefault();

            var queryHeader = VcfHeader
                .TryCreate(Path.Combine("Resources", "VcfHeaders", "query.wit-141.vcf").ToFileInfo()).GetOrThrow();
            var truthHeader = VcfHeader
                .TryCreate(Path.Combine("Resources", "VcfHeaders", "truth.wit-141.vcf").ToFileInfo()).GetOrThrow();

            var vcfLines = truthHeader.MergedWith(queryHeader, SamplePair.Default, null).ToList();
            var merged = VcfHeader.TryCreate(vcfLines)
                .GetOrThrow();
            
            MultiAssert.True(
                merged.ColumnMetaInfoLines.SampleFormatLines.ContainsKey(WittyerConstants.WittyerMetaInfoLineKeys
                    .What));
            MultiAssert.True(
                merged.ColumnMetaInfoLines.SampleFormatLines.ContainsKey(WittyerConstants.WittyerMetaInfoLineKeys
                    .Why));
            MultiAssert.True(
                merged.ColumnMetaInfoLines.SampleFormatLines.ContainsKey(WittyerConstants.WittyerMetaInfoLineKeys
                    .Wit));
            MultiAssert.True(
                merged.ColumnMetaInfoLines.InfoLines.ContainsKey(WittyerConstants.WittyerMetaInfoLineKeys
                    .Who));
            MultiAssert.True(
                merged.ColumnMetaInfoLines.InfoLines.ContainsKey(WittyerConstants.WittyerMetaInfoLineKeys
                    .Where));
            MultiAssert.True(
                merged.ColumnMetaInfoLines.InfoLines.ContainsKey(WittyerConstants.WittyerMetaInfoLineKeys
                    .Win));
            MultiAssert.True(
                merged.ColumnMetaInfoLines.InfoLines.ContainsKey(WittyerConstants.WittyerMetaInfoLineKeys
                    .Wow));
            
            // 1 different because of date in 
            // ##bcftools_viewCommand=view -h /home/hking/manta_2by250_sv_dragen/manta_2by250_sv_dragen.sv.vcf.gz; Date=Fri Jul 19 07:39:26 2019
            var diffs = queryHeader.Select(GetPrefix).Where(it => it != null).ToImmutableHashSet()
                .Except(merged.Select(GetPrefix).Where(it => it != null));
            MultiAssert.True(diffs.Count == 1);
            
            // 1 different because of date in 
            // ##bcftools_viewCommand=view -h NA12878_pbmm_v1.0.0_pbsv_v2.2.0_hg38_20190430_witty_format.vcf.gz; Date=Fri Jul 19 07:38:56 2019
            diffs = truthHeader.Select(GetPrefix).Where(it => it != null).ToImmutableHashSet()
                .Except(merged.Select(GetPrefix).Where(it => it != null));
            MultiAssert.True(diffs.Count == 1);
            MultiAssert.AssertAll();
        }
    }
}