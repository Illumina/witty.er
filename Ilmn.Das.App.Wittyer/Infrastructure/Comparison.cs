using System.Collections.Generic;
using Ilmn.Das.App.Wittyer.Utilities;
using Ilmn.Das.App.Wittyer.Vcf.Variants;
using JetBrains.Annotations;

namespace Ilmn.Das.App.Wittyer.Infrastructure
{
    public static class Comparison
    {
        internal static void Work([NotNull] TruthForest truthForest, [NotNull] IReadOnlyList<IWittyerSimpleVariant> queries)
        {
            foreach (var variant in queries)
                OverlappingUtils.DoOverlapping(truthForest.Trees, variant);
        }
    }
}