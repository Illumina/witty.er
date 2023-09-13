using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Ilmn.Das.App.Wittyer.Utilities;
using Ilmn.Das.Core.BgZip;
using Ilmn.Das.Core.Tries.Extensions;
using Ilmn.Das.Std.AppUtils.Collections;
using Ilmn.Das.Std.AppUtils.Intervals;
using Ilmn.Das.Std.AppUtils.Misc;
using Ilmn.Das.Std.BioinformaticUtils.Bed;
using Ilmn.Das.Std.BioinformaticUtils.Contigs;
using Ilmn.Das.Std.BioinformaticUtils.GenomicFeatures;
using Ilmn.Das.Std.BioinformaticUtils.Tabix;
using JetBrains.Annotations;

namespace Ilmn.Das.App.Wittyer.Input
{
    /// <summary>
    /// A thin wrapper that presents both a bed file and an <see cref="GenomeIntervalTree{IContigAndInterva}"/> for use.
    /// </summary>
    public class IncludeBedFile
    {
        /// <summary>
        /// The IntervalTree from this bed file.
        /// </summary>
        public GenomeIntervalTree<IContigAndInterval> IntervalTree => _intervalTree.Value;
        private readonly Lazy<GenomeIntervalTree<IContigAndInterval>> _intervalTree;

        /// <summary>
        /// The bed file associated with this instance.  If created from <see cref="CreateFromContigIntervals"/>, this will write out a bed file.
        /// </summary>
        public FileInfo BedFile => _fileSource.Value;
        private readonly Lazy<FileInfo> _fileSource;

        private IncludeBedFile(Lazy<GenomeIntervalTree<IContigAndInterval>> tree, 
            Lazy<FileInfo> fileSource)
        {
            _intervalTree = tree;
            _fileSource = fileSource;
        }

        /// <summary>
        /// Creates a new instance of <see cref="IncludeBedFile"/> from an <see cref="IEnumerable{IContigAndInterval}"/>.
        /// If you are using a bed file, please use <see cref="CreateFromBedFile"/>.
        /// </summary>
        /// <param name="contigIntervals">The source intervals</param>
        /// <param name="pathToWriteBedFile">The path that you want to output the bedfile if you need it
        ///      (won't be written until you access <see cref="BedFile"/>)
        ///      <c>WARNING:</c> This will overwrite the file!</param>
        [Pure]
        public static IncludeBedFile CreateFromContigIntervals(
            IEnumerable<IContigAndInterval> contigIntervals, FileInfo pathToWriteBedFile)
            // ReSharper disable PossibleMultipleEnumeration // TypeCache doesn't enumerate.
            => TypeCache<IEnumerable<IContigAndInterval>, IncludeBedFile>.GetOrAdd(contigIntervals, () =>
            {
                var tree = contigIntervals as GenomeIntervalTree<IContigAndInterval> ??
                           CreateGenomeIntervalTree(contigIntervals);
                return new IncludeBedFile(new Lazy<GenomeIntervalTree<IContigAndInterval>>(tree),
                    CreateBedFileLazy(tree));

                Lazy<FileInfo> CreateBedFileLazy(
                    IEnumerable<IContigAndInterval> thisTree)
                    => new(() =>
                    {
                        if (pathToWriteBedFile.Directory?.ExistsNow() == false)
                            pathToWriteBedFile.Directory.Create();
                        var contents = thisTree.Select(i => i.Contig.Name
                            .FollowedBy(i.Start.ToString(), i.Stop.ToString())
                            .StringJoin(BedItem.ColumnDelimiter));

                        if (pathToWriteBedFile.ExistsNow())
                            pathToWriteBedFile.Delete();

                        if (pathToWriteBedFile.Name.EndsWith(".gz"))
                        {
                            contents.WriteToCompressedFile(pathToWriteBedFile);
                            // ReSharper disable once ReturnValueOfPureMethodIsNotUsed // not actually pure anymore!
                            pathToWriteBedFile.TryEnsureTabixed(TabixFileType.Bed).GetOrDefault();
                        }
                        else
                            contents.WriteToFile(pathToWriteBedFile);

                        return pathToWriteBedFile;
                    });
            });
        // ReSharper restore PossibleMultipleEnumeration

        private static GenomeIntervalTree<IContigAndInterval> CreateGenomeIntervalTree(
            IEnumerable<IContigAndInterval> contigIntervals)
        {
            var dictionary = new Dictionary<IContigInfo, List<IInterval<uint>>>();
            var listOrder = new List<IContigInfo>();
            foreach (var contigInterval in contigIntervals)
            {
                var contig = contigInterval.Contig;
                if (!dictionary.TryGetValue(contig, out var tree))
                {
                    tree = new List<IInterval<uint>>();
                    listOrder.Add(contig);
                    dictionary.Add(contig, tree);
                }
                tree.Add(contigInterval);
            }

            var ret = GenomeIntervalTree<IContigAndInterval>.Create();
            foreach (var contig in listOrder)
            {
                ret.AddRange(dictionary[contig].ToMergedIntervalTree()
                    .Select(i => i as IContigAndInterval ?? ContigAndInterval.Create(contig, i.Start, i.Stop)));
                var other = contig.ToUcscStyle();
                if (other.Name == contig.Name)
                    other = contig.ToGrchStyle();
                if (other.Name != contig.Name)
                    ret.AddRange(dictionary[contig]
                        .Select(i => ContigAndInterval.Create(other, i.Start, i.Stop)));
            }

            return ret;
        }

        /// <summary>
        /// Creates a new instance of <see cref="IncludeBedFile"/> from a <see cref="FileInfo"/>.
        /// Assumes it's a valid bed file, otherwise, might crash.
        /// </summary>
        /// <param name="bedFile">The source bed file</param>
        [Pure]
        public static IncludeBedFile CreateFromBedFile(FileInfo bedFile)
        {
            var unzippedFileInfo = bedFile.GetUnzippedFileInfo();
            return bedFile.ExistsNow()
                ? CreateFromBedReader(BedReader.Create(unzippedFileInfo), bedFile)
                : TypeCache<string, IncludeBedFile>.GetOrAdd(bedFile.FullName,
                    () => CreateFromBedReader(BedReader.Create(unzippedFileInfo), bedFile));
        }

        /// <summary>
        /// Creates a new instance of <see cref="IncludeBedFile"/> from a <see cref="BedReader"/>.
        /// </summary>
        /// <param name="bedReader">The source bed reader</param>
        [Pure]
        public static IncludeBedFile CreateFromBedReader(BedReader bedReader, FileInfo originalFile)
            => TypeCache<string, IncludeBedFile>.GetOrAdd(originalFile.FullName, () =>
                new IncludeBedFile(new Lazy<GenomeIntervalTree<IContigAndInterval>>(
                        () => CreateGenomeIntervalTree(bedReader)),
                    new Lazy<FileInfo>(() => bedReader.FileSource)));

        /// <inheritdoc/>
        public override string ToString() => BedFile.FullName;
    }
}
