using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using Ilmn.Das.App.Wittyer.Vcf.Variants;
using Ilmn.Das.App.Wittyer.Input;
using Ilmn.Das.App.Wittyer.Vcf.Variants.Breakend;
using Ilmn.Das.Std.VariantUtils.VariantTypes;
using Ilmn.Das.Std.VariantUtils.Vcf;
using Ilmn.Das.Std.VariantUtils.Vcf.Variants;
using Ilmn.Das.Std.VariantUtils.Vcf.Variants.Alleles.BreakEnds;
using JetBrains.Annotations;

namespace Ilmn.Das.App.Wittyer.Utilities
{
    internal static class WittyerVariantUtils
    {
        private const string MinusSign = "-";

        internal static uint GetSvLength([NotNull] this IVcfVariant variant)
        {
            // possible bug if more than one shared base or no shared bases! see https://jira.illumina.com/browse/WIT-86
            if (IsSimpleSequence(variant, out var absoluteDiff)) return absoluteDiff;

            if (variant.Info.TryGetValue(VcfConstants.EndTagKey, out var endStr))
            {
                return uint.TryParse(endStr, out var end)
                    ? GetAbsoluteDiff(end, variant.Position)
                    : throw new InvalidDataException(
                        $"Invalid value for {VcfConstants.EndTagKey} for variant\n{variant}");
            }

            var exception = TryGetSvLength(variant, out var ret);
            return exception == null ? ret : throw exception;
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

        internal static bool IsSimpleSequence([NotNull] this IVcfVariant variant, out uint absoluteDiff)
        {
            var isSequence = variant.Alts.All(x =>
                x.All(nucleotide => VcfConstants.ValidAltNucleotideChars.Contains(nucleotide)));
            if (isSequence)
            {
                absoluteDiff = GetAbsoluteDiff((uint) variant.Alts.First().Length, (uint) variant.Ref.Length);
                return true;
            }

            absoluteDiff = default;
            return false;
        }

        private static uint GetAbsoluteDiff(uint value1, uint value2) => value1 > value2 ? value1 - value2 : value2 - value1;

        internal static WittyerVariantType ParseWittyerVariantType([NotNull] this IVcfVariant variant,
            [CanBeNull] string sampleName)
        {
            if (variant.IsRefCall(sampleName))
                return WittyerVariantType.CopyNumberReference;

            //anything NOT a refcall requires SVTYPE INFO key
            if (!variant.Info.TryGetValue(VcfConstants.SvTypeKey, out var svTypeStr))
                throw new InvalidDataException(
                    $"Following variant does not have {VcfConstants.SvTypeKey} info key:\n{variant}");

            if (TryParseEnumOrDescription(svTypeStr, out WittyerVariantType svType))
            {
                if (variant.Samples.Count > 0
                    && variant.Samples[0].SampleDictionary.ContainsKey(VcfConstants.CnSampleFieldKey)
                    && WittyerConstants.BaseLevelStatsTypes.Contains(svType))
                    return WittyerVariantType.Cnv;
                return svType;
            }

            if (!TryParseEnumOrDescription(svTypeStr, out SvType type) ||
                    !type.Equals(SvType.TranslocationBreakend))
                    throw new InvalidDataException($"Cannot recognize {svTypeStr}");

            var bnd = variant as IGeneralBnd ?? GeneralBnd.Create(variant);
            return !bnd.Contig.Equals(bnd.Mate.Contig)
                ? WittyerVariantType.TranslocationBreakend
                : WittyerVariantType.IntraChromosomeBreakend;
        }

        internal static bool TryParseEnumOrDescription<T>(this string description, out T result) where T : struct
            => Enum.TryParse(description, true, out result) || GetEnumValueFromDescription(description, out result);

        private static bool GetEnumValueFromDescription<T>(string description, out T result)
        {
            var fis = typeof(T).GetFields();

            foreach (var fi in fis)
            {
                var attributes = (DescriptionAttribute[])fi.GetCustomAttributes(typeof(DescriptionAttribute), false);

                if (attributes == null || attributes.Length <= 0 || attributes[0].Description != description) continue;
                result = (T)Enum.Parse(typeof(T), fi.Name);
                return true;
            }

            result = default;
            return false;
        }
    }
}