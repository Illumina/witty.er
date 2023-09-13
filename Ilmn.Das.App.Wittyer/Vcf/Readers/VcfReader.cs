using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Ilmn.Das.App.Wittyer.Vcf.Variants;
using Ilmn.Das.Core.BgZip;
using Ilmn.Das.Core.Tries;
using Ilmn.Das.Core.Tries.Extensions;
using Ilmn.Das.Std.BioinformaticUtils.Genomes;
using Ilmn.Das.Std.VariantUtils.Vcf.Headers;
using JetBrains.Annotations;

namespace Ilmn.Das.App.Wittyer.Vcf.Readers;

public class VcfReader : IVcfReader
  {
    private VcfReader(
      FileInfo fileSource,
      IVcfHeader header,
      VcfVariantParserSettings parserSettings)
    {
      FileSource = fileSource;
      Header = header;
      ParserSettings = parserSettings;
    }

    public VcfVariantParserSettings ParserSettings { get; }

    public FileInfo FileSource { get; }

    public IVcfHeader Header { get; }

    public IGenomeAssembly ReferenceGenome => ParserSettings.ReferenceGenome;

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public IEnumerator<ITry<IVcfVariant>> GetEnumerator() => GetAllItems().GetEnumerator();

    public IEnumerable<ITry<IVcfVariant>> GetAllItems() => ReadAllLines().SkipWhile(VcfHeaderUtils.IsHeaderLine).Select((Func<string, ITry<IVcfVariant>>) (line => VcfVariantParser.TryParse(line, ParserSettings)));

    public IEnumerable<string> ReadAllLines() => FileSource.ReadLinesSafe();

    [Pure]
    public static ITry<IVcfReader> TryCreate(FileInfo vcfFile, IGenomeAssembly? referenceGenome = null) => VcfHeader.TryCreate(vcfFile).Select<IVcfHeader, IVcfReader>((Func<IVcfHeader, IVcfReader>) (vcfHeader => Create(vcfFile, vcfHeader, referenceGenome)));

    [Pure]
    private static IVcfReader Create(
      FileInfo vcfFile,
      IVcfHeader vcfHeader,
      IGenomeAssembly? referenceGenome)
    {
      return new VcfReader(vcfFile, vcfHeader, VcfVariantParserSettings.Create(vcfHeader.SampleNames, referenceGenome));
    }
  }