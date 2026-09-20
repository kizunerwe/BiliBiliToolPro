using Ray.BiliBiliTool.Agent.BiliBiliAgent.Dtos;
using WebApiClientCore.Attributes;

namespace Ray.BiliBiliTool.Agent.BiliBiliAgent.Interfaces;

/// <summary>
/// gaia 设备指纹网关
/// </summary>
[Header("Referer", "https://www.bilibili.com/")]
[Header("Origin", "https://www.bilibili.com")]
[Header("Host", "api.bilibili.com")]
public interface IGaiaApi : IBiliBiliApi
{
    /// <summary>
    /// 上报 Web 设备指纹（ExClimbWuzhi）。
    /// 程序生成的 buvid3 必须经此接口上报一次，在服务端建立设备档案，
    /// 否则分享等接口会被 -403"账号异常"风控拒绝。
    /// 2026-09-15 生产容器实验验证：生成 buvid3 + 本接口上报后 share/add 即放行。
    /// </summary>
    [HttpPost("/x/internal/gaia-gateway/ExClimbWuzhi")]
    Task<BiliApiResponse<object>> ReportDeviceFingerprint(
        [Header("Cookie")] string ck,
        [JsonContent] GaiaReportRequest request
    );
}
