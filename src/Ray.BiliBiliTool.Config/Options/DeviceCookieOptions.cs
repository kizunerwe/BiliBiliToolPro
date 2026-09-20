namespace Ray.BiliBiliTool.Config.Options;

/// <summary>
/// 固定设备指纹 Cookie 配置。
/// </summary>
/// <remarks>
/// B 站分享接口（share/add）的 -403"账号异常"风控与 buvid3/buvid4/b_nut 的设备档案绑定：
/// /x/frontend/finger/spi 每次调用都会生成一对全新的、无浏览历史的设备值，
/// 直接使用会被判定为"账号异常"（2026-09-15 生产容器 A/B 实验验证）。
/// 配置一对来自真实浏览器（长期使用、有正常浏览记录）的设备值，
/// 程序将固定使用该值，不再调用 finger/spi 刷新。
/// </remarks>
public class DeviceCookieOptions
{
    public const string SectionName = "DeviceCookie";

    /// <summary>
    /// 固定的 buvid3 值（从常用浏览器 Cookie 中复制）
    /// </summary>
    public string? Buvid3 { get; set; }

    /// <summary>
    /// 固定的 buvid4 值（从常用浏览器 Cookie 中复制）
    /// </summary>
    public string? Buvid4 { get; set; }

    /// <summary>
    /// 固定的 b_nut 值（从常用浏览器 Cookie 中复制）
    /// </summary>
    public string? BNut { get; set; }

    /// <summary>
    /// 是否配置了任意固定设备 Cookie 值
    /// </summary>
    public bool HasPinnedValues =>
        !string.IsNullOrWhiteSpace(Buvid3)
        || !string.IsNullOrWhiteSpace(Buvid4)
        || !string.IsNullOrWhiteSpace(BNut);
}
