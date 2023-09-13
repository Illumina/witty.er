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
            = new(
                nameof(CopyNumberGain),
                    true,
                    true,
                    true,
                    true,
                null,
                1000,5000,10000,20000,50000);

        /// <summary>
        /// The copy number loss
        /// </summary>
        public static readonly WittyerType CopyNumberLoss
            = new(
                nameof(CopyNumberLoss),
                    true,
                    true,
                    true,
                    true,
                null,
                1000,5000,10000,20000,50000);

        /// <summary>
        /// The copy number reference
        /// </summary>
        public static readonly WittyerType CopyNumberReference
            = new(
                nameof(CopyNumberReference),
                    true,
                    true,
                    true,
                    true,
                null,
                1000,5000,10000,20000,50000);

        /// <summary>
        /// The deletion
        /// </summary>
        public static readonly WittyerType Deletion
            = new(
                nameof(Deletion),
                true,
                true,
                true,
                true,
                SvTypeStrings.Deletion,
                50, 100, 200, 500, 1000, 10000);

        /// <summary>
        /// The duplication
        /// </summary>
        public static readonly WittyerType Duplication
            = new(
                nameof(Duplication),
                    true,
                    true,
                    true,
                    true,
                    SvTypeStrings.Duplication,
                50,1000,10000);

        /// <summary>
        /// The inversion
        /// </summary>
        public static readonly WittyerType Inversion
            = new(
                nameof(Inversion),
                    true,
                    true,
                    true,
                    false,
                    SvTypeStrings.Inversion,
                50,1000,10000);

        /// <summary>
        /// The intra chromosome breakend
        /// </summary>
        public static readonly WittyerType IntraChromosomeBreakend
            = new(
                nameof(IntraChromosomeBreakend),
                    true,
                    true,
                    false,
                    false,
                null,
                50,1000,10000);

        /// <summary>
        /// The insertion
        /// </summary>
        public static readonly WittyerType Insertion
            = new(
                nameof(Insertion),
                true,
                false,
                false,
                false,
                SvTypeStrings.Insertion,
                50, 100, 200, 500, 1000);

        /// <summary>
        /// The translocation breakend
        /// </summary>
        public static readonly WittyerType TranslocationBreakend
            = new(
                nameof(TranslocationBreakend),
                false,
                false,
                false,
                false,
                null);

        /// <summary>
        /// VNTRs
        /// </summary>
        public static readonly WittyerType CopyNumberTandemRepeat
            = new(
                nameof(CopyNumberTandemRepeat),
                true,
                true,
                true,
                false,
                null,
                100, 1000);

        /// <summary>
        /// VNTRs
        /// </summary>
        public static readonly WittyerType CopyNumberTandemReference
            = new(
                nameof(CopyNumberTandemReference),
                true,
                true,
                true,
                false,
                null,
                100, 1000);
        
        /// <summary>
        /// All types
        /// </summary>
        public static readonly IImmutableSet<WittyerType> AllTypes = ImmutableHashSet.Create(Deletion, Duplication,
            CopyNumberGain, CopyNumberLoss, CopyNumberReference, Inversion, IntraChromosomeBreakend, Insertion,
            TranslocationBreakend, CopyNumberTandemRepeat, CopyNumberTandemReference);

        private static readonly IImmutableDictionary<string, WittyerType> AllTypesStrings
            = AllTypes.ToImmutableDictionary(type => type.Name, type => type, StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Gets All types that have unique SVTYPEs (BND and CNV can map to multiple types)
        /// </summary>
        public static readonly IImmutableSet<WittyerType> AllUniqueSvTypes
            = AllTypes.Where(type => type._svTypeName != null).ToImmutableHashSet();

        private static readonly IImmutableDictionary<string, WittyerType> AllSvTypesStrings
            = AllUniqueSvTypes.ToImmutableDictionary(type => type._svTypeName, type => type);

        /// <summary>
        /// Tries to parse the given name into a <see cref="Type"/> (useful for config files).
        /// </summary>
        /// <param name="name">The name.</param>
        /// <param name="type">The type.</param>
        /// <returns></returns>
        [ContractAnnotation("=>true, type:notnull; =>false,type:null")]
        public static bool TryParse(string name, out WittyerType? type) =>
            AllTypesStrings.TryGetValue(name, out type);

        /// <summary>
        /// Tries to parse the given name into a <see cref="Type"/> (useful for config files).
        /// </summary>
        /// <param name="name">The name.</param>
        /// <returns></returns>
        [Pure]
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
        /// <returns></returns>
        /// <exception cref="InvalidDataException">
        /// Following variant does not have {VcfConstants.SvTypeKey} info key:\n{variant}
        /// or
        /// Cannot recognize SVTYPE of {svTypeStr}
        /// </exception>
        [Pure]
        public static FailedReason ParseFromVariant(IVcfVariant variant,
            string? sampleName, out WittyerType? wittyerType, out bool noSplit, int? altIndex = 0)
        {
            noSplit = false; // no split means do not split because it's a CN call and should be a single unit.
            if (variant.IsRefSite())
            {
                wittyerType =
                    variant.Info.TryGetValue(WittyerConstants.RefRucInfoKey, out var refRucStr) &&
                    double.TryParse(refRucStr, out _)
                        ? CopyNumberTandemRepeat
                        : CopyNumberReference;
                return FailedReason.Unset;
            }

            // check for VNTRs (has to be before CNV because of splitting of entries are not for CNVs)
            var alt = altIndex == null || variant.Alts.Count <= altIndex.Value ? null : variant.Alts[altIndex.Value];
            if (alt == "<CNV:TR>")
            {
                wittyerType = CopyNumberTandemRepeat;
                return FailedReason.Unset;
            }
            
            var hasSvTypeKey = variant.Info.TryGetValue(VcfConstants.SvTypeKey, out var svTypeStr);
            var isRefCall = IsRefCall(out var ploidy, out var cn, out var hasCn, out var isDelDup);
            if (isDelDup && hasSvTypeKey && svTypeStr == SvTypeStrings.Cnv)
            {
                noSplit = true;
                if (cn == null || cn.Value == ploidy)
                    isRefCall = true;
            }
            
            if (isRefCall)
            {
                wittyerType = CopyNumberReference;
                return FailedReason.Unset;
            }

            wittyerType = null;
            if (!hasSvTypeKey)
                // todo: maybe we can allow small variants, which would not have SVTYPE
                return FailedReason.InvalidSvType;

            if (svTypeStr == SvTypeStrings.TranslocationBreakend)
            {
                // breakends can be IntraChromosomeBreakend and TranslocationBreakend, so can't tell from SVTYPE.
                    
                ISimpleBreakEnd mate;
                if (variant is IBreakEnd cast)
                    mate = cast.Mate;
                else
                {
                    var parseResult = SimpleBreakEnd.TryParse(variant.GetSingleAlt()).GetOrDefault();
                    if (parseResult == null)
                        return FailedReason.UnsupportedBnd;
                    mate = parseResult;
                }

                wittyerType = variant.Contig.Equals(mate.Contig)
                    ? IntraChromosomeBreakend
                    : TranslocationBreakend;
                return FailedReason.Unset;
            }

            if (svTypeStr != null && !TryParseSvType(svTypeStr, out wittyerType))
            {
                // Not BND because of check above, and if not parsable and not CNV, it's something we don't know.
                if (svTypeStr != SvTypeStrings.Cnv)
                    return FailedReason.InvalidSvType;
            }
            else if (wittyerType?.HasBaseLevelStats == false)
                // If INV or INS or whatever that doesn't need to look for CN, return.
                return FailedReason.Unset;
            
            if (noSplit && cn != null)
            {
                if (cn.Value > ploidy)
                    wittyerType = CopyNumberGain;
                else if (cn.Value < ploidy)
                    wittyerType = CopyNumberLoss;
                else
                    throw new InvalidDataException($"Somehow got to this part.  Should've returned as ref already!");
                return FailedReason.Unset;
            }

            if (!hasCn)
            {
                if (svTypeStr != SvTypeStrings.Cnv)
                    return FailedReason.Unset;  // DEL or DUP without CN
                
                // SVTYPE of CNV but no CN, fallback to ALT
                if (alt == null || !VariantTypeUtils.AltAlleleStringToVariantType.TryGetValue(alt, out var vType))
                    return FailedReason.CnvWithUnsupportedAlt;
                switch (vType)
                {
                    case VariantType.Undetermined:
                    case VariantType.NonReference:
                    case VariantType.Snv:
                    case VariantType.Mnv:
                    case VariantType.Indel:
                    case VariantType.Cnv:
                    case VariantType.Strv:
                    case VariantType.UpstreamDeletion:
                        return FailedReason.CnvWithoutCn;

                    case VariantType.Insertion:
                    case VariantType.MobileElementInsertion:
                        wittyerType = Insertion;
                        break;
                    case VariantType.Deletion:
                    case VariantType.MobileElementDeletion:
                        wittyerType = CopyNumberLoss;
                        break;
                    case VariantType.Duplication:
                    case VariantType.TandemDuplication:
                        wittyerType = CopyNumberGain;
                        break;
                    case VariantType.Reference:
                        wittyerType = CopyNumberReference;
                        break;
                    case VariantType.CnGain:
                        wittyerType = CopyNumberGain;
                        break;
                    case VariantType.CnLoss:
                        wittyerType = CopyNumberLoss;
                        break;
                    case VariantType.TranslocationBreakend:
                        wittyerType = TranslocationBreakend;
                        break;
                    case VariantType.Inversion:
                        wittyerType = Inversion;
                        break;
                    default:
                        throw new ArgumentOutOfRangeException($"Not supported: {vType}");
                }

                return FailedReason.Unset;
            }

            // At this point, it is CNV with CN or DEL/DUP with CN, which are also considered CNV
            if (cn == null)
            {
                // has CN, but can't parse.
                wittyerType = null; // clear out SVTYPE=DEL/DUP
                return FailedReason.UndeterminedCn;
            }
            
            wittyerType = GetSvType(cn.Value);
            return FailedReason.Unset;

            WittyerType GetSvType(double cnValue)
                => cnValue < ploidy
                    ? CopyNumberLoss
                    : CopyNumberGain;

            bool IsRefCall(out int ploidyP, out uint? cnP, out bool hasCnP, out bool isDelDupP)
            {
                isDelDupP = variant.Alts.Count == 2 &&
                           variant.Alts.Contains("<DEL>") && variant.Alts.Contains("<DUP>");
                ploidyP = 2;
                cnP = null;
                hasCnP = false;
                //if not refsite and no sample field, not a refcall
                // todo: be careful though, there's CN calls that are like the following which have no CN (so theoretically maybe no sample, but we'll cross that bridge when we get there):
                // chr5  46275772  DRAGEN:CNLOH:chr5:46275773-46433176 N  <DEL>,<DUP> 357 PASS  SVLEN=-157404,157404;SVTYPE=CNV;END=46433176;REFLEN=157404 GT:SM:SD:MAF:BC:AS:PE  1/2:0.999112:337.7:0.003:104:143:125,4
                if (variant.Samples.Count == 0)
                    return false;

                var sample = sampleName == null ? variant.Samples[0] : variant.Samples[sampleName];
                hasCnP = sample.SampleDictionary.TryGetValue(VcfConstants.CnSampleFieldKey, out var cnString);
                var isGt = sample.SampleDictionary.TryGetValue(VcfConstants.GenotypeKey, out var gt);
                if (hasCnP)
                {
                    if (uint.TryParse(cnString, out var i))
                        cnP = i;
                    else if (double.TryParse(cnString, out var d))
                        cnP = (uint)Math.Round(d);
                }

                if (!isGt)
                    return hasCnP && cnString == "2";

                //todo: refining how to deal with ploidy. Also here we don't deal with LOH. assuming CN = ploidy is ref
                var gtArray = gt!.Split(VcfConstants.GtPhasedValueDelimiter[0],
                    VcfConstants.GtUnphasedValueDelimiter[0]);
                ploidyP = gtArray.Length;
                return cnP == null ? gtArray.All(alleleIndex => alleleIndex == "0") : cnP.Value == ploidyP;
            }
        }

        /// <summary>
        /// The default bins for this type.
        /// </summary>
        public IImmutableList<(uint binSize, bool skip)> DefaultBins { get; }
        
        /// <summary>
        /// The <c>UNIQUELY</c> identifying SVTYPE.
        /// For example, BND is for both <see cref="TranslocationBreakend"/> and <see cref="IntraChromosomeBreakend"/>, so both types have null as its <see cref="_svTypeName"/>.
        /// <see cref="CopyNumberReference"/> doesn't care about SVTYPE, so it also has null.
        /// </summary>
        private readonly string? _svTypeName;

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
        /// Whether this type has overlapping windows, like <see cref="HasBaseLevelStats"/> except it also includes <see cref="Inversion"/>s.
        /// </summary>
        public readonly bool HasOverlappingWindows;

        /// <summary>
        /// Whether this type has lengths, everything but <see cref="TranslocationBreakend"/> and <see cref="Insertion"/> (since it might not have lengths).
        /// </summary>
        public readonly bool HasLengths;

        /// <inheritdoc />
        private WittyerType(string name, bool hasBins, bool hasLengths, bool hasOverlappingWindows, bool hasBaseLevelStats,
            string? svTypeName, params uint[] defaultBins)
        {
            DefaultBins = defaultBins.Select(it => (it, false)).ToImmutableList();
            _svTypeName = svTypeName;
            Name = name;
            HasBins = hasBins;
            HasBaseLevelStats = hasBaseLevelStats;
            HasOverlappingWindows = hasOverlappingWindows;
            HasLengths = hasLengths;
        }

        /// <inheritdoc />
        [Pure]
        public override string ToString() => Name;
    }
}