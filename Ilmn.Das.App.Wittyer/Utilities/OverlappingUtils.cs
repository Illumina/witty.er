using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Ilmn.Das.App.Wittyer.Infrastructure;
using Bio;
using Ilmn.Das.App.Wittyer.Input;
using Ilmn.Das.App.Wittyer.Utilities.Enums;
using Ilmn.Das.App.Wittyer.Vcf.Samples;
using Ilmn.Das.App.Wittyer.Vcf.Variants;
using Ilmn.Das.App.Wittyer.Vcf.Variants.Annotations;
using Ilmn.Das.App.Wittyer.Vcf.Variants.Genotype;
using Ilmn.Das.Core.Tries.Extensions;
using Ilmn.Das.Std.AppUtils.Collections;
using Ilmn.Das.Std.AppUtils.Comparers;
using Ilmn.Das.Std.AppUtils.Enums;
using Ilmn.Das.Std.AppUtils.Intervals;
using Ilmn.Das.Std.AppUtils.Misc;
using Ilmn.Das.Std.BioinformaticUtils.Bed;
using Ilmn.Das.Std.BioinformaticUtils.GenomicFeatures;
using Ilmn.Das.Std.BioinformaticUtils.Nucleotides;
using Ilmn.Das.Std.VariantUtils.VariantTypes;
using Ilmn.Das.Std.VariantUtils.Vcf;

namespace Ilmn.Das.App.Wittyer.Utilities
{
    internal static class OverlappingUtils
    {
        private static readonly ISet<uint> WhoTags = new HashSet<uint>();

        private static readonly CustomClassComparer<IWittyerSimpleVariant> StartAndEndComparer =
            new(
                (v1, v2) =>
                {
                    var compare = v1.OriginalVariant.Position.CompareTo(v2.OriginalVariant.Position);
                    if (compare != 0)
                        return compare;
                    compare = v1.EndRefPos.CompareTo(v2.EndRefPos);
                    if (compare != 0)
                        return compare;
                    compare = string.Compare(v1.OriginalVariant.Ref.ToString(), v2.OriginalVariant.Ref.ToString(),
                        StringComparison.Ordinal);
                    if (compare != 0)
                        return compare;
                    compare = string.Compare(v1.OriginalVariant.Alts.StringJoin(VcfConstants.AltDelimiter),
                        v2.OriginalVariant.Alts.StringJoin(VcfConstants.AltDelimiter),
                        StringComparison.Ordinal);
                    if (compare != 0)
                        return compare;
                    compare = v1.OriginalVariant.Quality
                        .Select(it => it.CompareTo(v2.OriginalVariant.Quality.GetOrElse(double.NegativeInfinity)))
                        .GetOrElse(v2.OriginalVariant.Quality.Select(_ => -1).GetOrElse(0));
                    if (compare != 0)
                        return compare;
                    return string.Compare(v1.OriginalVariant.Ids.StringJoin(VcfConstants.IdFieldDelimiter),
                        v2.OriginalVariant.Ids.StringJoin(VcfConstants.IdFieldDelimiter), StringComparison.Ordinal);
                });

        internal static void ClearWhoTags() => WhoTags.Clear();

        internal static void DoOverlapping<T>(
            IReadOnlyDictionary<WittyerType, GenomeIntervalTree<T>> truthTrees,
            T queryVariant, MatchFunc<T> alleleMatchFunc, bool isCrossType, bool isSimpleCounting,
            InputSpec? trInputSpec = null, double similarityThreshold = WittyerConstants.DefaultSimilarityThreshold,
            int? maxMatches = WittyerConstants.DefaultMaxMatches)
            where T : class, IMutableWittyerSimpleVariant
        {
            var overlaps = Enumerable.Empty<T>();
            var hasTree = false;
            var failedReasons = new HashSet<FailedReason>();
            var queryRul = ExtractRul(queryVariant.OriginalVariant);
            if (truthTrees.TryGetValue(queryVariant.VariantType, out var tree))
            {
                hasTree = true;
                overlaps = tree.Search(queryVariant);
                if (queryVariant.VariantType == WittyerType.CopyNumberTandemRepeat)
                    overlaps = overlaps.Where(it =>
                    {
                        if (it.Start != queryVariant.Start || it.Stop != queryVariant.Stop)
                            return false;

                        if (ExtractRul(it.OriginalVariant) == queryRul)
                            return true;
                        failedReasons.Add(FailedReason.RulMismatch);
                        return false;
                    });
            }

            if (isCrossType
                && Quantify.CrossTypeCategories.TryGetValue(
                    queryVariant.VariantType, out var categories))
            {
                overlaps = overlaps.Concat(
                    categories
                        .Where(it =>
                            it.MainType != WittyerType.CopyNumberTandemRepeat
                            && it.SecondaryType != WittyerType.CopyNumberTandemRepeat)
                        .SelectMany(it =>
                        {
                            if (!truthTrees.TryGetValue(
                                    it.MainType == queryVariant.VariantType ? it.SecondaryType! : it.MainType,
                                    out var t)) return Enumerable.Empty<T>();
                            hasTree = true;
                            return t.Search(queryVariant);
                        }));
            }

            var trSpec = trInputSpec ?? WittyerConstants.DefaultTandemRepeatSpec;
            var alleleMatches = DoOverlaps(queryVariant, alleleMatchFunc, isCrossType, failedReasons,
                    isSimpleCounting, overlaps, trSpec, similarityThreshold)
                .ToDictionary(it => it.who, it => it.match);

            queryVariant.OverlapInfo.Sort();
            var gt = queryVariant.Sample is IWittyerGenotypedSample g ? g.Gt : null;
            var isAlleleCount = false;

            int max;
            if (maxMatches == null)
            {
                isAlleleCount = true;
                max = gt?.GenotypeIndices.Count ?? 2;
            }
            else
                max = maxMatches.Value;

            RemoveExtraMatches(queryVariant, max, alleleMatches);

            if (isAlleleCount
                && max > 1
                && gt != null
                // todo: try to properly support BNDs, but turned off temporarily for now.
                && queryVariant.VariantType != WittyerType.TranslocationBreakend
                && queryVariant.VariantType != WittyerType.IntraChromosomeBreakend
                && queryVariant.OverlapInfo.All(it
                    => !it.What.Contains(MatchEnum.Genotype) && it.What.Contains(MatchEnum.Allele)))
            {
                // modify remaining to be correct thing.
                var matchCount = gt.GenotypeIndices.Count(gtValue =>
                    gtValue != VcfConstants.MissingValueString && gtValue != "0");
                if (matchCount > 1)
                    AdjustGenotypeMatches(queryVariant, matchCount, gt, alleleMatches);
            }

            if (isCrossType && queryVariant.VariantType == WittyerType.CopyNumberTandemRepeat)
            {
                // check for other things like VNTR, but only if cross-type matching.
                // VNTR cross-type summary:
                // 1. We figure out the RUC via various methods like in
                //    a. If this allele has RUC, then we just grab it
                //    b. If this allele doesn't have RUC, then we try to get the CN to figure out RUC
                // 2. We then grab all the ins and del in the given region by overlapping and we then overlap specifically with POS+END to narrow the events.
                // 3. we then reconstruct the most likely haplotypes (probably best to construct all combinations) and compare to the SVLEN range using +/- <1.0 of the RUL.
                // caveats: if INS doesn't have length then: 1. if only unknown lengths, we say it's good no matter what or 2. if there's some known and it adds up > then expected SVLEN, then we say FP but if it's less, then TP

                if (
                    !queryVariant.OriginalVariant.Info.TryGetValue(WittyerConstants.RefRucInfoKey, out var refRucStr)
                    || !decimal.TryParse(refRucStr, out var refRuc)
                    || refRuc < 0
                )
                    failedReasons.Add(FailedReason.RucAlleleTruthError);
                else if (!queryVariant.OriginalVariant.Info.TryGetValue(WittyerConstants.RucInfoKey, out var rucStr)
                         || rucStr.Contains('-'))
                    // todo: this might not be needed if we just ignore this and grab CN?
                    failedReasons.Add(FailedReason.RucNotFoundOrInvalid);
                else if (queryRul == null)
                    failedReasons.Add(FailedReason.RulMismatch);
                else
                {
                    uint? who = null;
                    var matched = MatchTrs(truthTrees, queryVariant,
                        trSpec, rucStr,
                        failedReasons, queryRul.Value, queryVariant.EndRefPos, refRuc);

                    if (matched.Count == 0)
                        failedReasons.Add(FailedReason.NoOverlap);
                    else
                        foreach (var overlap in matched)
                        {
                            who ??= NextWhoTag(overlap);

                            var wow = overlap.VariantType.HasOverlappingWindows
                                ? overlap.TryGetOverlap(queryVariant).GetOrDefault()
                                : null;
                            UpdateVariantWithMatchInfo(queryVariant, CrossTypeMatchCntr, true, isSimpleCounting,
                                failedReasons, overlap, who.Value, trSpec, similarityThreshold, wow);
                        }
                }
            }
            else if (alleleMatches.Count == 0 && hasTree)
                // fall through to add failed reasons to result.
                failedReasons.Add(FailedReason.NoOverlap);

            if (failedReasons.Count > 0)
                queryVariant.AddToOverlapInfo(OverlapAnnotation.Create(null, MatchSet.Empty, null, null,
                    GetCorrectFailedReason(failedReasons, isSimpleCounting)));
        }

        private static void RemoveExtraMatches<T>(T queryVariant, int max, Dictionary<uint, T> alleleMatches)
            where T : class, IMutableWittyerSimpleVariant
        {
            if (max >= alleleMatches.Count) return;
            for (var i = max; i < queryVariant.OverlapInfo.Count; i++)
            {
                var who = queryVariant.OverlapInfo[i].Who;
                if (who == null)
                    continue;
                var match = alleleMatches[who.Value];
                var j = 0;
                for (; j < match.OverlapInfo.Count; j++)
                    if (match.OverlapInfo[j].Who == who)
                        break;
                match.OverlapInfo.RemoveAt(j);
            }

            queryVariant.OverlapInfo.RemoveRange(max,
                queryVariant.OverlapInfo.Count - max);
        }

        private static void AdjustGenotypeMatches<T>(T queryVariant, int matchCount, IGenotypeInfo gt,
            Dictionary<uint, T> alleleMatches)
            where T : class, IMutableWittyerSimpleVariant
        {
            var matchedGt = new bool[matchCount];
            var unphased = gt.OriginalGtString.Contains(VcfConstants.GtUnphasedValueDelimiter);
            foreach (var overlap in queryVariant.OverlapInfo)
            {
                var who = overlap.Who;
                if (who == null)
                    continue;
                var match = alleleMatches[who.Value];
                if (match.Sample is not IWittyerGenotypedSample mgt)
                    continue;
                for (var j = 0; j < mgt.Gt.GenotypeIndices.Count; j++)
                    if (mgt.Gt.GenotypeIndices[j] != VcfConstants.MissingValueString &&
                        mgt.Gt.GenotypeIndices[j] != "0")
                    {
                        if (!unphased) // we care about order
                            matchedGt[j] = true;
                        else if (--matchCount < 0)
                            return;

                        matchedGt[matchCount] = true;
                    }
            }

            if (matchedGt.All(it => it))
                AdjustGenotypeMatches(queryVariant, alleleMatches);
        }

        private static void AdjustGenotypeMatches<T>(T queryVariant, Dictionary<uint, T> alleleMatches)
            where T : class, IMutableWittyerSimpleVariant
        {
            for (var i = 0; i < queryVariant.OverlapInfo.Count; i++)
            {
                var overlap = queryVariant.OverlapInfo[i];
                var who = overlap.Who;
                if (who == null)
                    continue;
                var match = alleleMatches[who.Value];
                queryVariant.OverlapInfo[i] = OverlapAnnotation.Create(who,
                    MatchSet.AlleleAndGenotypeMatch,
                    overlap.Wow, overlap.Where,
                    overlap.Why == FailedReason.GtMismatch ? FailedReason.Unset : overlap.Why);
                for (var j = 0; j < match.OverlapInfo.Count; j++)
                {
                    var matchOverlap = match.OverlapInfo[j];
                    if (matchOverlap.Who != who)
                        continue;
                    match.OverlapInfo[j] = OverlapAnnotation.Create(who,
                        MatchSet.AlleleAndGenotypeMatch, matchOverlap.Wow, matchOverlap.Where,
                        matchOverlap.Why == FailedReason.GtMismatch
                            ? FailedReason.Unset
                            : matchOverlap.Why);
                }
            }
        }

        private static uint? ExtractRul(IVcfVariant variant)
            => variant.Info.TryGetValue(WittyerConstants.RulInfoKey, out var rul)
                ? uint.TryParse(rul, out var ret)
                    ? ret
                    : null
                : variant.Info.TryGetValue(WittyerConstants.RusInfoKey, out var rus)
                    ? (uint)rus.Count(n => DnaNucleotide.AllNucleotides.Contains((DnaNucleotide)n))
                    : null;

        private static List<IMutableWittyerSimpleVariant> MatchTrs<T>(
            IReadOnlyDictionary<WittyerType, GenomeIntervalTree<T>> truthTrees, T queryVariant, InputSpec inputSpec,
            string rucStr, ISet<FailedReason> failedReasons, uint rul, uint end, decimal refRuc)
            where T : class, IMutableWittyerSimpleVariant
        {
            var matched = new List<IMutableWittyerSimpleVariant>();

            bool IsLengthInRange(decimal length, bool hasUnknownIns, IInterval<decimal> expectedRange)
                => expectedRange.Stop > length
                   && (hasUnknownIns // assume good!
                       || expectedRange.Contains(length));

            var narrowInterval = BedInterval.Create(queryVariant.OriginalVariant.Position, end);

            // todo: what if query is part of phase group?
            var overlaps = Enumerable.Empty<IWittyerSimpleVariant>();
            if (truthTrees.TryGetValue(WittyerType.Deletion, out var tree))
                overlaps = overlaps.Concat(
                    tree.Search(queryVariant).Where(it =>
                        narrowInterval.Contains(it.Start) && narrowInterval.Contains(it.EndRefPos)));
            // TODO: Turn on when we need DUPs, and also add to the CrossType Bin code
            // if (truthTrees.TryGetValue(WittyerType.Duplication, out tree))
            //     overlaps = overlaps.Concat(
            //         tree.Search(queryVariant).Where(it =>
            //             narrowInterval.Contains(it.Start) && narrowInterval.Contains(it.EndRefPos)));
            if (truthTrees.TryGetValue(WittyerType.Insertion, out tree))
                overlaps = overlaps.Concat(
                    tree.Search(queryVariant).Where(it => narrowInterval.Contains(it.OriginalVariant.Position -
                        (it.OriginalVariant.Position != 0
                         && it.OriginalVariant.Alts.Count > 0
                         && (it.OriginalVariant.Alts[0].StartsWith("<")
                             || it.OriginalVariant.Alts[0].StartsWith(it.OriginalVariant.Ref[0]))
                            ? 0U
                            : 1U))));

            var isDiploidLocus = IsDiploidLocus(queryVariant);
            var groupByPhaseSetsInOrder =
                GroupByPhaseSetsInOrder(overlaps.OfType<IMutableWittyerSimpleVariant>(), isDiploidLocus).ToList();
            if (groupByPhaseSetsInOrder.Count == 0)
                return matched;

            var refLength = refRuc * rul;
            if (groupByPhaseSetsInOrder.Count == 1) // one phased set yay!
            {
                decimal? tmpRefRuc = refRuc;
                var _ = Array.Empty<string>();
                var tup = ExtractRucValueWithBackup(queryVariant, rucStr, ref tmpRefRuc, ref _);
                if (tup == null)
                {
                    failedReasons.Add(FailedReason.RucAlleleTruthError);
                    return matched;
                }

                var phased = groupByPhaseSetsInOrder[0];
                var (_, svlens, phasedOverlaps) = phased;
                var (left, right) = svlens;
                var (leftUnknown, leftAdjust) = left;
                var leftLength = refLength + leftAdjust;

                bool? rightUnknown = null;
                var rightLength = 0.0M;
                if (right != null)
                {
                    var (rightUnknownLocal, rightAdjust) = right.Value;
                    rightLength = refLength + rightAdjust;
                    rightUnknown = rightUnknownLocal;
                }

                var (_, rucValue, rucValue2) = tup.Value;
                var expectedSvLen = rucValue * rul;
                if (rucValue2 == null) // this means we have the total RUC for the whole locus
                {
                    leftLength += rightLength;
                    rightUnknown = null;
                }

                var thresholdLength = inputSpec.GetTrThreshold(expectedSvLen / rul) * rul;

                var expectedStop = expectedSvLen + thresholdLength;
                var expectedLengthInterval =
                    new ExclusiveInterval<decimal>(expectedSvLen - thresholdLength,
                        expectedStop);

                if (IsLengthInRange(leftLength, leftUnknown, expectedLengthInterval)
                    || rightUnknown != null && IsLengthInRange(rightLength, rightUnknown.Value, expectedLengthInterval))
                    return phasedOverlaps;
            }
            else // we are dealing with unphased truth, in this case, we have way too much combinatorial complexity, so we compare total CN.
            {
                decimal rucValue;
                if (TryGetCnValue(queryVariant, out var cn) && cn != null)
                    rucValue = CalculateRucValueFromCnAndRefRuc(refRuc, cn.Value);
                else
                {
                    // try with rucValues
                    decimal? tmpRefRuc = refRuc;
                    var _ = Array.Empty<string>();
                    var tup = ExtractRucValueWithBackup(queryVariant, rucStr, ref tmpRefRuc, ref _);
                    if (tup == null)
                    {
                        failedReasons.Add(FailedReason.RucAlleleTruthError);
                        return matched;
                    }

                    var (_, rucValueTmp, ruc2) = tup.Value;
                    rucValue = rucValueTmp;
                    if (ruc2 != null)
                        rucValue += ruc2.Value;
                }

                var finalLength = isDiploidLocus ? refLength * 2 : refLength;

                var hasUnknownIns = false;

                foreach (var (_, svlens, phasedOverlaps) in groupByPhaseSetsInOrder)
                {
                    var (left, right) = svlens;
                    var (leftUnknown, leftAdjust) = left;
                    finalLength += leftAdjust;
                    if (leftUnknown)
                        hasUnknownIns = true;
                    matched.AddRange(phasedOverlaps);

                    if (right == null) continue;

                    var (rightUnknown, rightAdjust) = right.Value;
                    finalLength += rightAdjust;
                    if (rightUnknown)
                        hasUnknownIns = true;
                }

                var calculatedRuc = finalLength / rul;
                var thresholdLength = inputSpec.GetTrThreshold(calculatedRuc);
                var thresholdStop = rucValue + thresholdLength;
                var thresholdStart = rucValue - thresholdLength;
                if (thresholdStart < 0m)
                    thresholdStart = 0m;
                return calculatedRuc < thresholdStop
                       && (hasUnknownIns
                           || (thresholdStop == thresholdStart
                               ? calculatedRuc == thresholdStart
                               : new ExclusiveInterval<decimal>(thresholdStart, thresholdStop)
                                   .Contains(calculatedRuc)))
                    ? matched
                    : new List<IMutableWittyerSimpleVariant>();
            }

            return matched;
        }

        private static bool IsDiploidLocus<T>(T queryVariant) where T : class, IWittyerSimpleVariant
            => queryVariant.Sample is not IWittyerGenotypedSample cast // assume diploid over haploid
               || cast.Gt.GenotypeIndices.Count > 1;

        private class PhaseSet
        {
            private int _phase;
            private PhaseSet(int phase) => _phase = phase;

            public static PhaseSet Custom(ushort phase)
            {
                var p = (int)phase;
                return TypeCache<int, PhaseSet>.GetOrAdd(p, () => new PhaseSet(p));
            }

            /// <summary>
            /// Uses / as separator
            /// </summary>
            public static readonly PhaseSet Unphased = new(int.MinValue);

            /// <summary>
            /// No PS but using | as seperator
            /// </summary>
            public static readonly PhaseSet PhasedDefaultSet = new(int.MinValue);

            /// <summary>
            /// Phased set because of haploid, though no guarantees on which haploid outside this region
            /// </summary>
            public static readonly PhaseSet PhasedHaploid = new(int.MinValue);

            /// <summary>
            /// Returns either:
            /// 1. A single phase set grouping with left and right alleles lengths.
            /// 2. A per variant (single per original variant) unphased set per left and right alleles lengths.
            ///
            /// the return type also includes whether or not there was an ins of unknown length in there and any new phase sets
            /// (can return unphased sections as a result of change in ploidy from expected ploidy).
            /// </summary>
            /// <returns></returns>
            /// <exception cref="InvalidDataException"></exception>
            /// <exception cref="InvalidOperationException"></exception>
            public static IEnumerable<(
                PhaseSet phaseSet,
                ((bool hasUnknownIns, int length) left,
                (bool hasUnknownIns, int length)? right) lengths,
                List<IMutableWittyerSimpleVariant> variants)> Merge(
                IEnumerable<IMutableWittyerSimpleVariant> variants, PhaseSet phaseSet, bool isDiploidLocus)
            {
                int leftLength = 0, rightLength = 0;
                bool firstInSet = true,
                    hasRight = true,
                    hasLeftIns = false,
                    hasRightIns = false;
                var originalPhaseSet = phaseSet;
                if (!isDiploidLocus && phaseSet != PhasedHaploid)
                {
                    phaseSet = PhasedHaploid; // overwrite temporarily
                    if (originalPhaseSet == Unphased) // haploid is phased always phased so overwrite it permanently
                        originalPhaseSet = phaseSet;
                }

                var currentPhaseSet = phaseSet;
                var setList = new List<IMutableWittyerSimpleVariant>();
                foreach (var sameVariants in variants.OrderBy(it => it, StartAndEndComparer)
                             .SortedGroupBy(StartAndEndComparer))
                {
                    var variantList = sameVariants.OfType<IMutableWittyerSimpleVariant>().ToList();

                    if (variantList.Count > 2)
                        throw new InvalidDataException(
                            $"Got a group of variants that were not quite grouped correctly: \n{variantList.StringJoin('\n')}");

                    var first = variantList[0];
                    var gt = ((IWittyerGenotypedSample)first.Sample).Gt;
                    var isDiploidSample = gt.GenotypeIndices.Count > 1;

                    IWittyerSimpleVariant? left = first, right = null;
                    if (variantList.Count == 2)
                        right = variantList[1];
                    else if (isDiploidSample)
                    {
                        right = left; // either ref 0/1 or homozygous 1/1
                        if (gt.GenotypeIndices[0] == "0") // ref!
                            left = null;
                        else if (gt.GenotypeIndices[1] == "0")
                            right = null;
                    }
                    else if (isDiploidLocus) // diploid VNTR with haploid truth, subtract ref length
                        rightLength -= (int)first.GetRefLength();
                    else
                        hasRight = false;

                    // desired behavior for diploid locus is:
                    // 1. if currently phased and diploid variant, continue with phasing
                    // 2. if unphased originally, just emit regardless
                    // 3. if phased originally, but we are in a haploid region and:
                    //    a. it is the first time: emit previous set and start new phase set as "semi-unphased"
                    //    b. but back to correct ploidy: emit and start new phase set as original phasing.
                    //    c. but still incorrect ploidy: continue with "unphased" phasedSet

                    // desired behavior for haploid locus is:
                    // 4. haploid is always phased, so as long as still haploid, continue with phasing
                    // 5. we are in a diploid region and:
                    //    a. it is the first time: emit previous set and start new phase set as "semi-unphased"
                    //    b. but back to correct ploidy: emit and start new phase set as original phasing.
                    //    c. but still incorrect ploidy: continue with "unphased" phasedSet

                    if ((isDiploidLocus != isDiploidSample // ploidy between VNTR and truth match
                         || currentPhaseSet == Unphased) // case 1. and 4. above
                        && phaseSet == currentPhaseSet) // case 3c. and 5c. above
                    {
                        // we're now either case 2, 3a, 3b, or 5a or 5b, so emit.
                        if (!firstInSet)
                        {
                            yield return GenerateReturnTuple();
                            firstInSet = hasRight = true;
                            leftLength = rightLength = 0;
                            hasLeftIns = hasRightIns = false;
                            setList = new List<IMutableWittyerSimpleVariant>();
                        }

                        currentPhaseSet =
                            isDiploidLocus != isDiploidSample
                                ? Unphased // we break up into a new unphased phaseSet if case 3a. or reassign with no harm done for case 2.
                                : phaseSet; // we're back to correct ploidy, so switch this back aka case 3b.
                    }

                    if (firstInSet)
                    {
                        firstInSet = false;
                    }

                    setList.AddRange(variantList);
                    leftLength += LengthAdjustment(left, ref hasLeftIns);
                    rightLength += LengthAdjustment(right, ref hasRightIns);
                }

                // return last set
                if (!firstInSet)
                    yield return GenerateReturnTuple();

                (PhaseSet phaseSet,
                    ((bool hasUnknownIns, int length) left, (bool hasUnknownIns, int length)? right) lengths,
                    List<IMutableWittyerSimpleVariant> variants) GenerateReturnTuple()
                    => (
                        currentPhaseSet == PhasedHaploid && originalPhaseSet != PhasedHaploid
                            ? originalPhaseSet
                            : currentPhaseSet,
                        ((hasLeftIns, leftLength),
                            hasRight
                                ? (hasRightIns, rightLength)
                                : null), setList);
            }

            private static int LengthAdjustment(IWittyerSimpleVariant? variant, ref bool hasUnknownIns)
            {
                if (variant == null) return 0;

                // null means variant is ref, i.e. variant is not split into two, which means no changes
                if (variant.VariantType == WittyerType.Deletion)
                    return (int)-variant.GetRefLength();
                if (variant is not IWittyerBnd bnd)
                    return (int)variant.GetRefLength();
                if (bnd.SvLenInterval != null)
                    return (int)bnd.SvLenInterval.GetLength();
                hasUnknownIns = true;

                return 0;
            }
        }

        private static IEnumerable<(PhaseSet phaseSet,
            ((bool hasUnknownIns, int length) left,
            (bool hasUnknownIns, int length)? right) lengths,
            List<IMutableWittyerSimpleVariant> variants)> GroupByPhaseSetsInOrder(
            IEnumerable<IMutableWittyerSimpleVariant> source, bool isDiploidLocus)
        {
            PhaseSet? previousPhaseSet = null;
            var variants = new List<IMutableWittyerSimpleVariant>();
            foreach (var variant in source.Where(it => it.Sample is IWittyerGenotypedSample)
                         .OrderBy(it => it, StartAndEndComparer))
            {
                var sample = (WittyerGenotypedSample)variant.Sample;

                PhaseSet phaseSet;
                var isHaploid = sample.Gt.GenotypeIndices.Count < 2; // assumes diploid or haploid at most
                if (!sample.Gt.IsPhased && !isHaploid)
                    phaseSet = PhaseSet.Unphased;
                else if (sample.OriginalSample.SampleDictionary.TryGetValue("PS", out var setStr)
                         && int.TryParse(setStr, out var ps))
                    phaseSet = PhaseSet.Custom((ushort)ps);
                else if (isHaploid)
                    phaseSet = PhaseSet.PhasedHaploid;
                else
                    phaseSet = PhaseSet.PhasedDefaultSet;

                if (previousPhaseSet == null)
                    previousPhaseSet = phaseSet;
                else if (previousPhaseSet != phaseSet)
                {
                    foreach (var (mergedPhaseSet, interval, subVariants) in PhaseSet.Merge(variants, previousPhaseSet,
                                 isDiploidLocus))
                        yield return (mergedPhaseSet, interval, subVariants);
                    previousPhaseSet = phaseSet;
                    variants = new List<IMutableWittyerSimpleVariant>();
                }

                variants.Add(variant);
            }

            if (previousPhaseSet == null || variants.Count <= 0) yield break;
            foreach (var (mergedPhaseSet, interval, subVariants) in PhaseSet.Merge(variants, previousPhaseSet,
                         isDiploidLocus))
                yield return (mergedPhaseSet, interval, subVariants);
        }

        private static IEnumerable<(uint who, T match)> DoOverlaps<T>(T queryVariant,
            MatchFunc<T> alleleMatchFunc,
            bool isCrossType, ICollection<FailedReason> failedReasons,
            bool isSimpleCounting, IEnumerable<T> overlaps, InputSpec trInputSpec, double similarityThreshold)
            where T : class, IMutableWittyerSimpleVariant
        {
            foreach (var overlap in overlaps)
            {
                var who = NextWhoTag(overlap);

                var wow = overlap.VariantType.HasOverlappingWindows
                    ? overlap.TryGetOverlap(queryVariant).GetOrDefault()
                    : null;
                UpdateVariantWithMatchInfo(queryVariant, alleleMatchFunc, isCrossType, isSimpleCounting,
                    failedReasons, overlap, who, trInputSpec, similarityThreshold, wow);
                yield return (who, overlap);
            }
        }

        private static void UpdateVariantWithMatchInfo<T>(T queryVariant, MatchFunc<T> alleleMatchFunc,
            bool isCrossType, bool isSimpleCounting, ICollection<FailedReason> failedReasons, T overlap, uint who,
            InputSpec trInputSpec, double similarityThreshold, IInterval<uint>? wow)
            where T : class, IMutableWittyerSimpleVariant
        {
            var (matchEnum, failedReason) =
                GenerateWhatAndWhy(queryVariant, failedReasons, overlap, alleleMatchFunc, isCrossType, isSimpleCounting,
                    trInputSpec, similarityThreshold);

            var borderDistance = BorderDistance.CreateFromVariant(overlap, queryVariant);

            var overlapInfo =
                OverlapAnnotation.Create(who, matchEnum, wow, borderDistance, failedReason);

            failedReasons.Clear();
            queryVariant.AddToOverlapInfo(overlapInfo);
            overlap.AddToOverlapInfo(overlapInfo);
        }

        private static uint NextWhoTag<T>(T overlap) where T : class, IWittyerSimpleVariant
        {
            var ret = overlap.OriginalVariant.Position;
            while (WhoTags.Contains(ret))
                ret++;
            WhoTags.Add(ret);
            return ret;
        }

        internal static (MatchSet what, FailedReason reason) GenerateWhatAndWhy<T>(
            T query, ICollection<FailedReason> failedReasons, T overlap, MatchFunc<T> alleleMatchFunc,
            bool isCrossType, bool isSimpleCounting, InputSpec trInputSpec,
            double similarityThreshold = WittyerConstants.DefaultSimilarityThreshold)
            where T : class, IWittyerSimpleVariant
        {
            var matchSet = alleleMatchFunc(query, failedReasons, overlap, trInputSpec, similarityThreshold);
            var isAlleleMatch = matchSet.Contains(MatchEnum.Allele);
            if (query.VariantType.HasBaseLevelStats
                && overlap.VariantType.HasBaseLevelStats
                && isAlleleMatch)
            {
                //CNV situation
                isAlleleMatch = isCrossType
                                || query.Sample is not IWittyerCopyNumberSample cnv
                                || overlap.Sample is not IWittyerCopyNumberSample otherCnv
                                || Equals(cnv.Cn, otherCnv.Cn);
                if (!isAlleleMatch)
                {
                    failedReasons.Add(FailedReason.CnMismatch);
                    matchSet = matchSet.Remove(MatchEnum.Allele);
                }
            }

            var (isGt, reason) = GenerateWhatAndWhy(query, failedReasons, overlap, isSimpleCounting);
            if (isGt)
                matchSet = matchSet.Add(MatchEnum.Genotype);
            return (matchSet, reason);
        }

        private static (bool isGt, FailedReason reason) GenerateWhatAndWhy<T>(T query,
            ICollection<FailedReason> failedReasons,
            T overlap, bool isSimpleCounting) where T : class, IWittyerSimpleVariant
        {
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

            return (isGtMatch, GetCorrectFailedReason(failedReasons, isSimpleCounting));
        }

        private static FailedReason GetCorrectFailedReason(ICollection<FailedReason> failedReasons,
            bool isSimpleCounting)
        {
            if (failedReasons.Count == 0)
                return FailedReason.Unset;
            FailedReason ret;
            if (failedReasons.Contains(FailedReason.SequenceMismatch))
                ret = FailedReason.SequenceMismatch;
            else if (failedReasons.Contains(FailedReason.BndPartialMatch))
                ret = FailedReason.BndPartialMatch;
            else if (failedReasons.Contains(FailedReason.BordersTooFarOff))
                ret = FailedReason.BordersTooFarOff;
            else if (failedReasons.Contains(FailedReason.CnMismatch))
                ret = FailedReason.CnMismatch;
            else
            {
                if (failedReasons.Contains(FailedReason.GtMismatch) && !isSimpleCounting)
                    ret = FailedReason.GtMismatch;
                else if (failedReasons.Contains(FailedReason.LengthUnassessed))
                    ret = FailedReason.LengthUnassessed;
                else if (failedReasons.Contains(FailedReason.SequenceUnassessed))
                    ret = FailedReason.SequenceUnassessed;
                else
                    ret = failedReasons.First();
            }

            failedReasons.Clear();
            return ret;
        }

        internal delegate MatchSet MatchFunc<in T>(T query, ICollection<FailedReason> failedReason, T truth,
            InputSpec trInputSpec, double similarityThreshold);


        internal static MatchSet CrossTypeMatchCntr<T>(
            T query, ICollection<FailedReason> failedReasons, T truth, InputSpec _, double similarityThreshold)
            where T : IWittyerSimpleVariant
        {
            var ret = MatchSet.AlleleMatch;
            if (similarityThreshold > 0.0 && WittyerConstants.SequenceComparable.Contains(truth.VariantType))
                failedReasons.Add(FailedReason.SequenceUnassessed);
            return ret;
        }

        internal static MatchSet MatchBnd<T>(
            T query, ICollection<FailedReason> failedReason, T truth, InputSpec _, double similarityThreshold)
            where T : IWittyerSimpleVariant
            => MatchBnd(query, failedReason, truth, similarityThreshold);

        private static MatchSet MatchBnd<T>(
            T query, ICollection<FailedReason> failedReason, T truth, double similarityThreshold)
            where T : IWittyerSimpleVariant
        {
            var matchSet = MatchSet.LocalMatch;
            var castQuery = (IWittyerBnd)query;
            var castTruth = (IWittyerBnd)truth;
            // shouldn't need to check the pos end since that was used to search tree
            if (!Equals(castQuery.EndOriginalVariant.Contig, castTruth.EndOriginalVariant.Contig)
                || !castQuery.EndInterval.TryGetOverlap(castTruth.EndInterval).Any())
            {
                failedReason.Add(FailedReason.BndPartialMatch);
                return matchSet;
            }

            matchSet = matchSet.Add(MatchEnum.Allele);

            if (castQuery.SvLenInterval == null || castTruth.SvLenInterval == null)
            {
                if (castQuery.VariantType.HasLengths && castTruth.VariantType.HasLengths)
                {
                    failedReason.Add(FailedReason.LengthUnassessed);
                    if (WittyerConstants.SequenceComparable.Contains(castQuery.VariantType)
                        && WittyerConstants.SequenceComparable.Contains(castTruth.VariantType))
                        failedReason.Add(FailedReason.SequenceUnassessed);
                }
            }
            else if (similarityThreshold == 0.0
                     || GetLengthRatio(castQuery.SvLenInterval, castTruth.SvLenInterval) >=
                     similarityThreshold)
                matchSet = matchSet.Add(MatchEnum.Length);
            else
                failedReason.Add(FailedReason.LengthMismatch);

            return CheckForSequence(query, failedReason, truth, similarityThreshold, matchSet);
        }

        private static MatchSet CheckForSequence<T>(
            T query, ICollection<FailedReason> failedReason, T truth, double similarityThreshold, MatchSet matchSet)
            where T : IWittyerSimpleVariant
            => RequiresSequenceChecking(query, truth)
                ? CompareSequences(query, failedReason, truth, matchSet, similarityThreshold)
                : matchSet;

        private static MatchSet CompareSequences<T>(T query, ICollection<FailedReason> failedReason, T truth,
            MatchSet matchSet, double similarityThreshold)
            where T : IWittyerSimpleVariant
        {
            var shorter = query.OriginalVariant.Alts[0];
            var longer = truth.OriginalVariant.Alts[0];
            var shorterPos = query.Start;
            var longerPos = truth.Start;
            var isTruthSymbolic = longer.StartsWith("<");
            // 1. symbolic vs symbolic => normal matching
            // 2. symbolic truth vs seq query => normal matching but WHY:SequenceUnassessed
            // 3. seq truth vs symbolic query => normal matching but WHY:SequenceUnassessed
            // 4. seq truth vs seq query => sequence similarity matching
            var isQuerySymbolic = shorter.StartsWith("<");
            if (isTruthSymbolic || isQuerySymbolic)
            {
                if (isTruthSymbolic != isQuerySymbolic)
                    failedReason.Add(FailedReason.SequenceUnassessed);
                return matchSet;
            }

            if (similarityThreshold == 0.0) // user explictly asked for no sequence checks.
                return matchSet.Add(MatchEnum.Sequence);

            if (shorter.Length > longer.Length)
            {
                (shorter, longer) = (longer, shorter);
                (shorterPos, longerPos) = (longerPos, shorterPos);
            }

            if (!matchSet.Contains(MatchEnum.Length))
            {
                // previously checked and if length isn't even enough, we definitely do not have a sequence match
                var chars = new HashSet<char>(shorter);
                if (longer.Any(chars.Contains))
                    matchSet = matchSet.Add(MatchEnum.PartialSequence); // any overlap would mean partial sequence
                return matchSet;
            }

            var length = longer.Length;
            var score = CompareTwoSequences(shorter, longer, length);
            if (score == null)
            {
                failedReason.Add(FailedReason.SequenceUnassessed);
                return matchSet;
            }

            if (score.Value < similarityThreshold && query.Start != truth.Start && query.Stop != truth.Stop)
            {
                (shorter, longer) = WittyerUtils.Unroll(shorter, shorterPos,
                    longer, longerPos);

                var score2 = CompareTwoSequences(shorter, longer, length);
                score = score2 >= similarityThreshold ? score2 : score.Value;
            }

            return score >= similarityThreshold
                ? matchSet.Add(MatchEnum.Sequence)
                : score >= 0.0
                    ? matchSet.Add(MatchEnum.PartialSequence)
                    : matchSet;
        }

        private static bool RequiresSequenceChecking<T>(
            T query, T truth) where T : IWittyerSimpleVariant
            => query.VariantType == truth.VariantType
               && WittyerConstants.SequenceComparable.Contains(query.VariantType);

        private static double? CompareTwoSequences(string seq1, string seq2, int length)
        {
            if (seq1 == seq2)
                return 1.0;
            
            if ((long)seq1.Length * seq2.Length >= 2_000_000_000L) // 2147483647 is the max from the third party library
                return null; // we do not assess sequence when this happens.
            
            var alignments = WittyerConstants.Aligner.Align(
                new Sequence(Alphabets.AmbiguousDNA, seq1),
                new Sequence(Alphabets.AmbiguousDNA, seq2));
            var alignmentFirst = alignments.First();

            var alignment = alignmentFirst.First();
            return alignment.Score / (double)length;
        }

        internal static MatchSet VariantMatch<T>(
            T query, ICollection<FailedReason> failedReason, T truth, InputSpec trInputSpec,
            double similarityThreshold) where T : class, IWittyerSimpleVariant
        {
            var ret = MatchSet.LocalMatch;

            var castQuery = (IWittyerVariant)query;
            var castTruth = (IWittyerVariant)truth;
            if (castQuery.SvLenInterval == null || castTruth.SvLenInterval == null)
                failedReason.Add(FailedReason.LengthUnassessed); // should never happen, but just in case
            else if (similarityThreshold == 0.0
                || GetLengthRatio(castQuery.SvLenInterval, castTruth.SvLenInterval) >= similarityThreshold)
                ret = ret.Add(MatchEnum.Length);

            if (!castQuery.PosInterval.TryGetOverlap(castTruth.PosInterval).Any()
                || !castQuery.EndInterval.TryGetOverlap(castTruth.EndInterval).Any())
            {
                failedReason.Add(FailedReason.BordersTooFarOff);
                return ret;
            }

            if (castQuery.VariantType == WittyerType.CopyNumberTandemRepeat
                && castTruth.VariantType == WittyerType.CopyNumberTandemRepeat)
            {
                if (castTruth.OriginalVariant.Info.TryGetValue(WittyerConstants.RucInfoKey, out var trueRuc)
                    && castQuery.OriginalVariant.Info.TryGetValue(WittyerConstants.RucInfoKey, out var ruc))
                {
                    var isRucsSame = IsRucsSame(castQuery, failedReason, castTruth, trInputSpec, trueRuc, ruc);
                    if (isRucsSame)
                        ret = ret.Add(MatchEnum.Allele);
                    return ret;
                }

                failedReason.Add(FailedReason.RucNotFoundOrInvalid);
                return ret;
            }

            ret = ret.Add(MatchEnum.Allele);

            return CheckForSequence(query, failedReason, truth, similarityThreshold, ret);
        }

        private static double GetLengthRatio(IInterval<uint> queryInterval, IInterval<uint> truthInterval)
        {
            var shorterLen = queryInterval.GetLength();
            var longerLen = truthInterval.GetLength();
            if (shorterLen > longerLen)
                (shorterLen, longerLen) = (longerLen, shorterLen);
            var ratio = shorterLen / (double)longerLen;
            return ratio;
        }

        private static bool IsRucsSame<T>(
            T query, ICollection<FailedReason> failedReason, T truth,
            InputSpec trInputSpec,
            string trueRuc,
            string ruc) where T : class, IWittyerVariant
        {
            decimal? refRucValue = null;
            var trueCount = 2;
            var trueRucValueTmp = ExtractRucValue(trueRuc, out var trueRucSplit);
            decimal? trueRucValue2 = null;
            var truthMissingRuc = false;
            decimal trueRucValue;
            if (trueRucValueTmp == null)
            {
                truthMissingRuc = true;
                var tup = ExtractRucValueWithBackup(truth, trueRuc, ref refRucValue, ref trueRucSplit);
                if (tup == null)
                {
                    failedReason.Add(FailedReason.RucAlleleTruthError);
                    return false;
                }

                (trueCount, trueRucValue, trueRucValue2) = tup.Value;
            }
            else
                trueRucValue = trueRucValueTmp.Value;

            var ploidy = 2;
            var rucValueTmp = ExtractRucValue(ruc, out var rucSplit);
            decimal? rucValue2 = null;
            decimal rucValue;
            if (truthMissingRuc || rucValueTmp == null)
            {
                var tup = ExtractRucValueWithBackup(query, ruc, ref refRucValue, ref rucSplit);
                if (tup == null)
                {
                    failedReason.Add(FailedReason.RucNotFoundOrInvalid);
                    return false;
                }

                (ploidy, rucValue, rucValue2) = tup.Value;

                if (!truthMissingRuc)
                {
                    tup = ExtractRucValueWithBackup(truth, trueRuc, ref refRucValue, ref trueRucSplit);
                    if (tup == null)
                    {
                        failedReason.Add(FailedReason.RucAlleleTruthError);
                        return false;
                    }

                    (trueCount, trueRucValue, trueRucValue2) = tup.Value;
                }

                if (ploidy != trueCount)
                {
                    failedReason.Add(FailedReason.RucAlleleCountDiff);
                    return false;
                }
            }
            else
                rucValue = rucValueTmp.Value;

            if (trueRucValue2 == null && rucValue2 != null)
                rucValue += rucValue2.Value;
            else if (rucValue2 == null && trueRucValue2 != null)
                trueRucValue += trueRucValue2.Value;

            var trThreshold = trInputSpec.GetTrThreshold(trueRucValue);
            var start = trueRucValue - trThreshold;
            if (start < 0.0m)
            {
                if (rucValue == 0.0m)
                    return true;
                start = 0.0m;
            }

            if (new ExclusiveInterval<decimal>(start, trueRucValue + trThreshold).Contains(rucValue))
                return true;

            failedReason.Add(FailedReason.RucMismatch);
            return false;
        }

        internal static decimal? ExtractRucValue(string ruc, out string[] rucSplit)
        {
            rucSplit = ruc.Split(VcfConstants.InfoFieldValueDelimiter);
            return decimal.TryParse(rucSplit[0], out var rucValue) && rucValue >= 0 ? rucValue : null;
        }

        /// <summary>
        /// This extracts the RUC and gives back multiple possible values (assumes that there are only ever 2 possible RUC values, since WittyerVcfReader collapsed all NON_REF:
        /// 1. the first RUC value is there and parseable (not a missing value): returns the count of RUC values, the ruc value and second allele's ruc value if available and parseable, otherwise 0.
        /// 2. the first ruc is missing a value we try to use CN and REFRUC to calculate the Ruc Value from those:
        ///    a. we return null (no values at all) if no CN, no REFRUC, or either is not parseable (only possible way to get null back).
        ///    b. if we have additional info on the second ruc value (e.g. ./4.0), then we can give the second ruc value in the third tuple item
        ///    b. If not, we give back null for the second ruc value (this case indicates that we are in case 2 since this is the only time we return null for ruc2).
        /// Caller needs to handle it from there.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        internal static (int ploidy, decimal ruc, decimal? ruc2)? ExtractRucValueWithBackup<T>(
            T variant, string ruc, ref decimal? refRuc, ref string[] rucSplit) where T : class, IWittyerSimpleVariant
        {
            if (rucSplit.Length == 0)
                rucSplit = ruc.Split(VcfConstants.InfoFieldValueDelimiter);
            // we assume the first ruc is the current one we want to analyze.

            var ploidy = 2;
            var nonMissingIndices = new List<int>();
            var hasRef = false;
            if (variant.Sample is IWittyerGenotypedSample sample)
            {
                ploidy = sample.Gt.GenotypeIndices.Count;
                nonMissingIndices = sample.Gt.GenotypeIndices
                    .Where(it => it != VcfConstants.MissingValueString).Select(int.Parse).ToList();
                hasRef = nonMissingIndices.Contains(0);
            }

            var rucValue = 0.0M;
            var ruc2 = 0.0M;
            if (hasRef && refRuc == null)
            {
                if (!ExtractRefRuc(variant, out var refRucTemp)) return null;
                refRuc = refRucTemp;
            }

            var successFor1 = false;
            var successFor2 = true;
            foreach (var ind in nonMissingIndices)
            {
                if (ind == 0)
                    ruc2 += refRuc ?? 0.0M;
                else if (decimal.TryParse(rucSplit[ind - 1], out var ruc2Value))
                {
                    if (ind == 1)
                    {
                        rucValue += ruc2Value;
                        successFor1 = true;
                        continue;
                    }

                    if (ruc2Value < 0)
                        return null;
                    ruc2 += ruc2Value;
                }
                else if (ind != 1)
                    successFor2 = false;
            }

            if (!successFor2 || !nonMissingIndices.Contains(2))
            {
                ruc2 = 0.0M;
                if (refRuc != null)
                {
                    var additional = refRuc.Value;
                    ruc2 = nonMissingIndices.Sum(it => it == 0 ? additional : 0.0M);
                }
            }

            if (successFor1) // means this was handled above already
                return rucValue >= 0 ? (ploidy, rucValue, ruc2) : null;

            decimal refRucValue;
            if (refRuc == null)
            {
                if (hasRef || !ExtractRefRuc(variant, out var refRucTemp)) return null;
                refRucValue = refRucTemp;
                refRuc = refRucTemp;
            }
            else
                refRucValue = refRuc.Value;


            // ruc is probably ".", so we need to calculate it based on the FORMAT:CN tag
            if (!TryGetCnValue(variant, out var cn) || cn == null || cn.Value < 0) return null;
            rucValue = CalculateRucValueFromCnAndRefRuc(refRucValue, cn.Value);

            if (ploidy == 1)
                return (ploidy, rucValue, 0);

            return rucSplit[0] == VcfConstants.MissingValueString
                   || rucSplit[1] == VcfConstants.MissingValueString
                ? (ploidy, rucValue, null)
                : (ploidy, rucValue - ruc2, ruc2);
        }

        private static bool ExtractRefRuc<T>(T variant, out decimal refRucTemp)
            where T : class, IWittyerSimpleVariant
        {
            refRucTemp = -2.0M;
            return variant.OriginalVariant.Info.TryGetValue(WittyerConstants.RefRucInfoKey, out var refRucStr)
                   && decimal.TryParse(refRucStr, out refRucTemp)
                   && refRucTemp >= 0;
        }

        private static decimal CalculateRucValueFromCnAndRefRuc(decimal refRucValue,
            decimal cn)
            => refRucValue * cn;

        private static bool TryGetCnValue<T>(T variant, out decimal? cn) where T : class, IWittyerSimpleVariant
        {
            cn = null;
            if (variant.Sample.OriginalSample == null
                || !variant.Sample.OriginalSample.SampleDictionary.TryGetValue(VcfConstants.CnSampleFieldKey,
                    out var cnStr)
                || !decimal.TryParse(cnStr, out var cnVal))
                return false;

            cn = cnVal;
            return true;
        }
    }
}