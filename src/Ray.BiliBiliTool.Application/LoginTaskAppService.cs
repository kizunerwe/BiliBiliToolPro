using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Ray.BiliBiliTool.Agent;
using Ray.BiliBiliTool.Application.Attributes;
using Ray.BiliBiliTool.Application.Contracts;
using Ray.BiliBiliTool.DomainService.Dtos;
using Ray.BiliBiliTool.DomainService.Interfaces;
using Ray.BiliBiliTool.Infrastructure.Enums;

namespace Ray.BiliBiliTool.Application;

public class LoginTaskAppService(
    IConfiguration configuration,
    ILogger<LoginTaskAppService> logger,
    ILoginDomainService loginDomainService
) : AppService, ILoginTaskAppService
{
    [TaskInterceptor("扫码登录", TaskLevel.One)]
    public override async Task DoTaskAsync(CancellationToken cancellationToken = default)
    {
        var loginResult = await QrCodeLoginAsync(cancellationToken);
        var cookieInfo = loginResult.Cookie;

        //set cookie
        cookieInfo = await SetCookiesAsync(cookieInfo, cancellationToken);

        //持久化cookie
        await SaveCookieAsync(cookieInfo, cancellationToken);

        //持久化 access_key
        await SaveAccessKeyAsync(cookieInfo.UserId, loginResult.AccessKey, cancellationToken);
    }

    [TaskInterceptor("获取二维码")]
    private async Task<PassportTvLoginResult> QrCodeLoginAsync(CancellationToken cancellationToken)
    {
        return await loginDomainService.LoginByTvQrCodeAsync(cancellationToken);
    }

    [TaskInterceptor("Set Cookie")]
    private async Task<BiliCookie> SetCookiesAsync(
        BiliCookie biliCookie,
        CancellationToken cancellationToken
    )
    {
        var ck = await loginDomainService.SetCookieAsync(biliCookie, cancellationToken);
        return ck;
    }

    [TaskInterceptor("持久化Cookie")]
    private async Task SaveCookieAsync(BiliCookie ckInfo, CancellationToken cancellationToken)
    {
        var platformType = configuration.GetSection("PlatformType").Get<PlatformType>();
        logger.LogInformation("当前运行平台：{platform}", platformType);

        //更新cookie到青龙env
        if (platformType == PlatformType.QingLong)
        {
            await loginDomainService.SaveCookieToQinLongAsync(ckInfo, cancellationToken);
            return;
        }

        //更新cookie到json
        await loginDomainService.SaveCookieToJsonFileAsync(ckInfo, cancellationToken);
    }

    [TaskInterceptor("持久化 access_key", rethrowWhenException: false)]
    private async Task SaveAccessKeyAsync(
        string userId,
        string accessKey,
        CancellationToken cancellationToken
    )
    {
        if (string.IsNullOrWhiteSpace(accessKey))
        {
            logger.LogWarning("本次登录未返回 access_key");
            return;
        }

        var platformType = configuration.GetSection("PlatformType").Get<PlatformType>();
        if (platformType == PlatformType.QingLong)
        {
            await loginDomainService.SaveAccessKeyToQingLongAsync(
                userId,
                accessKey,
                cancellationToken
            );
            return;
        }

        await loginDomainService.SaveAccessKeyToJsonFileAsync(userId, accessKey, cancellationToken);
    }
}
