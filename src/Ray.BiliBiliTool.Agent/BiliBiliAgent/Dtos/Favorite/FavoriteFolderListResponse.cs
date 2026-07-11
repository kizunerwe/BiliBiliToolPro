using System.Text.Json.Serialization;

namespace Ray.BiliBiliTool.Agent.BiliBiliAgent.Dtos.Favorite;

public sealed class FavoriteFolderListResponse
{
    [JsonPropertyName("list")]
    public List<FavoriteFolderDto> List { get; set; } = [];
}
