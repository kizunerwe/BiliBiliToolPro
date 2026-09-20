using System.Text.Json.Serialization;

namespace Ray.BiliBiliTool.Agent.BiliBiliAgent.Dtos;

/// <summary>
/// ExClimbWuzhi 设备指纹上报请求体
/// </summary>
public class GaiaReportRequest
{
    /// <summary>
    /// 设备指纹 payload（JSON 字符串，字段名为 B 站混淆键）
    /// </summary>
    [JsonPropertyName("payload")]
    public string Payload { get; set; } = string.Empty;
}
