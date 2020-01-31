using System.Collections.Generic;
using System.Linq;
using Ilmn.Das.App.Wittyer.Vcf.Variants.Breakend;
using JetBrains.Annotations;

namespace Ilmn.Das.App.Wittyer.Utilities
{
    internal class BreakendPairComparer : IEqualityComparer<IGeneralBnd>
    {
        internal static readonly BreakendPairComparer Default = new BreakendPairComparer();

        public bool Equals([NotNull] IGeneralBnd var1, [NotNull] IGeneralBnd var2)
        {
            // ReSharper disable once InlineOutVariableDeclaration
            string mateId;
            var hasMateId = var1.Info.TryGetValue(WittyerConstants.MateId, out mateId);
            if (hasMateId) // both have to have MateId to be matched via MateId
                return var2.Ids.Contains(mateId)
                       && var2.Info.TryGetValue(WittyerConstants.MateId, out mateId) && var1.Ids.Contains(mateId);
            return !var2.Info.ContainsKey(WittyerConstants.MateId) // no match if one has MateId and other doesn't
                   && var1.Mate.Is3Prime == var2.Is3Prime && var1.Is3Prime == var2.Mate.Is3Prime
                   && var1.Contig.Equals(var2.Mate.Contig)
                   && var1.Position.Equals(var2.Mate.Position)
                   && var2.Contig.Equals(var1.Mate.Contig)
                   && var2.Position.Equals(var1.Mate.Position);
        }

        public int GetHashCode([NotNull] IGeneralBnd variant) 
            => unchecked(variant.Contig.Name.GetHashCode() + (int)variant.Position +
                variant.Mate.Contig.Name.GetHashCode() + (int)variant.Mate.Position);
    }
}
