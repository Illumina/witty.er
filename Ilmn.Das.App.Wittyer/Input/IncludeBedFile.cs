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
        [NotNull] public readonly GenomeIntervalTree<IContigAndInterval> IntervalTree;

        /// <summary>
        /// The bed file associated with this instance.  If created from <see cref="CreateFromContigIntervals"/>, this will write out a bed file.
        /// </summary>
        [NotNull] public FileInfo BedFile => _fileSource.Value;
        private readonly Lazy<FileInfo> _fileSource;

        private IncludeBedFile([NotNull] GenomeIntervalTree<IContigAndInterval> tree, 
            [NotNull] Lazy<FileInfo> fileSource)
        {
            IntervalTree = tree;
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
        [NotNull]
        [Pure]
        public static IncludeBedFile CreateFromContigIntervals(
            [NotNull] IEnumerable<IContigAndInterval> contigIntervals, [NotNull] FileInfo pathToWriteBedFile)
            // ReSharper disable PossibleMultipleEnumeration // TypeCache doesn't enumerate.
            => TypeCache<IEnumerable<IContigAndInterval>, IncludeBedFile>.GetOrAdd(contigIntervals, () =>
            {
                var tree = contigIntervals as GenomeIntervalTree<IContigAndInterval> ??
                           CreateGenomeIntervalTree(contigIntervals);
                return new IncludeBedFile(tree, CreateBedFileLazy(tree));

                Lazy<FileInfo> CreateBedFileLazy(
                    IEnumerable<IContigAndInterval> thisTree)
                    => new Lazy<FileInfo>(() =>
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

        [NotNull]
        private static GenomeIntervalTree<IContigAndInterval> CreateGenomeIntervalTree(
            [NotNull] IEnumerable<IContigAndInterval> contigIntervals)
        {
            var dictionary = new Dictionary<IContigInfo, MergedIntervalTree<uint>>();
            var listOrder = new List<IContigInfo>();
            foreach (var contigInterval in contigIntervals)
            {
                if (!dictionary.TryGetValue(contigInterval.Contig, out var tree))
                {
                    tree = MergedIntervalTree<uint>.Create(null);
                    listOrder.Add(contigInterval.Contig);
                    dictionary.Add(contigInterval.Contig, tree);
                }
                tree.Add(contigInterval);
            }

            var ret = GenomeIntervalTree<IContigAndInterval>.Create();
            foreach (var contig in listOrder)
                ret.AddRange(dictionary[contig]
                    .Select(i => i as IContigAndInterval ?? ContigAndInterval.Create(contig, i.Start, i.Stop)));

            return ret;
        }

        /// <summary>
        /// Creates a new instance of <see cref="IncludeBedFile"/> from a <see cref="FileInfo"/>.
        /// Assumes it's a valid bed file, otherwise, might crash.
        /// </summary>
        /// <param name="bedFile">The source bed file</param>
        [NotNull]
        [Pure]
        public static IncludeBedFile CreateFromBedFile([NotNull] FileInfo bedFile)
            => CreateFromBedReader(BedReader.Create(bedFile));

        /// <summary>
        /// Creates a new instance of <see cref="IncludeBedFile"/> from a <see cref="BedReader"/>.
        /// </summary>
        /// <param name="bedReader">The source bed reader</param>
        [NotNull]
        [Pure]
        public static IncludeBedFile CreateFromBedReader([NotNull] BedReader bedReader)
            => TypeCache<string, IncludeBedFile>.GetOrAdd(bedReader.FileSource.GetCompleteRealPath().FullName, () =>
                new IncludeBedFile(CreateGenomeIntervalTree(bedReader),
                    new Lazy<FileInfo>(() => bedReader.FileSource)));

        /// <inheritdoc/>
        [NotNull]
        public override string ToString() => BedFile.FullName;
    }
}