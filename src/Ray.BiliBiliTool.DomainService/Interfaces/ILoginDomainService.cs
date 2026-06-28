using Ray.BiliBiliTool.Agent;
using Ray.BiliBiliTool.DomainService.Dtos;

namespace Ray.BiliBiliTool.DomainService.Interfaces;

/// <summary>
/// 账户
/// </summary>
public interface ILoginDomainService : IDomainService
{
    /// <summary>
    /// 扫描二维码登录
    /// </summary>
    /// <returns></returns>
    Task<BiliCookie> LoginByQrCodeAsync(CancellationToken cancellationToken);

    /// <summary>
    /// 通过 B 站 App/TV 二维码登录，一次性获得 Cookie 与 access_key
    /// </summary>
    Task<PassportTvLoginResult> LoginByTvQrCodeAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Set Cookie
    /// </summary>
    /// <param name="cookie"></param>
    /// <returns></returns>
    Task<BiliCookie> SetCookieAsync(BiliCookie cookie, CancellationToken cancellationToken);

    /// <summary>
    /// 通过 B 站 App 扫描 TV 登录二维码，补全 access_key
    /// </summary>
    Task<string?> TryGetAccessKeyByTvQrCodeAsync(CancellationToken cancellationToken);

    /// <summary>
    /// 持久化Cookie到配置文件
    /// </summary>
    /// <returns></returns>
    Task SaveCookieToJsonFileAsync(BiliCookie ckInfo, CancellationToken cancellationToken);

    /// <summary>
    /// 持久化 access_key 到本地配置文件
    /// </summary>
    Task SaveAccessKeyToJsonFileAsync(
        string userId,
        string accessKey,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// 持久化 access_key 到青龙环境变量
    /// </summary>
    Task SaveAccessKeyToQingLongAsync(
        string userId,
        string accessKey,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// 持久化Cookie到青龙环境变量
    /// </summary>
    /// <param name="ckInfo"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<bool> SaveCookieToQinLongAsync(BiliCookie ckInfo, CancellationToken cancellationToken);
}
