using Ilmn.Das.App.Wittyer.Vcf.Variants;
using Ilmn.Das.Std.VariantUtils.Vcf.Headers;

namespace Ilmn.Das.App.Wittyer.Vcf.Readers;

using Core.Tries;
using Std.AppUtils.Files.FileReader;
using Std.BioinformaticUtils.Genomes;

public interface IVcfReader : 
    IFileReader<ITry<IVcfVariant>>
{
    IVcfHeader Header { get; }

    IGenomeAssembly? ReferenceGenome { get; }

    VcfVariantParserSettings ParserSettings { get; }
}