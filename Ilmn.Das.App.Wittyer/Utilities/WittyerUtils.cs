using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using Ilmn.Das.App.Wittyer.Input;
using Ilmn.Das.App.Wittyer.Stats.Counts;
using Ilmn.Das.App.Wittyer.Utilities.Enums;
using Ilmn.Das.App.Wittyer.Vcf.Variants;
using Ilmn.Das.Core.Tries.Extensions;
using Ilmn.Das.Std.AppUtils.Intervals;
using Ilmn.Das.Std.AppUtils.Misc;
using Ilmn.Das.Std.BioinformaticUtils.Bed;
using Ilmn.Das.Std.BioinformaticUtils.Contigs;
using Ilmn.Das.Std.BioinformaticUtils.GenomicFeatures;
using Ilmn.Das.Std.VariantUtils.SimpleVariants;
using Ilmn.Das.Std.VariantUtils.Vcf;
using Ilmn.Das.Std.VariantUtils.Vcf.Exceptions;
using Ilmn.Das.Std.VariantUtils.Vcf.Variants;
using JetBrains.Annotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Ilmn.Das.App.Wittyer.Utilities
{
    /// <summary>
    /// General basic utility methods
    /// </summary>
    public static class WittyerUtils
    {
        private const string MinusSign = "-";

        private static readonly IImmutableDictionary<string, ImmutableHashSet<string>> TypesNeedingRemoval =
            WittyerType.AllTypes.Where(type => !type.HasLengths || !type.HasBins).ToImmutableDictionary(
                type => type.ToString(),
                type => WittyerSettings.PercentDistanceName.FollowedBy(type.HasBins
                    ? null
                    : WittyerSettings.BinSizesName).ToImmutableHashSet());

        /// <summary>
        /// Serializes a bunch of inputSpecs into a string (config file).
        /// </summary>
        /// <returns></returns>
        public static string SerializeToString(this IEnumerable<InputSpec> inputSpecs)
        {
            var serializedObject = JsonConvert.SerializeObject(inputSpecs);
            var deserialized = JArray.Parse(serializedObject);
            foreach (var token in deserialized)
            {
                if (!TypesNeedingRemoval.TryGetValue(token[WittyerSettings.VariantTypeName].ToString(),
                    out var propertiesToRemove))
                    continue;

                // need to materialize so we don't get modified enumeration error.
                foreach (var property in ((JObject)token).Properties()
                    .Where(p => propertiesToRemove.Contains(p.Name)).ToList())
                    property.Remove();
            }

            return JsonConvert.SerializeObject(deserialized, Formatting.Indented);
        }

        [CanBeNull]
        internal static IInterval<uint> ToBedInterval([NotNull] this IVcfVariant baseVariant,
            bool throwException, out uint endVal, out bool sharedFirstBase)
        {
            endVal = baseVariant.Position;

            if (IsSimpleSequence(baseVariant,
                    out var refLenVal, out sharedFirstBase, out var sharedLastBase, false))
                // first need to save the original end before normalizing
            {
                endVal += refLenVal;

                if (baseVariant.Alts.Count > 0) // refsites don't have Alts in VariantUtils
                    baseVariant = baseVariant.TryNormalizeVariant(VariantNormalizer.TrimCommonBases, 0).GetOrThrow();
            }

            var refLen = baseVariant.GetSvLength(throwException, out sharedFirstBase, out sharedLastBase, out var endPos);
            if (refLen == null) // means insertion of unknown length.
                return null;

            if (endPos != null)
                endVal = endPos.Value;

            var start = sharedFirstBase || baseVariant.Position == 0 ? baseVariant.Position : baseVariant.Position - 1;
            var end = start + refLen.Value;
            if (sharedLastBase) // rare case
                end--;

            return BedInterval.Create(start, end);
        }

        private static uint? GetSvLength([NotNull] this IVcfVariant variant, bool throwException,
            out bool sharedFirstBase, out bool sharedLastBase, out uint? endPos)
        {
            endPos = null;
            if (IsSimpleSequence(variant,
                out var absoluteDiff, out sharedFirstBase, out sharedLastBase, true))
                return absoluteDiff;

            if (variant.Info.TryGetValue(VcfConstants.EndTagKey, out var endStr))
            {
                if (!uint.TryParse(endStr, out var end))
                    return throwException
                            ? throw new InvalidDataException(
                                $"Invalid value for {VcfConstants.EndTagKey} for variant\n{variant}")
                            : default(uint?);
                endPos = end;
                // when end and pos is the same, we do 1, even though based on strict vcf spec, it's 0 length :(
                var diff = end == variant.Position ? 1 : end - variant.Position;
                if (variant.Alts.Count == 0) // ref site has 0 alts in VariantUtils
                    diff++;
                return diff;
            }

            var exception = TryGetSvLength(variant, out var ret);
            return exception == null ? ret : throwException ? throw exception : default(uint?);
        }

        [CanBeNull]
        internal static Exception TryGetSvLength([NotNull] this IVcfVariant variant, out uint svLength)
        {
            svLength = default;
            if (!variant.Info.TryGetValue(VcfConstants.SvLenKey, out var svLenStr))
                return new InvalidDataException(
                    $"Found a symbolic SV have no END or SVLEN key in info field, cannot process the variant \n{variant}");

            if (svLenStr.StartsWith(MinusSign))
                svLenStr = svLenStr.Substring(1);

            return uint.TryParse(svLenStr, out svLength)
                ? null
                : new InvalidDataException($"Invalid value for {VcfConstants.SvLenKey} for variant\n{variant}");
        }

        internal static bool IsSimpleSequence([NotNull] this IVcfVariant variant, out uint absoluteDiff,
            out bool sharedFirstBase, out bool sharedLastBase, bool isTrimmed)
        {
            absoluteDiff = default;
            sharedFirstBase = false;
            sharedLastBase = false;
            if (variant.Alts.Count == 0) return false;
            if (variant.IsAltSimpleSequence(0))
            {
                absoluteDiff = (uint)Math.Max(variant.Ref.Length, variant.Alts[0].Length);
                sharedFirstBase = variant.Ref[0].Letter == variant.Alts[0][0];
                sharedLastBase = variant.Ref.Last().Letter == variant.Alts[0].Last();
                if (isTrimmed && sharedFirstBase && sharedLastBase && (variant.Ref.Length == 1 || variant.Alts[0].Length == 1))
                    sharedLastBase = false; // corner case like chr1 1 A AA would be true for both.

                if (isTrimmed && sharedFirstBase && sharedLastBase)
                    throw new InvalidDataException(
                        "Somehow we got a variant that after trimming, shares first and last base: " + variant);

                if (sharedFirstBase || sharedLastBase)
                    absoluteDiff--;
                return true;
            }

            // always assume the first base is shared in symbolic alleles.
            sharedFirstBase = true;
            // and last base never shared.
            sharedLastBase = false;
            absoluteDiff = default;
            return false;
        }

        [NotNull]
        internal static IInterval<uint> ConvertPositionToCiInterval(
            this uint position, [NotNull] IVcfVariant variant, [NotNull] string ciInfoTag)
        {
            if (!variant.Info.TryGetValue(ciInfoTag, out var posString))
                return BedInterval.Create(position > 0 ? position - 1 : 0, position == 0 ? 1 : position);

            var split = posString.Split(WittyerConstants.InfoValueDel);
            if (split.Length != 2)
                throw VcfVariantFormatException.Create(variant.ToString(), ImmutableHashSet.Create(VcfColumn.Info),
                    $"Invalid {ciInfoTag} found: {posString}", variant.ToStrings().ToList().AsReadOnly());

            var parsedStart = GetParsedAbsValue(split[0]);
            if (parsedStart == null)
                throw new InvalidOperationException($"Failed to parse {ciInfoTag}={posString}!");
            var parsedStop = GetParsedAbsValue(split[1]);
            if (parsedStop == null)
                throw new InvalidOperationException($"Failed to parse {ciInfoTag}={posString}!");
            var (start, stop) = ConvertPositionToCiInterval(position, (parsedStart.Value, parsedStop.Value));
            return BedInterval.Create(start, stop);

            uint? GetParsedAbsValue(string val) 
                => !int.TryParse(val, out var parsed) ? (uint?) null : (uint) (parsed < 0 ? -parsed : parsed);
        }

        internal static (uint zeroStartInclusive, uint zeroStopExclusive) ConvertPositionToCiInterval(this uint position,
            (uint start, uint stop) closedInterval)
        {
            var posStart = position;
            if (posStart > 0)
                posStart--;
            if (closedInterval.start >= posStart)
                posStart = 0;
            else
                posStart -= closedInterval.start;

            return (posStart, position + closedInterval.stop);
        }

        internal static (IContigAndInterval posInterval, IContigAndInterval endInterval) GetPosAndEndInterval(
            [NotNull] IContigInfo first, double? percentageDistance, uint basepairDistance,
            IInterval<uint> ciPosInterval, uint startPosition, IInterval<uint> ciEndInterval, uint stopPosition,
            [CanBeNull] IContigInfo second = null)
        {
            var pd = basepairDistance;
            if (percentageDistance != null)
                pd = (uint)Math.Round((stopPosition - startPosition + 1) * percentageDistance.Value,
                    MidpointRounding.AwayFromZero);

            uint posStart, posStop, endStart, endStop;
            if (pd < basepairDistance)
                GetPosAndEndInterval(pd); // ignore CIPOS and CIEND, and just use pd
            else
            {
                // use basepairDistance or cipos/ciend, whichever is larger magnitude.
                GetPosAndEndInterval(basepairDistance);
                GetBetterInterval(ciPosInterval, ref posStart, ref posStop);
                GetBetterInterval(ciEndInterval, ref endStart, ref endStop);
            }

            var posInterval = ContigAndInterval.Create(first, posStart, posStop);
            var endInterval = posStart == endStart
                              && posStop == endStop
                              && (second == null || Equals(first, second))
                ? posInterval
                : ContigAndInterval.Create(second ?? first, endStart, endStop);

            return (posInterval, endInterval);

            void GetPosAndEndInterval(uint offSet)
            {
                var tuple = (offSet, offSet);
                (posStart, posStop) = startPosition.ConvertPositionToCiInterval(tuple);
                (endStart, endStop) = stopPosition.ConvertPositionToCiInterval(tuple);
            }
        }

        internal static void GetBetterInterval([NotNull] IInterval<uint> ciInterval, ref uint start, ref uint stop)
        {
            if (ciInterval.Start < start) start = ciInterval.Start;
            if (ciInterval.Stop > stop) stop = ciInterval.Stop;
        }

        /// <summary>
        /// Deconstructs the specified <see cref="ISamplePair"/>.
        /// </summary>
        /// <param name="samplePair">The sample pair.</param>
        /// <param name="truthSampleName">Name of the truth sample.</param>
        /// <param name="querySampleName">Name of the query sample.</param>
        public static void Deconstruct([CanBeNull] this ISamplePair samplePair, [CanBeNull] out string truthSampleName, [CanBeNull] out string querySampleName)
        {
            truthSampleName = samplePair?.TruthSampleName;
            querySampleName = samplePair?.QuerySampleName;
        }

        internal static string GetSingleAlt([NotNull] this IVcfVariant variant)
            => variant.Alts.Count != 1
                ? throw new InvalidDataException(
                    $"Only support breakend with one ALT for now, double check this one {variant}")
                : variant.Alts[0];

        /// <summary>
        /// Gets the <see cref="WitDecision"/>.
        /// </summary>
        /// <param name="variant">The variant.</param>
        /// <returns></returns>
        public static WitDecision WitDecision([NotNull] this IWittyerSimpleVariant variant)
            => variant.Sample.Wit;

        /// <summary>
        /// Gets the recall.
        /// </summary>
        /// <param name="stats">The stats.</param>
        /// <returns></returns>
        public static double GetRecall([NotNull] this IStatsUnit stats)
            => (double)stats.TruthStats.TrueCount / stats.TruthStats.GetTotal();

        /// <summary>
        /// Gets the precision.
        /// </summary>
        /// <param name="stats">The stats.</param>
        /// <returns></returns>
        public static double GetPrecision([NotNull] this IStatsUnit stats)
            => (double)stats.QueryStats.TrueCount / stats.QueryStats.GetTotal();

        /// <summary>
        /// Gets the F-score.
        /// </summary>
        /// <param name="stats"></param>
        /// <returns></returns>
        public static double GetFscore([NotNull] this IStatsUnit stats)
        {
            var recall = stats.GetRecall();
            var precision = stats.GetPrecision();
            return 2 * (recall * precision) / (recall + precision);
        }

        /// <summary>
        /// Gets the total of the <see cref="IBasicStatsCount.TrueCount"/> and <see cref="IBasicStatsCount.FalseCount"/>.
        /// </summary>
        /// <param name="stats">The stats.</param>
        /// <returns></returns>
        public static uint GetTotal([NotNull] this IBasicStatsCount stats)
            => stats.TrueCount + stats.FalseCount;
    }
}
