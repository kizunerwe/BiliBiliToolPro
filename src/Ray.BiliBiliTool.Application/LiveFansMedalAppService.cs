using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Ray.BiliBiliTool.Agent;
using Ray.BiliBiliTool.Application.Attributes;
using Ray.BiliBiliTool.Application.Contracts;
using Ray.BiliBiliTool.Config.Options;
using Ray.BiliBiliTool.DomainService.Dtos;
using Ray.BiliBiliTool.DomainService.Interfaces;
using Ray.BiliBiliTool.Infrastructure.Cookie;

namespace Ray.BiliBiliTool.Application;

public class LiveFansMedalAppService(
    ILogger<LiveFansMedalAppService> logger,
    IOptionsMonitor<LiveFansMedalTaskOptions> liveFansMedalTaskOptions,
    ILiveDomainService liveDomainService,
    CookieStrFactory<BiliCookie> cookieStrFactory
) : BaseMultiAccountsAppService(logger, cookieStrFactory), ILiveFansMedalAppService
{
    [TaskInterceptor("直播间互动", TaskLevel.One)]
    protected override async Task DoTaskAccountAsync(
        BiliCookie ck,
        CancellationToken cancellationToken = default
    )
    {
        if (!liveFansMedalTaskOptions.CurrentValue.IsEnable)
        {
            logger.LogInformation("已配置为关闭，跳过");
            return;
        }

        var steps = new TaskStepAccumulator();
        await steps.RunAsync("发送弹幕", () => liveDomainService.SendDanmakuToFansMedalLive(ck));
        await steps.RunAsync("点赞直播间", () => liveDomainService.LikeFansMedalLive(ck));
        await steps.RunAsync(
            "直播时长挂机",
            () => liveDomainService.SendHeartBeatToFansMedalLive(ck)
        );
        steps.ThrowIfFailed("直播粉丝牌任务");
    }
}
