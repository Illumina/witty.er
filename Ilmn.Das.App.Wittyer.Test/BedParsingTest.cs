using System;
using System.IO;
using System.Linq;
using Ilmn.Das.App.Wittyer.Input;
using Ilmn.Das.Std.AppUtils.Misc;
using Ilmn.Das.Std.BioinformaticUtils.Bed;
using Xunit;

namespace Ilmn.Das.App.Wittyer.Test
{
    public class BedParsingTest
    {
        private static string BedsFolder => Path.Combine(Environment.CurrentDirectory, "Resources", "Beds");

        [Theory]
        [InlineData("empty.bed")]
        [InlineData("refseq_union_cds_first-ten-lines.bed")]
        [InlineData("ucsc_example_bed.bed")]
        public void ParsingDoesntThrowException(string bedFileName)
        {
            var bedFile = Path.Combine(BedsFolder, bedFileName).ToFileInfo();
            Assert.Equal(bedFile.FullName, IncludeBedFile.CreateFromBedFile(bedFile).BedFile.FullName);
            var bedReader = BedReader.Create(bedFile);
            var source = bedReader.Select(b => (b.Contig, b.Start, b.Stop)).ToList();
            var actual = BedReader.Create(IncludeBedFile
                    .CreateFromContigIntervals(bedReader, Path.GetTempFileName().ToFileInfo()).BedFile)
                .Select(b => (b.Contig, b.Start, b.Stop)).ToList();
            // if source and count is equal, then the sequence should equal.
            Assert.Equal(source.Count == actual.Count, source.SequenceEqual(actual));
        }

        //[Theory]

        //public void NoOverlappingIntervalsInTree(string bedFileWithOverlaps)
        //{
        //    var bedFile = Path.Combine(BedsFolder, bedFileWithOverlaps).ToFileInfo();
        //    var mergedGenomeIntervalTree = WittyerUtils.GenerateMergedGenomeIntervalTreeFromBedFile(bedFile);
        //    foreach (var intervalTree in mergedGenomeIntervalTree)
        //    {
        //        foreach (var interval in intervalTree.Value)
        //        {
        //            Assert.False(mergedGenomeIntervalTree.Search(interval));
        //        }
        //        Assert.False(mergedGenomeIntervalTree.Search(interval.Value.get).Any());
        //    }
        //}
    }
}
