using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace Ilmn.Das.App.Wittyer.Json.JsonConverters
{
    /// <inheritdoc />
    /// <summary>
    /// Json converter that writes an <see cref="IEnumerable"/> of (<see cref="uint"/> size, <see cref="bool"/> skip) tuples as a string of
    /// example format "!0,10,!50,100", where sizes are preceded by an '!' if skip is true.
    /// </summary>
    public class BinsConverter : JsonConverter<IEnumerable<(uint size, bool skip)>>
    {
        /// <inheritdoc />
        public override IEnumerable<(uint size, bool skip)> ReadJson(JsonReader reader, Type objectType, IEnumerable<(uint size, bool skip)> existingValue, bool hasExistingValue,
            JsonSerializer serializer)
            => throw new NotSupportedException();

        /// <inheritdoc />
        public override void WriteJson(JsonWriter writer, IEnumerable<(uint size, bool skip)> value, JsonSerializer serializer)
            => writer.WriteValue(string.Join(",", value.Select(SizeSkipTupleToString)));

        /// <inheritdoc />
        public override bool CanRead => false;

        private static string SizeSkipTupleToString((uint size, bool skip) sizeSkipTuple)
            => sizeSkipTuple.skip ? $"!{sizeSkipTuple.size}" : sizeSkipTuple.size.ToString();
    }
}
