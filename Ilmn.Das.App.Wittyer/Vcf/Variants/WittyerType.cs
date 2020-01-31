using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using Ilmn.Das.App.Wittyer.Utilities;
using Ilmn.Das.App.Wittyer.Utilities.Enums;
using Ilmn.Das.Core.Tries.Extensions;
using Ilmn.Das.Std.VariantUtils.SimpleVariants;
using Ilmn.Das.Std.VariantUtils.VariantTypes;
using Ilmn.Das.Std.VariantUtils.Vcf;
using Ilmn.Das.Std.VariantUtils.Vcf.Variants;
using Ilmn.Das.Std.VariantUtils.Vcf.Variants.Alleles.BreakEnds;
using JetBrains.Annotations;

namespace Ilmn.Das.App.Wittyer.Vcf.Variants
{
    /// <summary>
    /// The SvType under Wittyer
    /// </summary>
    public class WittyerType
    {
        /// <summary>
        /// The copy number gain
        /// </summary>
        public static readonly WittyerType CopyNumberGain
            = new WittyerType(nameof(CopyNumberGain), true, true, true, true, true);

        /// <summary>
        /// The copy number loss
        /// </summary>
        public static readonly WittyerType CopyNumberLoss
            = new WittyerType(nameof(CopyNumberLoss), true, true, true, true, true);

        /// <summary>
        /// The copy number reference
        /// </summary>
        public static readonly WittyerType CopyNumberReference
            = new WittyerType(nameof(CopyNumberReference), true, true, true, true, false);

        /// <summary>
        /// The deletion
        /// </summary>
        public static readonly WittyerType Deletion
            = new WittyerType(nameof(Deletion), true, true, true, true, false, SvTypeStrings.Deletion);

        /// <summary>
        /// The duplication
        /// </summary>
        public static readonly WittyerType Duplication
            = new WittyerType(nameof(Duplication), true, true, true, true, false, SvTypeStrings.Duplication);

        /// <summary>
        /// The inversion
        /// </summary>
        public static readonly WittyerType Inversion
            = new WittyerType(nameof(Inversion), true, true, true, false, false, SvTypeStrings.Inversion);

        /// <summary>
        /// The intra chromosome breakend
        /// </summary>
        public static readonly WittyerType IntraChromosomeBreakend
            = new WittyerType(nameof(IntraChromosomeBreakend), true, true, false, false, false);

        /// <summary>
        /// The insertion
        /// </summary>
        public static readonly WittyerType Insertion
            = new WittyerType(nameof(Insertion), true, false, false, false, false, SvTypeStrings.Insertion);

        /// <summary>
        /// The translocation breakend
        /// </summary>
        public static readonly WittyerType TranslocationBreakend
            = new WittyerType(nameof(TranslocationBreakend), false, false, false, false, false);

        /// <summary>
        /// All types
        /// </summary>
        public static readonly IImmutableSet<WittyerType> AllTypes = ImmutableHashSet.Create(Deletion, Duplication,
            CopyNumberGain, CopyNumberLoss, CopyNumberReference, Inversion, IntraChromosomeBreakend, Insertion,
            TranslocationBreakend);

        private static readonly IImmutableDictionary<string, WittyerType> AllTypesStrings =
            AllTypes.ToImmutableDictionary(type => type.Name, type => type, StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Gets All types that have unique SVTYPEs (BND and CNV can map to multiple types)
        /// </summary>
        public static readonly IImmutableSet<WittyerType> AllUniqueSvTypes =
            AllTypes.Where(type => type._svTypeName != null).ToImmutableHashSet();

        private static readonly IImmutableDictionary<string, WittyerType> AllSvTypesStrings =
            AllUniqueSvTypes.ToImmutableDictionary(type => type._svTypeName, type => type);

        /// <summary>
        /// Tries to parse the given name into a <see cref="Type"/> (useful for config files).
        /// </summary>
        /// <param name="name">The name.</param>
        /// <param name="type">The type.</param>
        /// <returns></returns>
        [ContractAnnotation("=>true, type:notnull; =>false,type:null")]
        public static bool TryParse(string name, [CanBeNull] out WittyerType type) =>
            AllTypesStrings.TryGetValue(name, out type);

        /// <summary>
        /// Tries to parse the given name into a <see cref="Type"/> (useful for config files).
        /// </summary>
        /// <param name="name">The name.</param>
        /// <returns></returns>
        [Pure]
        [NotNull]
        public static WittyerType Parse(string name) => TryParse(name, out var ret)
            ? ret
            : throw new KeyNotFoundException($"{name} is not a valid {nameof(WittyerType)}!");

        /// <summary>
        /// Tries the parse the given SVTYPE string into a <see cref="Type"/>.
        /// Not good to use publicly since we override SVTYPE by CNV types if they contain CN.
        /// </summary>
        /// <param name="svType">Type of the sv.</param>
        /// <param name="type">The type.</param>
        /// <returns></returns>
        [ContractAnnotation("=>true, type:notnull; =>false,type:null")]
        internal static bool TryParseSvType(string svType, out WittyerType type)
            => AllSvTypesStrings.TryGetValue(svType, out type);

        /// <summary>
        /// Parses the type of the wittyer variant and returns the type or a <see cref="FailedReason"/>.
        /// <c>IMPORTANT NOTE:</c>
        ///      This will return <see cref="CopyNumberReference"/> even if it doesn't make sense to because not doing that is even worse (SVTYPE=INS, but is a ref site, for example).
        ///      So if it's <see cref="CopyNumberReference"/>, you should double check that it is a supported <see cref="Type"/>.
        /// </summary>
        /// <param name="variant">The variant.</param>
        /// <param name="isCrossTypeOn">Whether or not crosstype matching is on.</param>
        /// <param name="sampleName">Name of the sample.</param>
        /// <param name="svType">The parsed <see cref="Type"/> if there was no <see cref="FailedReason"/>.</param>
        /// <returns></returns>
        /// <exception cref="InvalidDataException">
        /// Following variant does not have {VcfConstants.SvTypeKey} info key:\n{variant}
        /// or
        /// Cannot recognize SVTYPE of {svTypeStr}
        /// </exception>
        [Pure]
        [ContractAnnotation("=>null,svType:notnull;=>notnull,svType:null")]
        public static FailedReason? ParseFromVariant([NotNull] IVcfVariant variant, bool isCrossTypeOn,
            [CanBeNull] string sampleName, [CanBeNull] out WittyerType svType)
        {
            if (variant.IsRefSite() || IsRefCall(out var ploidy, out var cn, out var hasCn))
            {
                svType = CopyNumberReference;
                return null;
            }

            var hasSvTypeKey = variant.Info.TryGetValue(VcfConstants.SvTypeKey, out var svTypeStr);
            if (!hasSvTypeKey)
                // todo: maybe we can allow small variants, which would not have SVTYPE
                throw new InvalidDataException(
                    $"Following variant does not have {VcfConstants.SvTypeKey} info key:\n{variant}");

            svType = null;
            if (svTypeStr == SvTypeStrings.TranslocationBreakend)
            {
                // breakends can be IntraChromosomeBreakend and TranslocationBreakend, so can't tell from SVTYPE.
                var mate = variant is IBreakEnd cast
                    ? cast.Mate
                    : SimpleBreakEnd.TryParse(variant.GetSingleAlt()).GetOrThrow();
                svType = variant.Contig.Equals(mate.Contig)
                    ? IntraChromosomeBreakend
                    : TranslocationBreakend;
                return null;
            }

            if (!TryParseSvType(svTypeStr, out svType))
            {
                // Not BND because of check above, and if not parsable and not CNV, it's something we don't know.
                if (svTypeStr != SvTypeStrings.Cnv)
                    throw new InvalidDataException($"Cannot recognize SVTYPE of {svTypeStr}");
            }
            else if (!svType.HasBaseLevelStats)
                // If INV or INS or whatever that doesn't need to look for CN, return.
                return null;

            if (!hasCn)
                return svType == null
                    ? FailedReason.CnvWithoutCn
                    : default(FailedReason?); // DEL or DUP without CN

            // At this point, it is CNV with CN or DEL/DUP with CN, which are also considered CNV
            if (cn == null)
            {
                // has CN, but can't parse.
                svType = null; // clear out SVTYPE=DEL/DUP
                return FailedReason.UndeterminedCn;
            }

            svType = GetSvType(cn.Value);
            return null;

            WittyerType GetSvType(int cnValue)
                => cnValue < ploidy
                    ? (isCrossTypeOn ? Deletion : CopyNumberLoss)
                    : (isCrossTypeOn ? Duplication : CopyNumberGain);

            bool IsRefCall(out int ploidyP, out int? cnP, out bool hasCnP)
            {
                ploidyP = 2;
                cnP = null;
                hasCnP = false;
                //if not refsite and no sample field, not a refcall
                if (variant.Samples.Count == 0)
                    return false;

                var sample = sampleName == null ? variant.Samples[0] : variant.Samples[sampleName];
                hasCnP = sample.SampleDictionary.TryGetValue(VcfConstants.CnSampleFieldKey, out var cnString);
                var isGt = sample.SampleDictionary.TryGetValue(VcfConstants.GenotypeKey, out var gt);
                if (hasCnP && int.TryParse(cnString, out var i))
                    cnP = i;
                if (!isGt)
                    return hasCnP && cnString == "2";

                //todo: refining how to deal with ploidy. Also here we don't deal with LOH. assuming CN = ploidy is ref
                var gtArray = gt.Split(VcfConstants.GtPhasedValueDelimiter[0],
                    VcfConstants.GtUnphasedValueDelimiter[0]);
                ploidyP = gtArray.Length;
                return cnP == null ? gtArray.All(alleleIndex => alleleIndex == "0") : cnP.Value == ploidyP;
            }
        }

        /// <summary>
        /// The <c>UNIQUELY</c> identifying SVTYPE.
        /// For example, BND is for both <see cref="TranslocationBreakend"/> and <see cref="IntraChromosomeBreakend"/>, so both types have null as its <see cref="_svTypeName"/>.
        /// <see cref="CopyNumberReference"/> doesn't care about SVTYPE, so it also has null.
        /// </summary>
        [CanBeNull] private readonly string _svTypeName;

        /// <summary>
        /// The name of this <see cref="Type"/>, should be a full word.
        /// </summary>
        public readonly string Name;

        /// <summary>
        /// Whether this type has bins (basically everything but <see cref="TranslocationBreakend"/>).
        /// </summary>
        public readonly bool HasBins;

        /// <summary>
        /// Whether this type has base level stats, mainly the DEL/DUP/CNVs
        /// </summary>
        public readonly bool HasBaseLevelStats;

        /// <summary>
        /// Whether this type has overlapping windows, everything but <see cref="TranslocationBreakend"/>, <see cref="IntraChromosomeBreakend"/>, and <see cref="Insertion"/>
        /// </summary>
        public readonly bool HasOverlappingWindows;

        /// <summary>
        /// Whether this type has lengths, everything but <see cref="TranslocationBreakend"/> and <see cref="Insertion"/>
        /// </summary>
        public readonly bool HasLengths;

        /// <summary>
        /// Whether this type is a CNV.
        /// </summary>
        public readonly bool IsCopyNumberVariant;

        /// <inheritdoc />
        private WittyerType(string name, bool hasBins, bool hasLengths, bool hasOverlappingWindows, bool hasBaseLevelStats,
            bool isCopyNumberVariant, [CanBeNull] string svTypeName = null)
        {
            _svTypeName = svTypeName;
            Name = name;
            HasBins = hasBins;
            HasBaseLevelStats = hasBaseLevelStats;
            IsCopyNumberVariant = isCopyNumberVariant;
            HasOverlappingWindows = hasOverlappingWindows;
            HasLengths = hasLengths;
        }

        /// <inheritdoc />
        [NotNull]
        [Pure]
        public override string ToString() => Name;
    }
}