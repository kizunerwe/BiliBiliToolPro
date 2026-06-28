using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Ray.BiliBiliTool.Agent;
using Ray.BiliBiliTool.Agent.BiliBiliAgent.Dtos;
using Ray.BiliBiliTool.Agent.BiliBiliAgent.Dtos.Mall;
using Ray.BiliBiliTool.Agent.BiliBiliAgent.Dtos.ViewMall;
using Ray.BiliBiliTool.Agent.BiliBiliAgent.Dtos.VipTask;
using Ray.BiliBiliTool.Agent.BiliBiliAgent.Dtos.VipTask.ThreeDaysSign;
using Ray.BiliBiliTool.Agent.BiliBiliAgent.Interfaces;
using Ray.BiliBiliTool.Agent.BiliBiliAgent.Utils;
using Ray.BiliBiliTool.Config.Options;
using Ray.BiliBiliTool.DomainService.Dtos;
using Ray.BiliBiliTool.DomainService.Interfaces;

namespace Ray.BiliBiliTool.DomainService;

public class VipBigPointDomainService(
    ILogger<VipBigPointDomainService> logger,
    IOptionsMonitor<VipBigPointOptions> vipBigPointOptions,
    IVipBigPointApi vipApi,
    IMallApi mallApi,
    IVipMallApi vipMallApi,
    IVideoApi videoApi,
    IAccountDomainService accountDomainService,
    IVideoDomainService videoDomainService,
    VipBigPointAccessKeyStore vipBigPointAccessKeyStore
) : IVipBigPointDomainService
{
    public async Task<VipBigPointCombine> GetCombineAsync(BiliCookie ck)
    {
        var allTasks = await mallApi.GetCombineAsync(
            new GetCombineRequest { csrf = ck.BiliJct, buvid = ck.Buvid },
            ck.ToString()
        );
        if (allTasks.Code != 0)
            throw new Exception(allTasks.ToJsonStr());
        return allTasks.Data;
    }

    public async Task VipExpressAsync(BiliCookie ck)
    {
        var re = await vipApi.GetVouchersInfoAsync(ck.ToString());
        if (re.Code == 0)
        {
            var state = re.Data.List.Find(x => x.Type == 9)?.State;

            switch (state)
            {
                case 2:
                    logger.LogInformation("大会员经验观看任务未完成");
                    logger.LogInformation("开始观看视频");
                    DailyTaskInfo dailyTaskInfo = await accountDomainService.GetDailyTaskStatus(ck);
                    await videoDomainService.WatchAndShareVideo(dailyTaskInfo, ck);
                    goto case 0;

                case 1:
                    logger.LogInformation("大会员经验已兑换");
                    break;

                case 0:
                    logger.LogInformation("大会员经验未兑换");
                    var response = await vipApi.ObtainVipExperienceAsync(
                        new VipExperienceRequest { csrf = ck.BiliJct },
                        ck.ToString()
                    );
                    if (response.Code != 0)
                    {
                        logger.LogInformation(
                            "大会员经验领取失败，错误信息：{message}",
                            response.Message
                        );
                        break;
                    }

                    logger.LogInformation("领取成功，经验+10 √");
                    var combine = await GetCombineAsync(ck);
                    combine.LogPointInfo(logger);
                    break;

                default:
                    logger.LogDebug("大会员经验领取失败，未知错误");
                    break;
            }
        }
    }

    public async Task SignAsync(BiliCookie ck)
    {
        var signInfo = await vipApi.GetThreeDaySignAsync(
            new ThreeDaySignRequest { csrf = ck.BiliJct },
            ck.ToString()
        );
        if (signInfo.Data.three_day_sign.signed)
        {
            logger.LogInformation("已完成，跳过");
            logger.LogInformation(signInfo.Data.ToString());
            return;
        }

        BiliApiResponse<Sign2Response> re = await mallApi.Sign2Async(
            new Sign2RequestPath(ck.BiliJct),
            new Sign2Request(),
            ck.ToString()
        );
        if (re.Code != 0)
            throw new Exception(re.ToJsonStr());

        logger.LogInformation("签到成功");
        logger.LogInformation(re.Data.ToString());

        signInfo = await vipApi.GetThreeDaySignAsync(
            new ThreeDaySignRequest { csrf = ck.BiliJct },
            ck.ToString()
        );
        signInfo.Data.LogPointInfo(logger);
    }

    public async Task ReceiveDailyMissionsAsync(VipBigPointCombine combine, BiliCookie ck)
    {
        const string moduleCode = "日常任务";

        var module = combine.Task_info.Modules.FirstOrDefault(x => x.module_title == moduleCode);
        var missionsNeedReceive = module
            ?.common_task_item.Where(x =>
                x.state == 0 && !VipBigPointTaskCatalog.IsAutomationUnsupported(x)
            )
            .ToList();
        if (missionsNeedReceive == null || missionsNeedReceive.Count == 0)
        {
            logger.LogInformation("均已领取，跳过");
            return;
        }

        if (!HasAccessKey(ck, "领取日常任务"))
        {
            return;
        }

        foreach (var targetTask in missionsNeedReceive)
        {
            logger.LogInformation("开始领取任务：{task}", targetTask.title);
            await TryReceive(targetTask.task_code, ck);
        }
    }

    public async Task ReceiveAndCompleteAsync(
        VipBigPointCombine info,
        string moduleCode,
        string taskCode,
        BiliCookie ck,
        Func<string, BiliCookie, Task<bool>> completeFunc
    )
    {
        var module = info.Task_info.Modules.FirstOrDefault(x => x.module_title == moduleCode);
        var bonusTask = module?.common_task_item.FirstOrDefault(x => x.task_code == taskCode);

        if (bonusTask == null)
        {
            logger.LogInformation("任务失效");
            return;
        }

        if (bonusTask.state == 3)
        {
            logger.LogInformation("已完成，跳过");
            return;
        }

        if (bonusTask.state == 0)
        {
            logger.LogInformation("开始领取任务");
            await TryReceive(bonusTask.task_code, ck);
        }

        logger.LogInformation("开始完成任务");
        var re = await completeFunc(taskCode, ck);

        if (re)
        {
            var combine = await GetCombineAsync(ck);
            module = combine.Task_info.Modules.FirstOrDefault(x => x.module_title == moduleCode);
            bonusTask = module?.common_task_item.FirstOrDefault(x => x.task_code == taskCode);
            var success = bonusTask is { state: 3, complete_times: >= 1 };
            logger.LogInformation("确认：{re}", success ? "成功，经验 +10" : "失败");
        }
    }

    public async Task<bool> CompleteAsync(string taskCode, BiliCookie ck)
    {
        var request = new ReceiveOrCompleteTaskRequest(taskCode);
        var re = await vipApi.CompleteAsync(request, ck.ToString());
        if (re.Code == 0)
        {
            logger.LogInformation("已完成");
            return true;
        }

        logger.LogInformation("失败：{msg}", re.ToJsonStr());
        return false;
    }

    public async Task<bool> CompleteViewAsync(string taskCode, BiliCookie ck)
    {
        var channel = taskCode switch
        {
            "animatetab" => "jp_channel",
            "filmtab" => "tv_channel",
            _ => throw new ArgumentOutOfRangeException(
                nameof(taskCode),
                $"Invalid taskCode: {taskCode}"
            ),
        };

        logger.LogInformation("开始浏览");
        await Task.Delay(10 * 1000);

        var request = new ViewRequest(channel);
        var re = await vipApi.ViewComplete(request, ck.ToString());
        if (re.Code == 0)
        {
            logger.LogInformation("浏览完成");
            return true;
        }

        logger.LogInformation("浏览失败：{msg}", re.ToJsonStr());
        return false;
    }

    public async Task<bool> CompleteViewVipMallAsync(string taskCode, BiliCookie ck)
    {
        var re = await vipMallApi.ViewVipMallAsync(
            new ViewVipMallRequest { Csrf = ck.BiliJct },
            ck.ToString()
        );
        if (re.Code != 0)
            throw new Exception(re.ToJsonStr());
        return true;
    }

    public async Task<bool> CompleteV2Async(string taskCode, BiliCookie ck)
    {
        string? accessKey = RequireAccessKey(ck, $"完成任务 {taskCode}");
        if (accessKey == null)
        {
            return false;
        }

        var request = new VipBigPointTaskAppRequest(taskCode, ck.BiliJct);
        VipBigPointAppRequestSigner.PopulateAppSign(request, accessKey);

        var re = await vipApi.CompleteV2(request, ck.ToString());
        if (re.Code == 0)
        {
            logger.LogInformation("已完成");
            return true;
        }

        logger.LogInformation("失败：{msg}", re.ToJsonStr());
        return false;
    }

    public async Task<bool> CompleteOgvWatchAsync(BiliCookie ck)
    {
        string? accessKey = RequireAccessKey(ck, "观看剧集内容");
        if (accessKey == null)
        {
            return false;
        }

        var bangumiEpisode = await GetBangumiEpisodeAsync(ck);
        if (bangumiEpisode == null)
        {
            logger.LogInformation("未找到可用的剧集，跳过");
            return false;
        }

        var startRequest = new StartOgvWatchRequest(
            bangumiEpisode.Value.EpisodeId,
            bangumiEpisode.Value.SeasonId,
            ck.BiliJct
        );
        VipBigPointAppRequestSigner.PopulateAppSign(startRequest, accessKey);

        var startResponse = await vipApi.StartOgvWatchAsync(
            startRequest,
            ck.ToString(),
            ck.Buvid,
            ck.UserId
        );
        if (startResponse.Code != 0 || startResponse.Data == null)
        {
            logger.LogInformation("开始观看失败：{msg}", startResponse.ToJsonStr());
            return false;
        }
        if (!startResponse.Data.HasValidTaskContext)
        {
            logger.LogInformation(
                "开始观看返回缺少有效 task_id/token：{msg}",
                startResponse.ToJsonStr()
            );
            return false;
        }
        if (startResponse.Data.CountdownMilliseconds > 0)
        {
            var delay = TimeSpan.FromMilliseconds(startResponse.Data.CountdownMilliseconds + 5000);
            logger.LogInformation("等待剧集观看倒计时：{seconds} 秒", (int)delay.TotalSeconds);
            await DelayAsync(delay);
        }

        var completeRequest = new CompleteOgvWatchRequest(
            startResponse.Data.task_id,
            startResponse.Data.token!,
            ck.BiliJct
        )
        {
            timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
        };
        VipBigPointAppRequestSigner.PopulateOgvTaskSigns(completeRequest);
        VipBigPointAppRequestSigner.PopulateAppSign(completeRequest, accessKey);

        var completeResponse = await vipApi.CompleteOgvWatchAsync(
            completeRequest,
            ck.ToString(),
            ck.Buvid,
            ck.UserId
        );
        if (completeResponse.Code == 0)
        {
            logger.LogInformation("已完成");
            return true;
        }

        logger.LogInformation("失败：{msg}", completeResponse.ToJsonStr());
        return false;
    }

    #region private

    private async Task TryReceive(string taskCode, BiliCookie ck)
    {
        string? accessKey = RequireAccessKey(ck, $"领取任务 {taskCode}");
        if (accessKey == null)
        {
            return;
        }

        BiliApiResponse? re = null;
        try
        {
            var request = new VipBigPointTaskAppRequest(taskCode, ck.BiliJct);
            VipBigPointAppRequestSigner.PopulateAppSign(request, accessKey);

            re = await vipApi.ReceiveV2(request, ck.ToString());
            if (re.Code == 0)
                logger.LogInformation("领取任务成功");
            else
                logger.LogInformation("领取任务失败：{msg}", re.ToJsonStr());
        }
        catch (Exception e)
        {
            logger.LogError("领取任务异常");
            logger.LogError(e.Message + re?.ToJsonStr());
        }
    }

    private async Task<(long SeasonId, long EpisodeId)?> GetBangumiEpisodeAsync(BiliCookie ck)
    {
        var options = vipBigPointOptions.CurrentValue;
        if (options.ViewBangumiList.Count == 0)
            return null;

        long randomSsid = options.ViewBangumiList[
            new Random().Next(0, options.ViewBangumiList.Count)
        ];

        var res = await GetBangumi(randomSsid, ck);
        if (res is null)
        {
            return null;
        }

        return (randomSsid, res.Value.Item2);
    }

    private async Task<(VideoInfoDto, long)?> GetBangumi(long randomSsid, BiliCookie ck)
    {
        try
        {
            if (randomSsid is 0 or long.MinValue)
                return null;
            var bangumiInfo = await videoApi.GetBangumiBySsid(randomSsid, ck.ToString());
            var availableEpisodes = bangumiInfo
                .Result.episodes.Where(x => x.status == 2 && x.ep_id > 0 && x.cid > 0)
                .ToList();
            if (availableEpisodes.Count == 0)
            {
                logger.LogWarning("番剧 ssid={ssid} 未返回可用正片，跳过", randomSsid);
                return null;
            }

            var bangumi = availableEpisodes[new Random().Next(0, availableEpisodes.Count)];
            var videoInfo = new VideoInfoDto()
            {
                Bvid = bangumi.bvid,
                Aid = bangumi.aid.ToString(),
                Cid = bangumi.cid,
                Copyright = 1,
                Duration = bangumi.duration,
                Title = bangumi.share_copy,
            };
            logger.LogInformation("本次播放的正片为：{title}", bangumi.share_copy);
            return (videoInfo, bangumi.ep_id);
        }
        catch (Exception e)
        {
            logger.LogError(e.Message);
        }

        return null;
    }

    private string? ResolveAccessKey(BiliCookie ck)
    {
        string? runtimeAccessKey = NormalizeAccessKey(vipBigPointAccessKeyStore.Get(ck.UserId));
        if (runtimeAccessKey != null)
        {
            return runtimeAccessKey;
        }

        var options = vipBigPointOptions.CurrentValue;
        if (options.AccessKeys.TryGetValue(ck.UserId, out var accountAccessKey))
        {
            string? normalizedAccountAccessKey = NormalizeAccessKey(accountAccessKey);
            if (normalizedAccountAccessKey != null)
            {
                return normalizedAccountAccessKey;
            }
        }

        return NormalizeAccessKey(options.AccessKey);
    }

    private bool HasAccessKey(BiliCookie ck, string actionName)
    {
        return RequireAccessKey(ck, actionName) != null;
    }

    private string? RequireAccessKey(BiliCookie ck, string actionName)
    {
        string? accessKey = ResolveAccessKey(ck);
        if (accessKey == null)
        {
            logger.LogWarning("{action}缺少 access_key，跳过", actionName);
        }

        return accessKey;
    }

    private static string? NormalizeAccessKey(string? accessKey)
    {
        return string.IsNullOrWhiteSpace(accessKey) ? null : accessKey.Trim();
    }

    protected virtual Task DelayAsync(TimeSpan delay)
    {
        return Task.Delay(delay);
    }

    #endregion
}
