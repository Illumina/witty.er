using Ilmn.Das.Std.AppUtils.Comparers;
using Ilmn.Das.Std.VariantUtils.SimpleVariants;
using Ilmn.Das.Std.VariantUtils.Vcf;
using SampleBuilder = Ilmn.Das.App.Wittyer.Vcf.Samples.SampleBuilder;
using SampleDictionaries = Ilmn.Das.App.Wittyer.Vcf.Samples.SampleDictionaries;

namespace Ilmn.Das.App.Wittyer.Vcf.Variants;

using Core.Tries;
using Std.AppUtils.Collections;
using Std.AppUtils.Misc;
using Std.BioinformaticUtils.Contigs;
using Std.BioinformaticUtils.Nucleotides;
using JetBrains.Annotations;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Runtime.CompilerServices;
using Utilities;
using Readers;

public class VcfVariant : 
    IVcfVariant
  {
    private string? _vcfLine;

    internal VcfVariant(
      IContigInfo contig,
      uint position,
      IReadOnlyList<string> ids,
      DnaString refSequence,
      IReadOnlyList<string> alts,
      ITry<double> quality,
      IReadOnlyList<string> filters,
      IReadOnlyDictionary<string, string> info,
      SampleDictionaries samples,
      string? vcfLine = null)
    {
      Contig = contig;
      Position = position;
      Ref = refSequence;
      Alts = alts;
      Ids = ids;
      Quality = quality;
      Filters = filters;
      Info = info;
      Samples = samples;
      _vcfLine = vcfLine;
    }

    public IContigInfo Contig { get; }

    public uint Position { get; }

    public DnaString Ref { get; }

    public IReadOnlyList<string> Alts { get; }

    public IReadOnlyList<string> Ids { get; }

    public ITry<double> Quality { get; }

    public IReadOnlyList<string> Filters { get; }

    public IReadOnlyDictionary<string, string> Info { get; }

    public SampleDictionaries Samples { get; }

    public bool Equals(IVcfVariant? other) => VcfVariantEqualityComparer.Instance.Equals(this, other);

    public bool Equals(ISimpleVariant? other) => SimpleVariantEqualityComparer.Instance.Equals(this, other);

    [Pure]
    public static ITry<IVcfVariant> TryParse(string line, VcfVariantParserSettings settings) => VcfVariantParser.TryParse(line, settings);

    public override int GetHashCode() => VcfVariantEqualityComparer.Instance.GetHashCode(this);

    public override string ToString() => _vcfLine ??= GenerateVcfLine();

    [MethodImpl(MethodImplOptions.Synchronized)]
    private string GenerateVcfLine() => this.ToStrings().StringJoin<string>("\t");

    public static Builder CreateBuilder(IContigInfo contig, uint position, DnaString refSequence) =>
      new(contig, position, refSequence);

    public class Builder : IBuilder<IVcfVariant>
    {
      private IReadOnlyList<string> _alts = ImmutableList<string>.Empty;
      private IContigInfo _contig;
      private IReadOnlyList<string> _filters = ImmutableList<string>.Empty;
      private IReadOnlyList<string> _ids = ImmutableList<string>.Empty;
      private IImmutableDictionary<string, string> _info = ImmutableDictionary<string, string>.Empty;
      private uint _position;
      private ITry<double> _quality = VcfConstants.MissingValueQuality;
      private DnaString _ref;
      private SampleDictionaries _samples = SampleDictionaries.Empty;

      internal Builder(IContigInfo contig, uint position, DnaString reference)
      {
        _contig = contig;
        _position = position;
        _ref = reference;
      }

      [Pure]
      public IVcfVariant Build() => new VcfVariant(_contig, _position, _ids, _ref, _alts, _quality, _filters, _info, _samples);

      [Pure]
      public Builder SetContig(IContigInfo contig)
      {
        _contig = contig;
        return this;
      }

      [Pure]
      public Builder SetContig(string contigName)
      {
        _contig = ContigInfo.Create(contigName);
        return this;
      }

      [Pure]
      public Builder SetPosition(uint position)
      {
        _position = position;
        return this;
      }

      [Pure]
      public Builder SetIds(params string[]? ids) => SetIds(ids?.ToImmutableList() ?? ImmutableList<string>.Empty);

      [Pure]
      public Builder SetIds(IReadOnlyList<string> ids)
      {
        _ids = HandleVcfMissingValue(ids);
        return this;
      }

      [Pure]
      public Builder SetRef(DnaString reference)
      {
        _ref = reference;
        return this;
      }

      [Pure]
      public Builder SetAlts(params string[]? alts) => SetAlts(alts?.ToImmutableList() ?? ImmutableList<string>.Empty);

      [Pure]
      public Builder SetAlts(IReadOnlyList<string> alts)
      {
        _alts = HandleVcfMissingValue(alts);
        return this;
      }

      [Pure]
      public Builder SetQuality(double? quality) => SetQuality(!quality.HasValue ? VcfConstants.MissingValueQuality : TryFactory.Success(quality.Value));

      [Pure]
      public Builder SetQuality(ITry<double> quality)
      {
        _quality = quality is ISuccess<double> ? quality : VcfConstants.MissingValueQuality;
        return this;
      }

      [Pure]
      public Builder SetFilters(params string[]? filters)
      {
        if (filters == null || filters.Length == 0)
          return SetFilters(ImmutableList<string>.Empty);
        if (filters.Length > 1 || !filters[0].Equals("PASS"))
          return SetFilters(filters.ToImmutableList());
        _filters = WittyerConstants.PassFilterList;
        return this;
      }

      [Pure]
      public Builder SetFilters(IReadOnlyList<string> filters)
      {
        if (filters.Count == 1 && filters[0].Equals("PASS"))
          filters = WittyerConstants.PassFilterList;
        else if (filters.Any((Func<string, bool>) (filter => filter.Equals("PASS"))))
          throw new ArgumentException("Tried to SetFilters to with a list that has PASS and more!", nameof (filters));
        _filters = HandleVcfMissingValue(filters);
        return this;
      }

      [Pure]
      public Builder SetInfo(IReadOnlyDictionary<string, string> info)
      {
        if (info is not IImmutableDictionary<string, string> immutableDictionary)
          immutableDictionary = info.ToImmutableDictionary();
        _info = immutableDictionary;
        return this;
      }

      [Pure]
      public Builder SetInfo(
        Func<ImmutableDictionaryBuilder<string, string>, IImmutableDictionary<string, string>> buildMethod)
      {
        if (_info is not ImmutableDictionary<string, string> immutableDictionary)
          immutableDictionary = _info.ToImmutableDictionary();
        var source = immutableDictionary;
        return SetInfo(buildMethod(source.ToImmutableDictionaryBuilder()));
      }

      [Pure]
      public Builder SetSamples(SampleDictionaries samples)
      {
        _samples = samples;
        return this;
      }

      [Pure]
      public Builder SetSamples(Func<SampleBuilder, SampleDictionaries> sampleCreationFunc)
        => SetSamples(sampleCreationFunc(_samples.ToBuilder()));

      [Pure]
      private static IReadOnlyList<string> HandleVcfMissingValue(IReadOnlyList<string> strings) => strings.Count != 1 || !(strings[0] == ".") ? strings : ImmutableList<string>.Empty;
    }
  }