using System.Text.Json;
using System.Text.Json.Serialization;

namespace Ray.BiliBiliTool.Agent.BiliBiliAgent.Dtos.Live;

public sealed class PendantInfoDictionaryJsonConverter
    : JsonConverter<Dictionary<string, PendantInfo>?>
{
    public override Dictionary<string, PendantInfo>? Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options
    )
    {
        if (reader.TokenType == JsonTokenType.Null)
            return null;

        if (reader.TokenType == JsonTokenType.StartArray)
        {
            using JsonDocument arrayDocument = JsonDocument.ParseValue(ref reader);
            return [];
        }

        if (reader.TokenType != JsonTokenType.StartObject)
            throw new JsonException("pendant_info 必须是对象、空数组或 null");

        using JsonDocument objectDocument = JsonDocument.ParseValue(ref reader);
        Dictionary<string, PendantInfo> result = [];

        foreach (JsonProperty property in objectDocument.RootElement.EnumerateObject())
        {
            PendantInfo? pendant = property.Value.Deserialize<PendantInfo>(options);
            if (pendant != null)
            {
                result[property.Name] = pendant;
            }
        }

        return result;
    }

    public override void Write(
        Utf8JsonWriter writer,
        Dictionary<string, PendantInfo>? value,
        JsonSerializerOptions options
    )
    {
        writer.WriteStartObject();

        if (value != null)
        {
            foreach ((string key, PendantInfo pendant) in value)
            {
                writer.WritePropertyName(key);
                JsonSerializer.Serialize(writer, pendant, options);
            }
        }

        writer.WriteEndObject();
    }
}
