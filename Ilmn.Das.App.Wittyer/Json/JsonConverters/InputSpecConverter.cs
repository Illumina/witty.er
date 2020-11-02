using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Ilmn.Das.App.Wittyer.Input;
using Ilmn.Das.App.Wittyer.Vcf.Variants;
using Ilmn.Das.Std.AppUtils.Collections;
using JetBrains.Annotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using static Ilmn.Das.App.Wittyer.Input.WittyerSettings.Parser.WittyerParameters;

namespace Ilmn.Das.App.Wittyer.Json.JsonConverters
{
    /// <inheritdoc />
    /// <summary>
    /// Json converter that reads an <see cref="InputSpec"/> assignable object as the most derived type possible.
    /// </summary>
    public class InputSpecConverter : JsonConverter<InputSpec>
    {
        private readonly ISet<WittyerType> _typeSet = new HashSet<WittyerType>();

        /// <summary>
        /// Initializes a new instance of the <see cref="InputSpecConverter"/> class.
        ///  </summary>
        [Pure]
        [NotNull]
        public static JsonConverter<InputSpec> Create() 
            // can never share instances of JsonConverters.
            => new InputSpecConverter();
        private InputSpecConverter()
        {
        }

        private static readonly ImmutableHashSet<string> TraFieldNames
            = ImmutableHashSet.Create(WittyerSettings.VariantTypeName, BpDistanceName, IncludedFiltersName,
                ExcludedFiltersName, IncludeBedName);

        private static readonly ImmutableHashSet<string> InsFieldNames 
            = TraFieldNames.Add(WittyerSettings.BinSizesName);

        private static readonly ImmutableHashSet<string> AllFieldNames 
            = InsFieldNames.Add(WittyerSettings.PercentDistanceName);

        private static readonly JsonLoadSettings JsonLoadSettings 
            = new JsonLoadSettings {DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error};

        /// <inheritdoc />
        public override InputSpec ReadJson(JsonReader reader, Type objectType, InputSpec existingValue, 
            bool hasExistingValue, JsonSerializer serializer)
        {
            var jo = JObject.Load(reader, JsonLoadSettings);
            // Error handling
            VerifySettings();

            return jo.ToObject<InputSpec>();

            void VerifySettings()
            {
                var fieldNamesList = jo.Properties().Select(x => x.Name).ToList();
                var fieldNames = Enumerable.ToHashSet(fieldNamesList, StringComparer.OrdinalIgnoreCase);

                // Check all field names are unique.
                if (fieldNames.Count < fieldNamesList.Count)
                    throw new JsonSerializationException("Setting contains duplicate field names in config file.");

                // Check that 'VariantType' field exists.
                if (!fieldNames.Contains(WittyerSettings.VariantTypeName))
                    throw new JsonSerializationException($"Setting missing {WittyerSettings.VariantTypeName} field.");

                var variantType = jo.GetValue(WittyerSettings.VariantTypeName, StringComparison.Ordinal).Value<string>();

                // Make sure that variant type is recognized.
                if (!WittyerType.TryParse(variantType, out var variantTypeEnum))
                    throw new JsonSerializationException($"Unknown variant type '{variantType}' in the config file.");

                // Check that each field name is recognized.
                var unexpectedFields = fieldNames.Except(AllFieldNames, StringComparer.Ordinal).StringJoin(", ");
                if (unexpectedFields.Length > 0)
                    throw new JsonSerializationException($"Unrecognized field names in config file: {unexpectedFields}.");

                // Make sure the variant type is unique.
                if (!_typeSet.Add(variantTypeEnum))
                    throw new JsonSerializationException($"Duplicate variant type '{variantType}' in the config file.");
                
                var expectedFieldNames = variantTypeEnum.HasBins
                    ? variantTypeEnum.HasLengths ? AllFieldNames : InsFieldNames
                    : TraFieldNames;

                var missingFields = expectedFieldNames.Except(fieldNames);
                if (missingFields.Count > 0)
                    throw new JsonSerializationException(
                        $"Setting for variant type '{variantType}' did not contain required fields: {string.Join(", ", missingFields)}.");

                unexpectedFields = fieldNames.Except(expectedFieldNames, StringComparer.Ordinal).StringJoin(", ");
                if (unexpectedFields.Length > 0)
                    // Print a warning if unexpectedFields
                    Console.WriteLine($"Warning: {variantTypeEnum} type shouldn't " +
                                      "contain the following fields in the config file: "
                                      + unexpectedFields);
            }
        }

        /// <inheritdoc />
        public override void WriteJson(JsonWriter writer, InputSpec value, JsonSerializer serializer) 
            => throw new NotSupportedException();
    }
}