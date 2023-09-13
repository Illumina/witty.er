using System.Collections.Generic;
using System.IO;
using Ilmn.Das.App.Wittyer.Vcf.Variants;

namespace Ilmn.Das.App.Wittyer.Input
{
    public interface IWittyerSettings
    {
        /// <summary>
        /// Parent directory where all output files go to
        /// </summary>
        /// <value>
        /// The output directory.
        /// </value>
        DirectoryInfo OutputDirectory { get; }

        /// <summary>
        /// Gets the truth VCF.
        /// </summary>
        /// <value>
        /// The truth VCF.
        /// </value>
        FileInfo TruthVcf { get; }

        /// <summary>
        /// Gets the query VCF.
        /// </summary>
        /// <value>
        /// The query VCF.
        /// </value>
        FileInfo QueryVcf { get; }

        /// <summary>
        /// Gets the mapping from truth sample name to query sample name.
        /// <c>Note:</c> If empty, the first sample columns of each vcf will be used for comparison.
        /// </summary>
        /// <value>
        /// The sample pairs.
        /// </value>
        IReadOnlyCollection<ISamplePair> SamplePairs { get; }

        /// <summary>
        /// Gets the mode.
        /// </summary>
        /// <value>
        /// The mode.
        /// </value>
        EvaluationMode Mode { get; }

        /// <summary>
        /// Gets the input specs.
        /// </summary>
        /// <value>
        /// The input specs.
        /// </value>
        IReadOnlyDictionary<WittyerType, InputSpec> InputSpecs { get; }

        /// <summary>
        /// The similarity threshold that INS need to pass before considered a match.
        /// </summary>
        double SimilarityThreshold { get; }
        
        /// <summary>
        /// The max number of matches a query entry can participate in.  A zero means infinite.
        /// Null is special in that it also affects genotype matching and the number depends on the ploidy. 
        /// </summary>
        byte? MaxMatches { get; }
    }
}
