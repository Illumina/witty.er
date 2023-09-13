using System;
using Newtonsoft.Json;

namespace Ilmn.Das.App.Wittyer.Json.JsonConverters
{
    /// <inheritdoc />
    /// <summary>
    /// Json converter that writes an object as a string by calling extension method ToString() on it.
    /// </summary>
    public class ObjectConverter : JsonConverter
    {
        /// <inheritdoc />
        public override bool CanConvert(Type objectType) => true;

        /// <inheritdoc />
        public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
            => writer.WriteValue(value?.ToString() ?? string.Empty);

        /// <inheritdoc />
        public override object ReadJson(JsonReader reader, Type objectType, object existingValue,
            JsonSerializer serializer)
            => throw new NotSupportedException();

        /// <inheritdoc />
        public override bool CanRead => false;
    }
}