using System.Text.Json.Serialization;

namespace Ray.BiliBiliTool.Agent.BiliBiliAgent.Dtos.Favorite;

public sealed class FavoriteFolderDto
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("title")]
    public required string Title { get; set; }

    [JsonPropertyName("media_count")]
    public int MediaCount { get; set; }

    [JsonPropertyName("attr")]
    public int Attr { get; set; }
}
