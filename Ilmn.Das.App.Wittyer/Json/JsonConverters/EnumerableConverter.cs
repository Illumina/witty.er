using System;
using System.Collections;
using System.Linq;
using JetBrains.Annotations;
using Newtonsoft.Json;

namespace Ilmn.Das.App.Wittyer.Json.JsonConverters
{
    /// <inheritdoc />
    /// <summary>
    /// Json converter that writes an <see cref="IEnumerable"/> as a string of example format "object1.ToString(),object2.ToString()".
    /// </summary>
    public class EnumerableConverter : JsonConverter<IEnumerable>
    {
        public override IEnumerable ReadJson(JsonReader reader, Type objectType, IEnumerable existingValue,
            bool hasExistingValue, JsonSerializer serializer) 
            => throw new NotSupportedException();

        public override void WriteJson([NotNull] JsonWriter writer, [NotNull] IEnumerable value, JsonSerializer serializer) 
            => writer.WriteValue(string.Join(",", value.Cast<object>()));

        public override bool CanRead => false;
    }
}