using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using Ilmn.Das.App.Wittyer.Input;
using Ilmn.Das.App.Wittyer.Json.JsonConverters;
using Ilmn.Das.App.Wittyer.Vcf.Variants;
using Ilmn.Das.Std.AppUtils.Collections;
using Ilmn.Das.Std.XunitUtils;
using Newtonsoft.Json;
using Xunit;

namespace Ilmn.Das.App.Wittyer.Test
{
    
    public class ConfigParsingTest
    {
        private static string ConfigsFolder => Path.Combine(Environment.CurrentDirectory, "Resources", "Configs");

        private const string ObjectAsRootLinuxError =
            "Cannot deserialize the current JSON object (e.g. {\"name\":\"value\"}) into type 'System.Collections.Generic.IEnumerable`1[Ilmn.Das.App.Wittyer.Input.InputSpec]' because the type requires a JSON array (e.g. [1,2,3]) to deserialize correctly.\n" +
            "To fix this error either change the JSON to a JSON array (e.g. [1,2,3]) or change the deserialized type so that it is a normal .NET type (e.g. not a primitive type like integer, not a collection type like an array or List<T>) that can be deserialized from a JSON object. JsonObjectAttribute can also be added to the type to force it to deserialize from a JSON object.\n" +
            "Path 'variantType', line 2, position 16.";

        [Theory]
        [InlineData("Config-missing-field.json", "Setting for variant type 'Deletion' did not contain required fields: bpDistance.")]
        [InlineData("Config-duplicate-sv-type.json", "Duplicate variant type 'Deletion' in the config file.")]
        [InlineData("Config-unrecognized-sv-type.json", "Unknown variant type 'Unrecognizable' in the config file.")]
        [InlineData("Config-unrecognized-field.json", "Unrecognized field names in config file: unrecognizedField.")]
        [InlineData("Config-object-as-root.json", ObjectAsRootLinuxError)] 
        public void InvalidConfigFileThrowsException(string configFileName, string expectedExceptionMessage)
        {
            var exception = Assert.Throws<JsonSerializationException>(() =>
                JsonConvert.DeserializeObject<IEnumerable<InputSpec>>(File.ReadAllText(Path.Combine(ConfigsFolder, configFileName)),
                    InputSpecConverter.Create()));
            Assert.Equal(expectedExceptionMessage, exception.Message.Replace("\r\n", "\n"));
        }

        [Fact]
        public void ConfigFileDuplicateThrowsException()
        {
            Assert.Throws<JsonReaderException>(() =>
                JsonConvert.DeserializeObject<IEnumerable<InputSpec>>(File.ReadAllText(Path.Combine(ConfigsFolder, "Config-duplicate-field.txt")),
                    InputSpecConverter.Create()));
        }

        [Fact]
        public void UsingDefaultConfigFileProducesSameInputSpecsAsCommandLineDefaults()
        {
            var defaultConfigFilePath = Path.Combine(Environment.CurrentDirectory, "Config-default.json");
            var defaultInputSpecsCommandLine = InputSpec
                .GenerateDefaultInputSpecs(true, WittyerType.AllTypes.OrderBy(s => s.Name))
                .ToImmutableDictionary(x => x.VariantType, x => x);
            var defaultInputSpecsConfig = JsonConvert
                .DeserializeObject<IEnumerable<InputSpec>>(File.ReadAllText(defaultConfigFilePath),
                    InputSpecConverter.Create()).OrderBy(x => x.VariantType.Name)
                .ToImmutableDictionary(x => x.VariantType, x => x);
            foreach (var (key, value) in defaultInputSpecsConfig)
            {
                if (!defaultInputSpecsCommandLine.TryGetValue(key, out var value2))
                    MultiAssert.Equal(string.Empty, key.ToString());
                if (!value.Equals(value2))
                    MultiAssert.Equal(value, value2);
            }
            MultiAssert.AssertAll();
        }

        [Fact]
        public void ParsingIsCaseSensitive()
        {
            Assert.Throws<JsonSerializationException>(() => JsonConvert.DeserializeObject<IEnumerable<InputSpec>>(
                File.ReadAllText(Path.Combine(ConfigsFolder, "Config-just-deletion-weirdcase.json")),
                InputSpecConverter.Create()));
        }

        [Fact]
        public void NoChangeAfterDeserializeReserializeDeserialize()
        {
            var defaultConfigFilePath = Path.Combine(Environment.CurrentDirectory, "Config-default.json");
            var text = File.ReadAllText(defaultConfigFilePath);
            var deserializedWittyerConfigObject =
                JsonConvert.DeserializeObject<IEnumerable<InputSpec>>(text,
                    InputSpecConverter.Create());

            var reserialized = JsonConvert.SerializeObject(deserializedWittyerConfigObject, Formatting.Indented);
            var deserializeReserializeDeserializedWittyerConfigObject = 
                JsonConvert.DeserializeObject<IEnumerable<InputSpec>>(reserialized,
                    InputSpecConverter.Create());

            Assert.True(deserializedWittyerConfigObject.IsScrambledEquals(deserializeReserializeDeserializedWittyerConfigObject));
        }

        [Fact]
        public void CanSuccessfullyParseSkippedBins()
        {
            var skippedBinsConfig = Path.Combine(ConfigsFolder, "Config-just-cng-skipped-bins.json");
            var text = File.ReadAllText(skippedBinsConfig);
            var deserializedWittyerConfigObject =
                JsonConvert.DeserializeObject<IEnumerable<InputSpec>>(text,
                    InputSpecConverter.Create());
            var copyNumberGainInputSpec =
                deserializedWittyerConfigObject.First(inputSpec => inputSpec.VariantType == WittyerType.CopyNumberGain);
            var bins = copyNumberGainInputSpec.BinSizes;
            var expectedBins = ImmutableList<(uint size, bool skip)>.Empty
                .Add((1, true))
                .Add((1000, false))
                .Add((5000, true))
                .Add((10000, false))
                .Add((20000, true));
            Assert.Equal(bins, expectedBins);
        }
    }
}
