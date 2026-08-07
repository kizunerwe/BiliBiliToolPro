using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Ray.BiliBiliTool.Agent;
using Ray.BiliBiliTool.Agent.BiliBiliAgent.Dtos;
using Ray.BiliBiliTool.Application.Attributes;
using Ray.BiliBiliTool.Application.Contracts;
using Ray.BiliBiliTool.Config.Options;
using Ray.BiliBiliTool.DomainService.Dtos;
using Ray.BiliBiliTool.DomainService.Interfaces;
using Ray.BiliBiliTool.Infrastructure.Cookie;
using Ray.BiliBiliTool.Infrastructure.Enums;

namespace Ray.BiliBiliTool.Application;

public class DailyTaskAppService(
    ILogger<DailyTaskAppService> logger,
    IAccountDomainService accountDomainService,
    IVideoDomainService videoDomainService,
    IArticleDomainService articleDomainService,
    IDonateCoinDomainService donateCoinDomainService,
    IVipPrivilegeDomainService vipPrivilegeDomainService,
    IOptionsMonitor<DailyTaskOptions> dailyTaskOptions,
    ILoginDomainService loginDomainService,
    IConfiguration configuration,
    CookieStrFactory<BiliCookie> cookieStrFactory
) : BaseMultiAccountsAppService(logger, cookieStrFactory), IDailyTaskAppService
{
    private readonly DailyTaskOptions _dailyTaskOptions = dailyTaskOptions.CurrentValue;

    [TaskInterceptor("每日任务", TaskLevel.One)]
    protected override async Task DoTaskAccountAsync(
        BiliCookie ck,
        CancellationToken cancellationToken = default
    )
    {
        if (!_dailyTaskOptions.IsEnable)
        {
            logger.LogInformation("已配置为关闭，跳过");
            return;
        }

        await SetCookiesAsync(ck, cancellationToken);

        UserInfo userInfo = await accountDomainService.LoginByCookie(ck);
        var steps = new TaskStepAccumulator();

        var status = await steps.RunValueAsync(
            "获取每日任务状态",
            () => accountDomainService.GetDailyTaskStatus(ck)
        );
        if (status.Succeeded && status.Value is not null)
        {
            await steps.RunAsync(
                "观看、分享视频",
                () => videoDomainService.WatchAndShareVideo(status.Value, ck)
            );
        }
        else
        {
            steps.Add(
                "观看、分享视频",
                TaskStepResult.Fail("每日任务状态查询失败，无法执行观看分享")
            );
        }

        if (_dailyTaskOptions.SaveCoinsWhenLv6 && userInfo.Level_info?.Current_level >= 6)
        {
            steps.Add("投币", TaskStepResult.Skip("当前账号已达到 LV6"));
        }
        else if (_dailyTaskOptions.IsDonateCoinForArticle)
        {
            logger.LogInformation("专栏投币已开启");
            var articleResult = await steps.RunResultAsync(
                "专栏投币",
                () => articleDomainService.AddCoinForArticles(ck)
            );
            if (articleResult.Status != TaskStepStatus.Succeeded)
            {
                await steps.RunAsync(
                    "视频投币",
                    () => donateCoinDomainService.AddCoinsForVideos(ck)
                );
            }
        }
        else
        {
            await steps.RunAsync("视频投币", () => donateCoinDomainService.AddCoinsForVideos(ck));
        }

        var vipResult = await steps.RunResultAsync(
            "领取大会员福利",
            () => vipPrivilegeDomainService.ReceiveVipPrivilege(userInfo, ck)
        );
        if (vipResult.Status == TaskStepStatus.Succeeded)
        {
            try
            {
                await accountDomainService.LoginByCookie(ck);
            }
            catch (Exception ex)
            {
                logger.LogError("领取福利成功，但之后刷新用户信息时异常，信息：{msg}", ex.Message);
            }
        }

        steps.ThrowIfFailed("每日任务");
    }

    [TaskInterceptor("Set Cookie")]
    private async Task SetCookiesAsync(BiliCookie biliCookie, CancellationToken cancellationToken)
    {
        //判断cookie是否完整
        if (!string.IsNullOrWhiteSpace(biliCookie.Buvid))
        {
            logger.LogInformation("Cookie完整，不需要Set Cookie");
            return;
        }

        //Set
        logger.LogInformation("开始Set Cookie");
        var ck = await loginDomainService.SetCookieAsync(biliCookie, cancellationToken);

        //持久化
        logger.LogInformation("持久化Cookie");
        await SaveCookieAsync(ck, cancellationToken);
    }

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
}
