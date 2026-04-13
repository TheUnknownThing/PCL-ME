using System.Text.Json;
using System.Text.Json.Serialization;

namespace PCL.Frontend.Spike.Serialization;

internal static class SpikeJson
{
    public static JsonSerializerOptions CreateOptions(bool writeIndented = true)
    {
        return new JsonSerializerOptions
        {
            WriteIndented = writeIndented,
            PropertyNameCaseInsensitive = true,
            Converters =
            {
                new JsonStringEnumConverter(),
                new SpikeVersionJsonConverter()
            }
        };
    }
}

internal sealed class SpikeVersionJsonConverter : JsonConverter<Version>
{
    public override Version Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetString();
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new JsonException("Version value cannot be null or empty.");
        }

        return Version.Parse(value);
    }

    public override void Write(Utf8JsonWriter writer, Version value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString());
    }
}
