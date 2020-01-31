using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Reflection;
using Ilmn.Das.App.Wittyer.Input;
using Ilmn.Das.App.Wittyer.Vcf.Variants;
using Ilmn.Das.Std.VariantUtils.VariantTypes;
using Ilmn.Das.Std.VariantUtils.Vcf;
using Ilmn.Das.Std.VariantUtils.Vcf.Headers.MetaInfoLines;

namespace Ilmn.Das.App.Wittyer.Utilities
{
    /// <summary>
    /// A class to hold constants
    /// </summary>
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

        /// <summary>
        /// The mate identifier Info tag key
        /// </summary>
        public const string MateId = "MATEID";

        /// <summary>
        /// The Filter sample tag key
        /// </summary>
        public const string Ft = "FT";

        /// <summary>
        /// The delimiter for info field's values
        /// </summary>
        public static readonly char InfoValueDel = VcfConstants.InfoFieldValueDelimiter[0];

        /// <summary>
        /// The delimiter for sample field values
        /// </summary>
        public static readonly char SampleValueDel = InfoValueDel;

        /// <summary>
        /// The border distance (aka WHERE) delimiter
        /// </summary>
        public const string BorderDistanceDelimiter = "|";

        /// <summary>
        /// The VCF suffix
        /// </summary>
        public const string VcfSuffix = ".vcf";

        /// <summary>
        /// The VCF gz suffix
        /// </summary>
        public const string VcfGzSuffix = VcfSuffix + ".gz";
        
        /// <summary>
        /// The wit command header key
        /// </summary>
        public const string WitCmdHeaderKey = "witty.erCmd";

        #region  input default stuff

        /// <summary>
        /// The default bp overlap
        /// </summary>
        public const uint DefaultBpOverlap = 500;

        /// <summary>
        /// The default pd
        /// </summary>
        public const double DefaultPd = 0.25;

        /// <summary>
        /// The default bins
        /// </summary>
        public static readonly IImmutableList<(uint size, bool skip)> DefaultBins = ImmutableList.Create((1000U, false), (10000U, false));
        
        /// <summary>
        /// The default includedFilter set
        /// </summary>
        public static readonly IReadOnlyCollection<string> DefaultIncludeFilters
            = ImmutableHashSet.Create(VcfConstants.PassFilter);

        /// <summary>
        /// The default excludedFilter set
        /// </summary>
        public static readonly IReadOnlyCollection<string> DefaultExcludeFilters
            = ImmutableHashSet<string>.Empty;

        /// <summary>
        /// The default insertion <see cref="InputSpec"/>
        /// </summary>
        public static readonly InputSpec DefaultInsertionSpec
            = InputSpec.Create(WittyerType.Insertion, DefaultBins,
                100U, null, DefaultExcludeFilters, DefaultIncludeFilters, null);

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
        /// The starting bin
        /// </summary>
        public const uint StartingBin = 1;

        /// <summary>
        /// The maximum number of annotations to keep.
        /// </summary>
        public const ushort MaxNumberOfAnnotations = 10;

        /// <summary>
        /// The base level stats type strings
        /// </summary>
        public static readonly IImmutableSet<string> BaseLevelStatsTypeStrings =
            ImmutableHashSet.Create(SvTypeStrings.Cnv, SvTypeStrings.Deletion, SvTypeStrings.Duplication);

        /// <summary>
        /// Constants for Json
        /// </summary>
        public static class Json
        {
            /// <summary>
            /// The infinte bin
            /// </summary>
            public const string InfiniteBin = "+";
        }

        /// <summary>
        /// The Meta info line keys for Wittyer
        /// </summary>
        public static class WittyerMetaInfoLineKeys
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

        /// <summary>
        /// The MetoInfo lines for Wittyer
        /// </summary>
        public static class WittyerMetaInfoLines
        {
            /// <summary>
            /// The wit decision header
            /// </summary>
            public static readonly ITypedMetaInfoLine WitDecisionHeader =
                TypedMetaInfoLine.CreateSampleFormatLine(WittyerMetaInfoLineKeys.Wit, TypeField.String,
                    NumberField.SpecificNumber(1), "Decision for call (TP/FP/FN/N). N means not assessed.");

            /// <summary>
            /// The what matched type header
            /// </summary>
            public static readonly ITypedMetaInfoLine WhatMatchedTypeHeader = TypedMetaInfoLine.CreateSampleFormatLine(
                WittyerMetaInfoLineKeys.What, TypeField.String, NumberField.Any,
                "A list of match type for the top ten matches. Could be no match (.) " +
                "or a list consisting of local match but genotype not matching (lm), " +
                "local match with genotype match (lgm); allele match but genotype not matching (am), " +
                "or allele match and genotype match (agm). When a list, it will match the order of WHO and WHERE.");

            /// <summary>
            /// The why failed reason header
            /// </summary>
            public static readonly ITypedMetaInfoLine WhyFailedReasonHeader =
                TypedMetaInfoLine.CreateSampleFormatLine(WittyerMetaInfoLineKeys.Why, TypeField.String, NumberField.Any,
                    "Why the entry failed the evaluation. Current supported values are FailedBoundary/GTmismatch/CNmismatch");

            /// <summary>
            /// The wow overlapping window header
            /// </summary>
            public static readonly ITypedMetaInfoLine WowOverlappingWindowHeader = TypedMetaInfoLine.CreateInfoLine(
                WittyerMetaInfoLineKeys.Wow, TypeField.String, NumberField.Any,
                "A list of interval to describe overlaps with other entries.");

            /// <summary>
            /// The who matched event header
            /// </summary>
            public static readonly ITypedMetaInfoLine WhoMatchedEventHeader = TypedMetaInfoLine.CreateInfoLine(
                WittyerMetaInfoLineKeys.Who, TypeField.String, NumberField.Any,
                "A list of IDs that identify this entry with its matched entry. " +
                "The ID is default to be the position of truth but if there are collisions, the ID will be the truth position incremented to the first unique number. " +
                $"This is the top ten best matches, in the same order as {WittyerMetaInfoLineKeys.Wow} and {WittyerMetaInfoLineKeys.Where}.");

            /// <summary>
            /// The win stratification header
            /// </summary>
            public static readonly ITypedMetaInfoLine WinStratificationHeader =
                TypedMetaInfoLine.CreateInfoLine(WittyerMetaInfoLineKeys.Win, TypeField.String,
                    NumberField.SpecificNumber(1), "The bin category the variant falls into");

            /// <summary>
            /// The where border distance header
            /// </summary>
            public static readonly ITypedMetaInfoLine WhereBorderDistanceHeader =
                TypedMetaInfoLine.CreateInfoLine(WittyerMetaInfoLineKeys.Where, TypeField.String, NumberField.Any,
                    "A list of 4 number-list (1|2|3|4,1|2|3|4,1|2|3|4) describing the boundary distances between the entry and the top ten matches, the order is the same as WHERE and WHO. " +
                    "The 4 numbers are separated by pipe (|), each 4-number-list is separated by comma (,). There are cases of 8-numbered lists when INVs match with two pairs of breakends.");
        }
    }
}