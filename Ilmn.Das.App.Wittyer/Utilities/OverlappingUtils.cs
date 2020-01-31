using System.Collections.Generic;
using System.IO;
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

        internal static void DoOverlapping(
            [NotNull] IReadOnlyDictionary<WittyerVariantType, GenomeIntervalTree<IWittyerSimpleVariant>> truthTrees,
            [NotNull] IWittyerSimpleVariant query)
        {
            if (!truthTrees.TryGetValue(query.VariantType, out var tree))
                return; // no match at all, nothing to update

            CompareVariants(tree.Search(query), query);
        }

        private static void CompareVariants([NotNull] IEnumerable<IWittyerSimpleVariant> overlaps, IWittyerSimpleVariant query)
        {
            foreach (var overlap in overlaps)
            {
                var wow = overlap.GenerateWow(query);
                var (matchEnum, failedReason) = overlap.GenerateWhatAndWhy(query);
                var borderDistance = BorderDistance.CreateFromVariant(overlap, query);
                var who = GetNextWhoTag(overlap.Start);

                var overlapInfo =
                    OverlapAnnotation.Create(who, matchEnum, wow, borderDistance, failedReason);

                overlap.AddToOverlapInfo(overlapInfo);
                query.AddToOverlapInfo(overlapInfo);
            }
        }

        private static uint GetNextWhoTag(uint truthPos)
        {
            var ret = truthPos;
            while (WhoTags.Contains(ret))
                ret++;
            WhoTags.Add(ret);
            return ret;
        }

        [CanBeNull]
        private static IInterval<uint> GenerateWow([NotNull] this IWittyerSimpleVariant original,
            IWittyerSimpleVariant otherSimpleVariant) 
            => WittyerConstants.NoOverlappingWindowTypes.Contains(original.VariantType) ? null : original.TryGetOverlap(otherSimpleVariant).GetOrDefault();


        internal static (MatchEnum what, FailedReason reason) GenerateWhatAndWhy([NotNull] this IWittyerSimpleVariant original,
            IWittyerSimpleVariant otherVariant)
        {
            var failedReason = new List<FailedReason>();
            
            var alleleMatch = false;
            //everything except BND
                if (original is IWittyerVariant normalVariant)
            {
                if(otherVariant is IWittyerVariant otherNormal)
                {
                    alleleMatch = normalVariant.PosInterval.Contains(otherNormal.Start) &&
                                  normalVariant.EndInterval.Contains(otherNormal.Stop) &&
                                  otherNormal.PosInterval.Contains(normalVariant.Start) &&
                                  otherNormal.EndInterval.Contains(normalVariant.Stop);

                    if(!alleleMatch)
                        failedReason.Add(FailedReason.FailedBoundary);
                }
                else
                {
                    throw new InvalidDataException("Not sure how we get here: a BND should not be compared to a normal type");                
                }
            }
            else if (original is IWittyerBnd thisBnd)
            {
                if (otherVariant is IWittyerBnd otherBnd)
                {
                    //representation for bnd
                    alleleMatch = thisBnd.IsContigIntervalContains(otherBnd.OriginalVariant)
                                  && thisBnd.EndInterval.IsContigIntervalContains(otherBnd.EndOriginalVariant) &&
                                  otherBnd.IsContigIntervalContains(thisBnd.OriginalVariant) &&
                                  otherBnd.EndInterval.IsContigIntervalContains(thisBnd.EndOriginalVariant);

                }
                else
                {
                    throw new InvalidDataException("Not sure how we get here: a BND should not be compared to a normal type");
                }

                if (!alleleMatch)
                    failedReason.Add(FailedReason.BndPartialMatch);
            }

            //GT situation
            var isGtMatch = false;
            if (original.Sample is IWittyerGenotypedSample originalGt && otherVariant.Sample is IWittyerGenotypedSample otherGt)
            {
                if (!originalGt.Gt.Equals(otherGt.Gt))
                {
                    failedReason.Add(FailedReason.GtMismatch);
                }
                else
                {
                    isGtMatch = true;
                }
                
            }

            //CNV situation
            if (original.Sample is IWittyerCopyNumberSample cnv &&
                otherVariant.Sample is IWittyerCopyNumberSample otherCnv)
            {
                if (!cnv.Cn.Equals(otherCnv.Cn))
                {
                    alleleMatch = false;
                    failedReason.Add(FailedReason.CnMismatch);
                }
            }

            return (GetCorrectMatchedType(isGtMatch, alleleMatch), GetCorrectFailedReason(failedReason));

        }

        private static bool IsContigIntervalContains([NotNull] this IContigAndInterval originalInterval,
            [NotNull] IContigAndPosition position) 
            => originalInterval.Contig.Equals(position.Contig)
               && originalInterval.Contains(position.Position);

        private static MatchEnum GetCorrectMatchedType(bool isGtMatch, bool isAlleleMatch) 
            => isAlleleMatch
            ? (isGtMatch ? MatchEnum.AlleleAndGenotypeMatch : MatchEnum.AlleleMatch)
            : (isGtMatch ? MatchEnum.LocalAndGenotypeMatch : MatchEnum.LocalMatch);

        private static FailedReason GetCorrectFailedReason([NotNull] IList<FailedReason> failedReasons)
        {
            if (failedReasons.Contains(FailedReason.BndPartialMatch))
                return FailedReason.BndPartialMatch;
            if (failedReasons.Contains(FailedReason.FailedBoundary))
                return FailedReason.FailedBoundary;
            if (failedReasons.Contains(FailedReason.CnMismatch))
                return FailedReason.CnMismatch;
            if (failedReasons.Contains(FailedReason.GtMismatch))
                return FailedReason.GtMismatch;
            return FailedReason.Unset;
        }
    }
}
