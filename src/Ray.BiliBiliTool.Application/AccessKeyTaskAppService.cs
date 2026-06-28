using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Ray.BiliBiliTool.Agent;
using Ray.BiliBiliTool.Application.Attributes;
using Ray.BiliBiliTool.Application.Contracts;
using Ray.BiliBiliTool.DomainService.Interfaces;
using Ray.BiliBiliTool.Infrastructure.Cookie;
using Ray.BiliBiliTool.Infrastructure.Enums;

namespace Ray.BiliBiliTool.Application;

public class AccessKeyTaskAppService(
    IConfiguration configuration,
    ILogger<AccessKeyTaskAppService> logger,
    ILoginDomainService loginDomainService,
    IAccountDomainService accountDomainService,
    CookieStrFactory<BiliCookie> cookieStrFactory
) : AppService, IAccessKeyTaskAppService
{
    [TaskInterceptor("补全 App access_key", TaskLevel.One)]
    public override async Task DoTaskAsync(CancellationToken cancellationToken = default)
    {
        if (cookieStrFactory.Count != 1)
        {
            logger.LogWarning("当前需隔离到单账号环境后再执行 AccessKey 任务");
            logger.LogWarning("当前账号个数：{count}", cookieStrFactory.Count);
            return;
        }

        var ck = cookieStrFactory.GetCookie(0);
        await accountDomainService.LoginByCookie(ck);

        string? accessKey = await loginDomainService.TryGetAccessKeyByTvQrCodeAsync(
            cancellationToken
        );
        if (string.IsNullOrWhiteSpace(accessKey))
        {
            logger.LogWarning("未获取到 access_key");
            return;
        }

        var platformType = configuration.GetSection("PlatformType").Get<PlatformType>();
        if (platformType == PlatformType.QingLong)
        {
            await loginDomainService.SaveAccessKeyToQingLongAsync(
                ck.UserId,
                accessKey,
                cancellationToken
            );
            return;
        }

        await loginDomainService.SaveAccessKeyToJsonFileAsync(
            ck.UserId,
            accessKey,
            cancellationToken
        );
    }
}
