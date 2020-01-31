using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Ilmn.Das.App.Wittyer.Stats
{
    /// <summary>
    /// What is considered a single case for stats calculations: either <see cref="Event"/>
    /// for calculating stats based on whole variant calls or <see cref="Base"/> for calculating
    /// stats stats based on nucleotide base calls.
    /// </summary>
    [JsonConverter(typeof(StringEnumConverter))]
    public enum StatsType
    {
        /// <summary>
        /// Calculating stats based on whole variant calls.
        /// </summary>
        Event,
        /// <summary>
        /// Calculating stats based on nucleotide base calls.
        /// </summary>
        Base
    }
}
