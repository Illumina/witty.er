using Ilmn.Das.App.Wittyer.Vcf.Samples;

namespace Ilmn.Das.App.Wittyer.Vcf.Variants;

using Core.Tries;
using Std.VariantUtils.SimpleVariants;
using System;
using System.Collections.Generic;

public interface IVcfVariant : 
    ISimpleVariant,
    IEquatable<IVcfVariant>
{
    IReadOnlyList<string> Ids { get; }

    ITry<double> Quality { get; }

    IReadOnlyList<string> Filters { get; }

    IReadOnlyDictionary<string, string> Info { get; }

    SampleDictionaries Samples { get; }
}