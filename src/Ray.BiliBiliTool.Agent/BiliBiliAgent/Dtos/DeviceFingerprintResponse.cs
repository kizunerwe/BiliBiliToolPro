using System.Text.Json.Serialization;

namespace Ray.BiliBiliTool.Agent.BiliBiliAgent.Dtos;

public sealed class DeviceFingerprintResponse
{
    [JsonPropertyName("b_3")]
    public string? B_3 { get; set; }

    [JsonPropertyName("b_4")]
    public string? B_4 { get; set; }
}
