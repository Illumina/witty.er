using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Ilmn.Das.App.Wittyer.Input;
using Ilmn.Das.App.Wittyer.Stats.Counts;
using Ilmn.Das.App.Wittyer.Utilities.Enums;
using Ilmn.Das.App.Wittyer.Vcf.Samples;
using Ilmn.Das.App.Wittyer.Vcf.Variants;
using Ilmn.Das.Core.BgZip;
using Ilmn.Das.Core.Tries;
using Ilmn.Das.Core.Tries.Extensions;
using Ilmn.Das.Std.AppUtils.Intervals;
using Ilmn.Das.Std.AppUtils.Misc;
using Ilmn.Das.Std.AppUtils.Collections;
using Ilmn.Das.Std.BioinformaticUtils.Bed;
using Ilmn.Das.Std.BioinformaticUtils.Contigs;
using Ilmn.Das.Std.BioinformaticUtils.GenomicFeatures;
using Ilmn.Das.Std.BioinformaticUtils.Nucleotides;
using Ilmn.Das.Std.VariantUtils.SimpleVariants;
using Ilmn.Das.Std.VariantUtils.VariantTypes;
using Ilmn.Das.Std.VariantUtils.Vcf;
using Ilmn.Das.Std.VariantUtils.Vcf.Exceptions;
using Ilmn.Das.Std.VariantUtils.Vcf.Variants.Alleles;
using JetBrains.Annotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using IVcfVariant = Ilmn.Das.App.Wittyer.Vcf.Variants.IVcfVariant;
using VcfVariant = Ilmn.Das.App.Wittyer.Vcf.Variants.VcfVariant;

namespace Ilmn.Das.App.Wittyer.Utilities
{
    /// <summary>
    /// General basic utility methods
    /// </summary>
    public static class WittyerUtils
    {
        private const string MinusSign = "-";

        private static readonly IImmutableDictionary<string, ImmutableHashSet<string>> TypesNeedingRemoval =
            WittyerType.AllTypes.Where(type => !type.HasLengths || !type.HasBins && type != WittyerType.CopyNumberTandemRepeat).ToImmutableDictionary(
                type => type.ToString(),
                type => WittyerSettings.PercentDistanceName.FollowedBy(type.HasBins
                    ? null
                    : WittyerSettings.BinSizesName).ToImmutableHashSet());

        private static readonly IReadOnlyCollection<string> NonRefStrings = new List<string>
        {
            SymbolicAltAlleleStrings.NonRef, SymbolicAltAlleleStrings.NonRefPisces, "<*>"
        }.AsReadOnly();
        
        /// <summary>
        /// 
        /// </summary>
        /// <param name="inputSpec"></param>
        /// <param name="refRuc"></param>
        /// <returns></returns>
        public static decimal GetTrThreshold(this InputSpec inputSpec, decimal truthSize)
        {
            var absoluteThreshold = inputSpec.AbsoluteThreshold;
            var percentDistance = inputSpec.PercentThreshold;
            if (percentDistance == null) return absoluteThreshold;
            var percentageThreshold = (decimal) percentDistance.Value * truthSize;
            if (percentageThreshold < 0)
                percentageThreshold = -percentageThreshold;
            if (absoluteThreshold < 0)
                absoluteThreshold = -absoluteThreshold;
            return percentageThreshold < absoluteThreshold ? absoluteThreshold : percentageThreshold;
        }

        public static bool IsNonRef(this string source) => NonRefStrings.Contains(source); 

        /// <summary>
        /// Serializes a bunch of inputSpecs into a string (config file).
        /// </summary>
        /// <returns></returns>
        public static string SerializeToString(this IEnumerable<InputSpec> inputSpecs)
        {
            var serializedObject = JsonConvert.SerializeObject(inputSpecs);
            var deserialized = JArray.Parse(serializedObject);
            for (var i = 0; i < deserialized.Count; i++)
            {
                var token = deserialized[i];
                var newToken = new JObject();
                var map = new Dictionary<string, string>();
                var wittyerType = token[WittyerSettings.VariantTypeName]!.ToString();

                if (!TypesNeedingRemoval.TryGetValue(wittyerType,
                        out var propertiesToRemove))
                    propertiesToRemove = ImmutableHashSet<string>.Empty; 
                
                var bpd = token[WittyerSettings.BpDistanceName];
                if (bpd != null)
                {
                    propertiesToRemove = propertiesToRemove.Add(WittyerSettings.BpDistanceName);
                    map[WittyerSettings.BpDistanceName] = WittyerSettings.AbsoluteThresholdName;
                    token[WittyerSettings.AbsoluteThresholdName] = bpd;
                }
                var pd = token[WittyerSettings.PercentDistanceName];
                if (pd != null)
                {
                    propertiesToRemove = propertiesToRemove.Add(WittyerSettings.PercentDistanceName);
                    map[WittyerSettings.PercentDistanceName] = WittyerSettings.PercentThresholdName;
                    token[WittyerSettings.PercentThresholdName] = pd;
                }

                if (propertiesToRemove.Count <= 0)
                    continue;
                foreach (var property in ((JObject)token).Properties())
                {
                    var name = property.Name;
                    if (propertiesToRemove.Contains(property.Name))
                    {
                        if (!map.TryGetValue(property.Name, out var newName))
                            continue;
                        name = newName;
                    }
                    newToken[name] = token[property.Name];
                }

                deserialized[i] = newToken;
            }

            return JsonConvert.SerializeObject(deserialized, Formatting.Indented);
        }

        public static uint GetRefLength(this IWittyerSimpleVariant source)
            => source.EndRefPos - source.Start;

        public static FileInfo GetUnzippedFileInfo(this FileInfo source)
        {
            if (!source.ExistsNow() || !source.IsGZipFile())
                return source;

            var tmpFile = Path.GetTempFileName().ToFileInfo();
            using var writer = new StreamWriter(tmpFile.FullName);
            using var zip = new StreamReader(new GZipStream(source.OpenRead(), CompressionMode.Decompress));
            foreach (var line in zip.ReadAllLines())
                writer.WriteLine(line);
            var destFileName = Path.Combine(tmpFile.Directory!.FullName, $"{tmpFile.Name}.vcf");
            tmpFile.MoveTo(destFileName);
            return destFileName.ToFileInfo();
        }

        internal static IInterval<uint>? ToBedInterval(this IVcfVariant baseVariant,
            bool throwException, out uint endVal, out bool sharedFirstBase, int? altIndex)
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

            var refLen = baseVariant.GetSvLength(throwException, out sharedFirstBase, out sharedLastBase, out var endPos, altIndex);
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

        [Pure]
        public static (string shorter, string longer) Unroll(string shorter, uint shortPos, string longer, uint longPos)
        {
            string UnrollString(string target, int f)
            {
                var s = target[^f..];
                var o = target[..^f];
                return s + o;
            }

            // unroll https://github.com/ACEnglish/truvari/wiki/bench#unroll
            var posDiff = (int)((long)longPos - shortPos);
            if (posDiff < 0)
                posDiff = -posDiff;
            if (shortPos < longPos)
            {
                // e.g. ATATAT vs -TATA
                // https://github.com/ACEnglish/truvari/blob/fbb556c084340f7fa103c2e7865ce9946082fb3d/truvari/comparisons.py#L569
                var f = posDiff % shorter.Length;
                shorter = UnrollString(shorter, f);
            }
            else
            {
                var f = posDiff % longer.Length;
                longer = UnrollString(longer, f);
            }

            return (shorter, longer);
        }

        private static uint? GetSvLength(this IVcfVariant variant, bool throwException,
            out bool sharedFirstBase, out bool sharedLastBase, out uint? endPos, int? altIndex)
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

            var exception = TryGetSvLength(variant, out var ret, altIndex ?? 0);
            return exception == null ? ret : throwException ? throw exception : default(uint?);
        }

        internal static Exception? TryGetSvLength(this IVcfVariant variant, out uint svLength, int altIndex)
        {
            svLength = default;
            if (!variant.Info.TryGetValue(VcfConstants.SvLenKey, out var svLenStr))
                return new InvalidDataException(
                    $"Found a symbolic SV have no END or SVLEN key in info field, cannot process the variant \n{variant}");

            var svlenSplit = svLenStr.Split(VcfConstants.InfoFieldValueDelimiter);
            var svLenStrNew = svlenSplit[altIndex];
            if (svLenStrNew.StartsWith(MinusSign))
                svLenStrNew = svLenStrNew[1..];

            return uint.TryParse(svLenStrNew, out svLength)
                ? null
                : new InvalidDataException($"Invalid value for {VcfConstants.SvLenKey} ({svLenStr}) for variant\n{variant}");
        }

        internal static bool IsSimpleSequence(this IVcfVariant variant, out uint absoluteDiff,
            out bool sharedFirstBase, out bool sharedLastBase, bool isTrimmed)
        {
            absoluteDiff = default;
            sharedFirstBase = false;
            sharedLastBase = false;
            if (variant.Alts.Count == 0) return false;
            if (variant.IsAltSimpleSequence(0))
            {
                absoluteDiff = (uint) Math.Max(variant.Ref.Length, variant.Alts[0].Length);
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

        internal static IInterval<uint> ConvertPositionToCiInterval(
            this uint position, IVcfVariant variant, string ciInfoTag)
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
                => !int.TryParse(val, out var parsed) ? null : (uint) (parsed < 0 ? -parsed : parsed);
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

            var stop = position + closedInterval.stop;
            if (posStart == stop)
                stop++;
            return (posStart, stop);
        }

        internal static (IContigAndInterval posInterval, IContigAndInterval endInterval) GetPosAndEndInterval(
            IContigInfo first, double? percentageDistance, uint basepairDistance,
            IInterval<uint> ciPosInterval, uint startPosition, IInterval<uint> ciEndInterval, uint stopPosition,
            bool isTr, IContigInfo? second = null)
        {
            var pd = basepairDistance;
            if (percentageDistance != null)
                pd = percentageDistance == 0.0
                    ? 0
                    : (uint)Math.Round((stopPosition - startPosition + 1) * percentageDistance.Value,
                        MidpointRounding.AwayFromZero);

            uint posStart, posStop, endStart, endStop;
            if (isTr || pd >= basepairDistance)
            {
                // use basepairDistance or cipos/ciend, whichever is larger magnitude.
                GetPosAndEndInterval(basepairDistance);
                GetBetterInterval(ciPosInterval, ref posStart, ref posStop);
                GetBetterInterval(ciEndInterval, ref endStart, ref endStop);
            }
            else
                GetPosAndEndInterval(pd); // ignore CIPOS and CIEND, and just use pd

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

        internal static void GetBetterInterval(IInterval<uint> ciInterval, ref uint start, ref uint stop)
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
        public static void Deconstruct(this ISamplePair? samplePair, out string? truthSampleName, out string? querySampleName)
        {
            truthSampleName = samplePair?.TruthSampleName;
            querySampleName = samplePair?.QuerySampleName;
        }

        internal static string GetSingleAlt(this IVcfVariant variant)
            => variant.Alts.Count != 1
                ? throw new InvalidDataException(
                    $"Only support breakend with one ALT for now, double check this one {variant}")
                : variant.Alts[0];

        /// <summary>
        /// Gets the <see cref="WitDecision"/>.
        /// </summary>
        /// <param name="variant">The variant.</param>
        /// <returns></returns>
        public static WitDecision WitDecision(this IWittyerSimpleVariant variant)
            => variant.Sample.Wit;

        /// <summary>
        /// Gets the recall.
        /// </summary>
        /// <param name="stats">The stats.</param>
        /// <returns></returns>
        public static double GetRecall(this IStatsUnit stats)
            => (double)stats.TruthStats.TrueCount / stats.TruthStats.GetTotal();

        /// <summary>
        /// Gets the precision.
        /// </summary>
        /// <param name="stats">The stats.</param>
        /// <returns></returns>
        public static double GetPrecision(this IStatsUnit stats)
            => (double)stats.QueryStats.TrueCount / stats.QueryStats.GetTotal();

        /// <summary>
        /// Gets the F-score.
        /// </summary>
        /// <param name="stats"></param>
        /// <returns></returns>
        public static double GetFscore(this IStatsUnit stats)
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
        public static uint GetTotal(this IBasicStatsCount stats)
            => stats.TrueCount + stats.FalseCount;
        
        public static IEnumerable<string> ToStrings(this IVcfVariant variant)
        {
            yield return variant.Contig.Name;
            yield return variant.Position.ToString();
            yield return ToString(variant.Ids, ";");
            yield return variant.Ref.ToString();
            yield return ToString(variant.Alts, ",");
            yield return variant.QualityString();
            yield return ToString(variant.Filters, ";");
            yield return variant.Info.Count == 0 ? "." : variant.Info.Select(InfoFieldKeyValueToString).StringJoin(";");
            var samplesString = variant.Samples.ToString();
            if (samplesString != string.Empty)
                yield return samplesString;

            string InfoFieldKeyValueToString(KeyValuePair<string, string> infoKeyValue) => !infoKeyValue.Value.IsNullOrEmpty() ? infoKeyValue.Key + "=" + infoKeyValue.Value : infoKeyValue.Key;
        }
        
        [Pure]
        public static string QualityString(this IVcfVariant variant) => variant.Quality.Select(q => q.ToString(CultureInfo.InvariantCulture)).GetOrElse<string>(".");
        private static string ToString(IReadOnlyCollection<string> list, string altDelimiter) => list.Count != 0 ? list.StringJoin(altDelimiter) : ".";
        [Pure]
        public static ITry<IVcfVariant> TryNormalizeVariant(
            this IVcfVariant variant,
            VariantNormalizerMethod normalizer,
            ushort altAlleleIndex)
        {
            return TryFactory.Try(new Func<IVcfVariant>(NormalizeVariant));

            IVcfVariant NormalizeVariant()
            {
                (IContigInfo, uint, DnaString, string) variant2 = (variant.Contig, variant.Position, variant.Ref, variant.Alts[altAlleleIndex]);
                var (contigInfo, position, reference, str) = normalizer(variant2);
                return variant2.Item1.Equals(contigInfo) && (int) variant2.Item2 == (int) position && variant2.Item3 == reference && variant2.Item4 == str && variant.Alts.Count == 1 ? variant : variant.ToBuilder().SetContig(contigInfo).SetPosition(position).SetRef(reference).SetAlts(str?.FollowedBy().ToImmutableList() ?? ImmutableList<string>.Empty).Build();
            }
        }
        
        [Pure]
        public static VcfVariant.Builder ToBuilder(this IVcfVariant variant) => new VcfVariant.Builder(variant.Contig, variant.Position, variant.Ref).SetAlts(variant.Alts).SetIds(variant.Ids).SetQuality(variant.Quality).SetFilters(variant.Filters).SetInfo(variant.Info).SetSamples(variant.Samples);
        [Pure]
        public static SampleBuilder ToBuilder(this SampleDictionaries dictionaries) => SampleBuilder.CreateSampleBuilder(dictionaries);
        [Pure]
        public static IVcfVariant ToUcscStyleVariant(this IVcfVariant variant)
        {
            var ucscStyle = variant.Contig.ToUcscStyle();
            return ucscStyle.Equals(variant.Contig) ? variant : ChangeBreakendVariant(variant, ucscStyle, x => x.ToUcscStyle());
        }

        [Pure]
        public static IVcfVariant ToGrchStyleVariant(this IVcfVariant variant)
        {
            var grchStyle = variant.Contig.ToGrchStyle();
            return grchStyle.Equals(variant.Contig) ? variant : ChangeBreakendVariant(variant, grchStyle, x => x.ToGrchStyle());
        }
        
        private static IVcfVariant ChangeBreakendVariant(
            IVcfVariant variant,
            IContigInfo ensemblContig,
            Func<IContigInfo, IContigInfo> conversionFunc)
        {
            var stringList = new List<string>();
            foreach (var alt in variant.Alts)
            {
                if (!IsBreakendAltAllele(alt))
                {
                    stringList.Add(alt);
                }
                else
                {
                    int num1;
                    for (num1 = 0; num1 < alt.Length; ++num1)
                    {
                        var ch = alt[num1];
                        if (ch.Equals(BndDistalFivePrimeKeyChar) || ch.Equals(BndDistalThreePrimeKeyChar))
                            break;
                    }
                    var startIndex = alt.IndexOf(':', num1);
                    var num2 = num1 + 1;
                    var name = alt.Substring(num2, startIndex - num2);
                    stringList.Add(alt.Substring(0, num2) + conversionFunc(ContigInfo.Create(name)).Name + alt.Substring(startIndex));
                }
            }
            return SetContig(variant, ensemblContig, stringList.AsReadOnly());
        }
        internal const string BndDistalThreePrimeKey = "]";
        internal static readonly char BndDistalThreePrimeKeyChar = BndDistalThreePrimeKey[0];
        internal const string BndDistalFivePrimeKey = "[";
        internal const char BndContigPositionDelimiter = ':';
        internal static readonly char BndDistalFivePrimeKeyChar = BndDistalFivePrimeKey[0];

        private static IVcfVariant SetContig(
            IVcfVariant variant,
            IContigInfo contig,
            IReadOnlyList<string>? alts = null) =>
            new VcfVariant(contig, variant.Position, variant.Ids, variant.Ref, alts ?? variant.Alts, variant.Quality, variant.Filters, variant.Info, variant.Samples);
        internal static bool IsBreakendAltAllele(string altAllele) => altAllele.StartsWith(BndDistalThreePrimeKey) || altAllele.StartsWith(BndDistalFivePrimeKey) || altAllele.EndsWith(BndDistalThreePrimeKey) || altAllele.EndsWith(BndDistalFivePrimeKey);

        public static bool IsPassFilter(this IVcfVariant variant) =>
            variant.Filters.Count == 1 && variant.Filters[0] == VcfConstants.PassFilter;
    }
}