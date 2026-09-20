using Ray.BiliBiliTool.Agent.BiliBiliAgent.Dtos;
using WebApiClientCore.Attributes;

namespace Ray.BiliBiliTool.Agent.BiliBiliAgent.Interfaces;

/// <summary>
/// 用户信息接口API
/// </summary>
[Header("Referer", "https://www.bilibili.com/")]
[Header("Origin", "https://www.bilibili.com")]
[Header("Host", "api.bilibili.com")]
public interface IUserInfoApi : IBiliBiliApi
{
    /// <summary>
    /// 登录
    /// </summary>
    /// <returns></returns>
    [HttpGet("/x/web-interface/nav")]
    Task<BiliApiResponse<UserInfo>> LoginByCookie([Header("Cookie")] string ck);

    /// <summary>
    /// 获取当前会话绑定的 Web 设备指纹 Cookie。
    /// </summary>
    [HttpGet("/x/frontend/finger/spi")]
    Task<BiliApiResponse<DeviceFingerprintResponse>> GetDeviceFingerprint(
        [Header("Cookie")] string ck
    );
}
