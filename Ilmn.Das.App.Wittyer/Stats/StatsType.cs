using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Ilmn.Das.App.Wittyer.Stats
{
    [JsonConverter(typeof(StringEnumConverter))]
    public enum StatsType
    {
        Default = 0,
        Event,
        Base
    }
}
