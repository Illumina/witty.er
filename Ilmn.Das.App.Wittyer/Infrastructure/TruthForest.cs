using System.Collections.Concurrent;
using System.Collections.Generic;
using Ilmn.Das.App.Wittyer.Utilities;
using Ilmn.Das.App.Wittyer.Vcf.Variants;
using Ilmn.Das.Std.BioinformaticUtils.GenomicFeatures;
using Ilmn.Das.Std.VariantUtils.Vcf.Variants;

namespace Ilmn.Das.App.Wittyer.Infrastructure
{
    internal class TruthForest
    {
        private static ConcurrentDictionary<WittyerVariantType, GenomeIntervalTree<IWittyerSimpleVariant>> _trees;

        /// <summary>
        /// Gets the truth trees. Everything can be used for evaluation
        /// </summary>
        /// <value>
        /// The trees.
        /// </value>
        internal ConcurrentDictionary<WittyerVariantType, GenomeIntervalTree<IWittyerSimpleVariant>> Trees => _trees;

        /// <summary>
        /// Gets the left overs. Variants being left out from evaluations. Could be variants filtered out, single breakend etc.
        /// </summary>
        /// <value>
        /// The left overs.
        /// </value>
        internal IList<IVcfVariant> LeftOvers { get; }

        internal string SampleName { get; }

        private TruthForest(ConcurrentDictionary<WittyerVariantType, GenomeIntervalTree<IWittyerSimpleVariant>> trees,
            IList<IVcfVariant> leftOvers, string sampleName)
        {
            _trees = trees;
            LeftOvers = leftOvers;
            SampleName = sampleName;
        }

        internal static TruthForest Create(ConcurrentDictionary<WittyerVariantType, GenomeIntervalTree<IWittyerSimpleVariant>> trees,
            IList<IVcfVariant> leftOvers, string sampleName) => new TruthForest(trees, leftOvers, sampleName);

        internal static TruthForest CreateEmpty(string sampleName)
        {
            //create empty tree for each supported type. So when we do search it won't be key not found
            var dict = new ConcurrentDictionary<WittyerVariantType, GenomeIntervalTree<IWittyerSimpleVariant>>();
            foreach (var type in WittyerConstants.SupportedSvType)
            {
                dict.AddOrUpdate(type, GenomeIntervalTree<IWittyerSimpleVariant>.Create(),
                    (k, old) => GenomeIntervalTree<IWittyerSimpleVariant>.Create());
            }
            return new TruthForest(dict, new List<IVcfVariant>(), sampleName);
        }

        internal void AddVariantToTrees(IWittyerSimpleVariant variant)
        {
            var tree = _trees.GetOrAdd(variant.VariantType, GenomeIntervalTree<IWittyerSimpleVariant>.Create());
            tree.Add(variant);
        }

        internal void AddToLeftOver(IVcfVariant variant)
        {
            LeftOvers.Add(variant);
        }
    }
}
