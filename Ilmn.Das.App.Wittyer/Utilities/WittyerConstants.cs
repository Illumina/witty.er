using System;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using Ilmn.Das.App.Wittyer.Vcf.Variants;
using Ilmn.Das.Std.AppUtils.Enums;
using Ilmn.Das.Std.VariantUtils.Vcf;
using Ilmn.Das.Std.VariantUtils.Vcf.Headers.MetaInfoLines;

namespace Ilmn.Das.App.Wittyer.Utilities
{
    public static class WittyerConstants
    {
        /// <summary>
        /// The cipos
        /// </summary>
        public const string Cipos = "CIPOS";

        /// <summary>
        /// The ciend
        /// </summary>
        public const string Ciend = "CIEND";

        public const string MateId = "MATEID";

        public const string Ft = "FT";

        public const char InfoValueDel = ',';

        public const char SampleValueDel = ',';

        public const string BorderDistanceDelimiter = "|";

        public const string MissingValueWow = ".";

        public const string NonMatchWhat = ".";

        public const string VcfSuffix = ".vcf";

        public static readonly string VcfGzSuffix = $"{VcfSuffix}.gz";

        /// <summary>
        /// The wit command header key
        /// </summary>
        public const string WitCmdHeaderKey = "witty.erCmd";

        #region  input default stuff

        public const uint DefaultBpOverlap = 500;

        public const double DefaultPd = 0.05;

        public static readonly IImmutableList<uint> DefaultBins = ImmutableList.Create(1000U, 10000U);
        /// <summary>
        /// The default includedFilter set
        /// </summary>
        public static readonly IImmutableSet<string> DefaultIncludeFilters
            = ImmutableHashSet.Create(VcfConstants.PassFilter);

        #endregion


        /// <summary>
        /// The current version
        /// </summary>
        public static readonly Version CurrentVersion = Assembly.GetExecutingAssembly().GetName().Version;

        /// <summary>
        /// The witty version header
        /// </summary>
        public const string WittyerVersionHeader = VcfConstants.Header.MetaInfoLines.Keys.Source;


        /// <summary>
        ///     The character representing a forward break end alt allele.
        /// </summary>
        public const char BndDistalFivePrimeKey = '[';

        /// <summary>
        ///     The character representing a reverse break end alt allele.
        /// </summary>
        public const char BndDistalThreePrimeKey = ']';

        internal const char BndContigPositionDelimiter = ':';

        public const uint StartingBin = 1;

        public static readonly IImmutableList<WittyerVariantType> SupportedSvType =
            ImmutableList.Create(WittyerVariantType.CopyNumberReference, WittyerVariantType.Cnv,
                WittyerVariantType.Deletion,
                WittyerVariantType.Insertion, WittyerVariantType.Inversion, WittyerVariantType.Duplication,
                WittyerVariantType.TranslocationBreakend, WittyerVariantType.IntraChromosomeBreakend);

        public static readonly IImmutableSet<WittyerVariantType> BaseLevelStatsTypes =
            ImmutableHashSet.Create(WittyerVariantType.Cnv, WittyerVariantType.Duplication, WittyerVariantType.Deletion,
                WittyerVariantType.CopyNumberReference);

        public static readonly IImmutableSet<string> BaseLevelStatsTypeStrings =
            BaseLevelStatsTypes.Select(x => x.ToStringDescription()).ToImmutableHashSet();

        public static readonly IImmutableSet<WittyerVariantType> NoOverlappingWindowTypes =
            ImmutableHashSet.Create(WittyerVariantType.IntraChromosomeBreakend, WittyerVariantType.TranslocationBreakend,
                WittyerVariantType.Insertion);

        public static class Json
        {
            public const string InfinteBin = "+";
        }

        public static class WittyMetaInfoLineKeys
        {
            /// <summary>
            /// The WIT format key 
            /// </summary>
            public const string Wit = "WIT";

            /// <summary>
            /// The WHAT format key
            /// </summary>
            public const string What = "WHAT";

            /// <summary>
            /// The WHY format key
            /// </summary>
            public const string Why = "WHY";

            /// <summary>
            /// The WOW info key
            /// </summary>
            public const string Wow = "WOW";

            /// <summary>
            /// The WHO info key
            /// </summary>
            public const string Who = "WHO";

            /// <summary>
            /// The WIN info key
            /// </summary>
            public const string Win = "WIN";

            /// <summary>
            /// The WHERE info key
            /// </summary>
            public const string Where = "WHERE";

            /// <summary>
            /// The default truth sample name in the output vcf
            /// </summary>
            public const string DefaultTruthSampleName = "TRUTH";

            /// <summary>
            /// The default query sample name in the output vcf
            /// </summary>
            public const string DefaultQuerySampleName = "QUERY";

            /// <summary>
            /// The original sample name line key
            /// </summary>
            public const string OriginalSampleNameLineKey = "SAMPLENAME";
        }

        public static class WittyMetaInfoLines
        {
            public static readonly ITypedMetaInfoLine WitDecisionHeader =
                TypedMetaInfoLine.CreateSampleFormatLine(WittyMetaInfoLineKeys.Wit, TypeField.String,
                    NumberField.SpecificNumber(1), "Decision for call (TP/FP/FN/N). N means not assessed.");

            public static readonly ITypedMetaInfoLine WhatMatchedTypeHeader = TypedMetaInfoLine.CreateSampleFormatLine(
                WittyMetaInfoLineKeys.What, TypeField.String, NumberField.Any,
                "A list of match type for the top ten matches. Could be no match (.) " +
                "or a list consisting of local match but genotype not matching (lm), " +
                "local match with genotype match (lgm); allele match but genotype not matching (am), " +
                "or allele match and genotype match (agm). When a list, it will match the order of WHO and WHERE.");

            public static readonly ITypedMetaInfoLine WhyFailedReasonHeader =
                TypedMetaInfoLine.CreateSampleFormatLine(WittyMetaInfoLineKeys.Why, TypeField.String, NumberField.Any,
                    "Why the entry failed the evaluation. Current supported values are FailedBoundary/GTmismatch/CNmismatch");

            public static readonly ITypedMetaInfoLine WowOverlappingWindowHeader = TypedMetaInfoLine.CreateInfoLine(
                WittyMetaInfoLineKeys.Wow, TypeField.String, NumberField.Any,
                "A list of interval to describe overlaps with other entries.");

            public static readonly ITypedMetaInfoLine WhoMatchedEventHeader = TypedMetaInfoLine.CreateInfoLine(
                WittyMetaInfoLineKeys.Who, TypeField.String, NumberField.Any,
                "A list of IDs that identify this entry with its matched entry. " +
                $"This is the top ten best matches, in the same order as {WittyMetaInfoLineKeys.Wow} and {WittyMetaInfoLineKeys.Where}.");

            public static readonly ITypedMetaInfoLine WinStratificationHeader =
                TypedMetaInfoLine.CreateInfoLine(WittyMetaInfoLineKeys.Win, TypeField.String,
                    NumberField.SpecificNumber(1), "The bin category the variant falls into");

            public static readonly ITypedMetaInfoLine WhereBorderDistanceHeader =
                TypedMetaInfoLine.CreateInfoLine(WittyMetaInfoLineKeys.Where, TypeField.String, NumberField.Any,
                    "A list of 4 number-list (1|2|3|4,1|2|3|4,1|2|3|4) describing the boundary distances between the entry and the top ten matches, the order is the same as WHERE and WHO. " +
                    "The 4 numbers are separated by pipe (|), each 4-number-list is separated by comma (,). There are cases of 8-numbered lists when INVs match with two pairs of breakends.");
        }
    }
}