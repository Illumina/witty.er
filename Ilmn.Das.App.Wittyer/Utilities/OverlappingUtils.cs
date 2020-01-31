using System.Collections.Generic;
using Ilmn.Das.App.Wittyer.Utilities.Enums;
using Ilmn.Das.App.Wittyer.Vcf.Samples;
using Ilmn.Das.App.Wittyer.Vcf.Variants;
using Ilmn.Das.App.Wittyer.Vcf.Variants.Annotations;
using Ilmn.Das.Core.Tries.Extensions;
using Ilmn.Das.Std.AppUtils.Intervals;
using Ilmn.Das.Std.BioinformaticUtils.GenomicFeatures;
using JetBrains.Annotations;

namespace Ilmn.Das.App.Wittyer.Utilities
{
    internal static class OverlappingUtils
    {
        private static readonly ISet<uint> WhoTags = new HashSet<uint>();

        internal static void ClearWhoTags() => WhoTags.Clear();

        internal static void DoOverlapping<T>(
            [NotNull] IReadOnlyDictionary<WittyerType, GenomeIntervalTree<T>> truthTrees,
            [NotNull] T queryVariant, IsAlleleMatch<T> alleleMatchFunc, bool isCrossType, bool isSimpleCounting)
            where T : class, IMutableWittyerSimpleVariant
        {
            if (!truthTrees.TryGetValue(queryVariant.VariantType, out var tree))
                return; // no match at all, nothing to update

            var failedReasons = new HashSet<FailedReason>();
            foreach (var overlap in tree.Search(queryVariant))
            {
                failedReasons.Clear();
                var (matchEnum, failedReason) = GenerateWhatAndWhy(queryVariant, failedReasons, overlap, alleleMatchFunc, isCrossType);

                var wow = overlap.VariantType.HasOverlappingWindows && 
                          (isSimpleCounting 
                           || matchEnum == MatchEnum.AlleleAndGenotypeMatch || matchEnum == MatchEnum.LocalAndGenotypeMatch)
                    ? overlap.TryGetOverlap(queryVariant).GetOrDefault()
                    : null;
                var borderDistance = BorderDistance.CreateFromVariant(overlap, queryVariant);
                var who = GetNextWhoTag();

                var overlapInfo =
                    OverlapAnnotation.Create(who, matchEnum, wow, borderDistance, failedReason);

                overlap.AddToOverlapInfo(overlapInfo);
                queryVariant.AddToOverlapInfo(overlapInfo);

                uint GetNextWhoTag()
                {
                    var ret = overlap.OriginalVariant.Position;
                    while (WhoTags.Contains(ret))
                        ret++;
                    WhoTags.Add(ret);
                    return ret;
                }
            }
        }

        internal static (MatchEnum what, FailedReason reason) GenerateWhatAndWhy<T>([NotNull] T query,
            [NotNull] ICollection<FailedReason> failedReasons, [NotNull] T overlap,
            [NotNull] IsAlleleMatch<T> alleleMatchFunc, bool isCrossType) where T : class, IWittyerSimpleVariant
        {
            var isAlleleMatch = alleleMatchFunc(query, failedReasons, overlap);

            //GT situation
            var isGtMatch = false;
            if (query.Sample is IWittyerGenotypedSample originalGt &&
                overlap.Sample is IWittyerGenotypedSample otherGt)
            {
                if (!originalGt.Gt.Equals(otherGt.Gt))
                    failedReasons.Add(FailedReason.GtMismatch);
                else
                    isGtMatch = true;
            }

            //CNV situation
            if (isCrossType || !(query.Sample is IWittyerCopyNumberSample cnv &&
                  overlap.Sample is IWittyerCopyNumberSample otherCnv)
                || Equals(cnv.Cn, otherCnv.Cn))
                return (GetCorrectMatchedType(isAlleleMatch), GetCorrectFailedReason());

            failedReasons.Add(FailedReason.CnMismatch);

            return (GetCorrectMatchedType(false), GetCorrectFailedReason());

            MatchEnum GetCorrectMatchedType(bool alleleMatch)
                => alleleMatch
                    ? isGtMatch ? MatchEnum.AlleleAndGenotypeMatch : MatchEnum.AlleleMatch
                    : isGtMatch ? MatchEnum.LocalAndGenotypeMatch : MatchEnum.LocalMatch;

            FailedReason GetCorrectFailedReason()
            {
                if (failedReasons.Count == 0)
                    return FailedReason.Unset;
                if (failedReasons.Contains(FailedReason.BndPartialMatch))
                    return FailedReason.BndPartialMatch;
                if (failedReasons.Contains(FailedReason.BordersTooFarOff))
                    return FailedReason.BordersTooFarOff;
                if (failedReasons.Contains(FailedReason.CnMismatch))
                    return FailedReason.CnMismatch;
                if (failedReasons.Contains(FailedReason.GtMismatch))
                    return FailedReason.GtMismatch;
                return FailedReason.Unset;
            }
        }

        internal delegate bool IsAlleleMatch<in T>(T query, ICollection<FailedReason> failedReason, T truth);

        internal static bool IsBndAlleleMatch<T>([NotNull] T wittyerBnd,
            [NotNull] ICollection<FailedReason> failedReason,
            [NotNull] T otherBnd) where T :IWittyerBnd
        {
            // shouldn't need to check the pos end since that was used to search tree
            if (Equals(wittyerBnd.EndOriginalVariant.Contig, otherBnd.EndOriginalVariant.Contig) 
                && wittyerBnd.EndInterval.TryGetOverlap(otherBnd.EndInterval).Any())
                return true;

            failedReason.Add(FailedReason.BndPartialMatch);
            return false;
        }

        internal static bool IsVariantAlleleMatch<T>([NotNull] T wittyerVariant,
            [NotNull] ICollection<FailedReason> failedReason,
            [NotNull] T otherVariant) where T : IWittyerVariant
        {
            if (wittyerVariant.PosInterval.TryGetOverlap(otherVariant.PosInterval).Any() &&
                wittyerVariant.EndInterval.TryGetOverlap(otherVariant.EndInterval).Any())
                return true;

            failedReason.Add(FailedReason.BordersTooFarOff);
            return false;
        }
    }
}