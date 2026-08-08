using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Ray.BiliBiliTool.Agent;
using Ray.BiliBiliTool.Agent.BiliBiliAgent.Dtos;
using Ray.BiliBiliTool.Agent.BiliBiliAgent.Dtos.Relation;
using Ray.BiliBiliTool.Agent.BiliBiliAgent.Interfaces;
using Ray.BiliBiliTool.Config.Options;
using Ray.BiliBiliTool.DomainService.Dtos;
using Ray.BiliBiliTool.DomainService.Interfaces;

namespace Ray.BiliBiliTool.DomainService;

/// <summary>
/// 投币
/// </summary>
public class DonateCoinDomainService(
    ILogger<DonateCoinDomainService> logger,
    IOptionsMonitor<DailyTaskOptions> dailyTaskOptions,
    IAccountApi accountApi,
    ICoinDomainService coinDomainService,
    IVideoDomainService videoDomainService,
    IFavoriteDomainService favoriteDomainService,
    IRelationApi relationApi,
    IVideoApi videoApi,
    DonateCoinSelectionStateStore selectionStateStore
) : IDonateCoinDomainService
{
    private const int ConfigUpPageSize = 30;
    private const int ConfigUpCoinStatusConcurrency = 4;
    private const int MaxSelectionAttempts = 20;
    private const int SpecialFollowingTryCount = 8;
    private const int FollowingTryCount = 12;
    private const int RankingTryCount = 20;

    private readonly DailyTaskOptions _dailyTaskOptions = dailyTaskOptions.CurrentValue;
    private readonly Dictionary<string, int> _expDic = Config.Constants.ExpDic;
    private readonly Dictionary<string, string> _donateContinueStatusDic = Config
        .Constants
        .DonateCoinCanContinueStatusDic;

    private readonly Dictionary<long, int> _upVideoCountDicCatch = new();
    private readonly ConcurrentDictionary<string, int> _videoCoinCountCache = new();
    private readonly HashSet<string> _attemptedVideoAidSet = [];
    private readonly HashSet<long> _blacklistedAidSet = [];
    private readonly Dictionary<long, DonateCoinConfigUpProgressSnapshot> _configUpProgressByUpId =
    [];

    /// <summary>
    /// 完成投币任务
    /// </summary>
    public async Task<TaskStepResult> AddCoinsForVideos(BiliCookie ck)
    {
        ResetRuntimeSelectionState();
        await LoadPersistentSelectionStateAsync(ck.UserId);

        int needCoins = await GetNeedDonateCoinNum(ck);
        int protectedCoins = _dailyTaskOptions.NumberOfProtectedCoins;
        if (needCoins <= 0)
            return TaskStepResult.Skip("没有需要投币的视频");

        decimal coinBalance = await coinDomainService.GetCoinBalance(ck);
        logger.LogInformation("【投币前余额】 : {coinBalance}", coinBalance);
        _ = int.TryParse(
            decimal.Truncate(coinBalance - protectedCoins).ToString(),
            out int unprotectedCoins
        );

        if (coinBalance <= 0)
        {
            logger.LogInformation("因硬币余额不足，今日暂不执行投币任务");
            return TaskStepResult.Skip("硬币余额不足");
        }

        if (coinBalance <= protectedCoins)
        {
            logger.LogInformation("因硬币余额达到或低于保留值，今日暂不执行投币任务");
            return TaskStepResult.Skip("硬币余额达到保留值");
        }

        if (coinBalance < needCoins)
        {
            _ = int.TryParse(decimal.Truncate(coinBalance).ToString(), out needCoins);
            logger.LogInformation("因硬币余额不足，目标投币数调整为: {needCoins}", needCoins);
        }

        if (coinBalance - needCoins <= protectedCoins && unprotectedCoins != needCoins)
        {
            needCoins = unprotectedCoins;
            logger.LogInformation(
                "因硬币余额投币后将达到或低于保留值，目标投币数调整为: {needCoins}",
                needCoins
            );
        }

        int success = 0;
        bool exhausted = false;
        bool retryableFailure = false;
        string? failureReason = null;
        logger.LogInformation("{message}", DonateCoinLogFormatter.BuildSelectionPlan());

        for (int i = 1; i <= MaxSelectionAttempts && success < needCoins; i++)
        {
            logger.LogDebug("开始尝试第{num}次", i);

            var selection = await TryGetCanDonateVideoWithSource(ck);
            if (selection.Video == null)
            {
                exhausted = true;
                retryableFailure =
                    selection.ConfigUpScanStatus == DonateCoinConfigUpScanStatus.RetryableFailure
                    || selection.RetryableFailure;
                failureReason = selection.FailureReason;
                break;
            }

            var video = selection.Video;
            logger.LogInformation(
                "{message}",
                DonateCoinLogFormatter.BuildSourceSelected(selection.Source)
            );
            logger.LogInformation("【视频】{title}", video.Title);

            if (_dailyTaskOptions.IsUpFriendlyMode)
            {
                VideoDetail detail;
                try
                {
                    detail = await videoDomainService.GetVideoDetail(video.Aid.ToString());
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "获取视频详情失败，跳过当前候选：{message}", ex.Message);
                    retryableFailure = true;
                    failureReason = ex.Message;
                    continue;
                }
                if (
                    !await videoDomainService.WatchVideoForUpFriendlyMode(
                        detail,
                        ck,
                        CancellationToken.None
                    )
                )
                {
                    logger.LogError("UP友好播放失败，跳过当前候选");
                    retryableFailure = true;
                    failureReason = "UP友好播放失败";
                    continue;
                }
            }

            if (_dailyTaskOptions.IsUpFriendlyMode && _dailyTaskOptions.SelectFavorite)
            {
                string folderName = string.IsNullOrWhiteSpace(_dailyTaskOptions.FavoriteFolderName)
                    ? "BiliBiliToolPro-UP支持"
                    : _dailyTaskOptions.FavoriteFolderName.Trim();
                long? folderId = await favoriteDomainService.GetOrCreateFolderAsync(
                    folderName,
                    ck,
                    video.Aid
                );
                if (folderId.HasValue)
                {
                    bool favoriteSucceeded = await favoriteDomainService.AddVideoAsync(
                        video.Aid,
                        folderId.Value,
                        "333.1007.tianma.3-4-10.click",
                        "333.788.0.0",
                        "",
                        ck
                    );
                    if (!favoriteSucceeded)
                    {
                        logger.LogWarning("收藏视频失败，将继续执行投币");
                    }
                }
                else
                {
                    logger.LogWarning("未能获取UP友好模式专用收藏夹，将继续执行投币");
                }
            }

            var coinResult = await AddCoinForVideoResultAsync(
                video,
                _dailyTaskOptions.SelectLike,
                ck
            );
            if (coinResult.Status == TaskStepStatus.Failed)
            {
                retryableFailure = true;
                failureReason = coinResult.Reason;
                continue;
            }

            success++;
            await MarkVideoAsBlacklistedAsync(ck.UserId, video.Aid);
            if (selection.Source == DonateCoinVideoSource.ConfigUp && selection.ConfigUpId.HasValue)
            {
                await IncreaseConfigUpRecordedCountIfNeededAsync(
                    ck.UserId,
                    selection.ConfigUpId.Value,
                    video.Aid
                );
            }
        }

        if (success == needCoins)
        {
            logger.LogInformation("视频投币任务完成");
        }
        else if (exhausted)
        {
            logger.LogInformation("可投视频不足，结束");
        }
        else
        {
            logger.LogInformation("投币尝试超过{tryCount}次，已终止", MaxSelectionAttempts);
        }

        logger.LogInformation(
            "【硬币余额】{coin}",
            (await accountApi.GetCoinBalanceAsync(ck.ToString())).Data?.Money ?? 0
        );

        if (success == needCoins)
            return TaskStepResult.Success();
        if (retryableFailure)
            return TaskStepResult.Fail(failureReason ?? "视频投币存在可重试失败");
        if (exhausted)
            return TaskStepResult.Skip("已确认没有可投视频");

        return TaskStepResult.Fail("视频投币未完成目标");
    }

    /// <summary>
    /// 尝试获取一个可以投币的视频
    /// </summary>
    /// <returns></returns>
    public async Task<UpVideoInfo?> TryGetCanDonatedVideo(BiliCookie ck)
    {
        ResetRuntimeSelectionState();
        await LoadPersistentSelectionStateAsync(ck.UserId);
        return (await TryGetCanDonateVideoWithSource(ck)).Video;
    }

    private async Task<DonateCoinSourceSearchResult> TryGetCanDonateVideoWithSource(BiliCookie ck)
    {
        var configUpResult = await TryGetCanDonateVideoByConfigUps(ck);
        if (configUpResult.Video != null)
            return configUpResult;
        if (configUpResult.ConfigUpScanStatus == DonateCoinConfigUpScanStatus.RetryableFailure)
            return configUpResult;
        LogSourceFailure(configUpResult, DonateCoinVideoSource.SpecialFollowings);

        var specialResult = await TryGetCanDonateVideoBySpecialUps(ck);
        if (specialResult.Video != null)
            return specialResult;
        LogSourceFailure(specialResult, DonateCoinVideoSource.Followings);

        var followingResult = await TryGetCanDonateVideoByFollowingUps(ck);
        if (followingResult.Video != null)
            return followingResult;
        LogSourceFailure(followingResult, DonateCoinVideoSource.Ranking);

        var rankingResult = await TryGetCanDonateVideoByRegion(RankingTryCount, ck);
        if (rankingResult.Video != null)
            return rankingResult;
        LogSourceFailure(rankingResult);

        return rankingResult;
    }

    /// <summary>
    /// 为视频投币
    /// </summary>
    /// <returns>是否投币成功</returns>
    public async Task<bool> DoAddCoinForVideo(UpVideoInfo video, bool select_like, BiliCookie ck)
    {
        var result = await AddCoinForVideoResultAsync(video, select_like, ck);
        return result.Status == TaskStepStatus.Succeeded;
    }

    private async Task<TaskStepResult> AddCoinForVideoResultAsync(
        UpVideoInfo video,
        bool select_like,
        BiliCookie ck
    )
    {
        try
        {
            var request = new AddCoinRequest(video.Aid, ck.BiliJct)
            {
                Select_like = select_like ? 1 : 0,
            };
            var referer =
                $"https://www.bilibili.com/video/{video.Bvid}/?spm_id_from=333.1007.tianma.1-1-1.click&vd_source=80c1601a7003934e7a90709c18dfcffd";
            var result = await videoApi.AddCoinForVideo(request, ck.ToString(), referer);
            if (result.Code == 0)
            {
                _expDic.TryGetValue("每日投币", out int exp);
                logger.LogInformation("投币成功，经验+{exp} √", exp);
                return TaskStepResult.Success();
            }

            if (_donateContinueStatusDic.Any(x => x.Key == result.Code.ToString()))
            {
                logger.LogError("投币失败，原因：{msg}", result.Message);
                return TaskStepResult.Fail($"视频投币被接口拒绝：{result.Message}");
            }

            string errorMsg = $"投币发生未预计异常：{result.Message}";
            logger.LogError(errorMsg);
            return TaskStepResult.Fail(errorMsg);
        }
        catch (Exception exception)
        {
            return TaskStepResult.Fail($"视频投币请求异常：{exception.Message}");
        }
    }

    #region private

    private void ResetRuntimeSelectionState()
    {
        _upVideoCountDicCatch.Clear();
        _videoCoinCountCache.Clear();
        _attemptedVideoAidSet.Clear();
        _blacklistedAidSet.Clear();
        _configUpProgressByUpId.Clear();
    }

    private async Task LoadPersistentSelectionStateAsync(string userId)
    {
        var accountState = await selectionStateStore.GetAccountStateAsync(userId);
        _blacklistedAidSet.UnionWith(accountState.BlacklistedAids);

        foreach (var pair in accountState.ConfigUpProgressByUpId)
        {
            _configUpProgressByUpId[pair.Key] = pair.Value;
        }
    }

    private async Task MarkVideoAsBlacklistedAsync(string userId, long aid)
    {
        if (!_blacklistedAidSet.Add(aid))
        {
            return;
        }

        await selectionStateStore.MarkVideoAsBlacklistedAsync(userId, aid);
    }

    private async Task SaveConfigUpProgressAsync(
        string userId,
        long upId,
        DonateCoinConfigUpProgressSnapshot progress
    )
    {
        if (
            _configUpProgressByUpId.TryGetValue(upId, out var latest)
            && latest.RecordedVideoCount > progress.RecordedVideoCount
        )
        {
            progress = progress with { RecordedVideoCount = latest.RecordedVideoCount };
        }

        _configUpProgressByUpId[upId] = progress;
        await selectionStateStore.UpdateConfigUpProgressAsync(userId, upId, progress);
    }

    /// <summary>
    /// 获取今日的目标投币数
    /// </summary>
    private async Task<int> GetNeedDonateCoinNum(BiliCookie ck)
    {
        int configCoins = _dailyTaskOptions.NumberOfCoins;
        if (configCoins <= 0)
        {
            logger.LogInformation("已配置为跳过投币任务");
            return configCoins;
        }

        int alreadyCoins = await coinDomainService.GetDonatedCoins(ck);
        int targetCoins = configCoins;

        logger.LogInformation("【今日已投】{already}枚", alreadyCoins);
        logger.LogInformation("【目标欲投】{already}枚", targetCoins);

        if (targetCoins > alreadyCoins)
        {
            int needCoins = targetCoins - alreadyCoins;
            logger.LogInformation("【还需再投】{need}枚", needCoins);
            return needCoins;
        }

        logger.LogInformation("已完成投币任务，不需要再投啦~");
        return 0;
    }

    private async Task<DonateCoinSourceSearchResult> TryGetCanDonateVideoByConfigUps(BiliCookie ck)
    {
        if (_dailyTaskOptions.SupportUpIdList.Count == 0)
        {
            return new DonateCoinSourceSearchResult(
                DonateCoinVideoSource.ConfigUp,
                FailureReason: "未配置 SupportUpIds，跳过"
            );
        }

        DonateCoinSourceSearchResult? lastResult = null;
        foreach (var upId in _dailyTaskOptions.SupportUpIdList)
        {
            if (upId == 0 || upId == long.MinValue)
            {
                continue;
            }

            if (upId.ToString() == ck.UserId)
            {
                logger.LogDebug("不能为自己投币");
                continue;
            }

            var result = await TryGetCanDonateVideoByOrderedConfigUp(upId, ck);
            if (result.Video != null)
            {
                return result;
            }

            lastResult = result;
            if (result.ConfigUpScanStatus == DonateCoinConfigUpScanStatus.RetryableFailure)
            {
                return result;
            }
        }

        return lastResult
            ?? new DonateCoinSourceSearchResult(
                DonateCoinVideoSource.ConfigUp,
                FailureReason: "已确认无可投视频",
                ConfigUpScanStatus: DonateCoinConfigUpScanStatus.ConfirmedExhausted
            );
    }

    private async Task<DonateCoinSourceSearchResult> TryGetCanDonateVideoByOrderedConfigUp(
        long upId,
        BiliCookie ck
    )
    {
        DonateCoinConfigUpProgressSnapshot? progress = null;
        try
        {
            progress = await EnsureConfigUpProgressAsync(upId, ck);
            LogConfigUpProgress(upId, progress);

            if (progress.Status == DonateCoinConfigUpScanStatus.ConfirmedExhausted)
            {
                return new DonateCoinSourceSearchResult(
                    DonateCoinVideoSource.ConfigUp,
                    FailureReason: DonateCoinLogFormatter.BuildConfigUpConfirmedExhausted(),
                    ConfigUpId: upId,
                    ConfigUpScanStatus: DonateCoinConfigUpScanStatus.ConfirmedExhausted
                );
            }

            progress = progress with
            {
                Status = DonateCoinConfigUpScanStatus.InProgress,
                FailureReason = null,
            };
            await SaveConfigUpProgressAsync(ck.UserId, upId, progress);
            LogConfigUpProgress(upId, progress);

            if (progress.NextPageNumber <= 0)
            {
                progress = await MarkConfigUpConfirmedExhaustedAsync(ck.UserId, upId, progress);
                return new DonateCoinSourceSearchResult(
                    DonateCoinVideoSource.ConfigUp,
                    FailureReason: DonateCoinLogFormatter.BuildConfigUpConfirmedExhausted(),
                    ConfigUpId: upId,
                    ConfigUpScanStatus: DonateCoinConfigUpScanStatus.ConfirmedExhausted
                );
            }

            while (progress.NextPageNumber > 0)
            {
                IReadOnlyList<UpVideoInfo> pageVideos;
                try
                {
                    pageVideos = await videoDomainService.GetVideosOfUp(
                        upId,
                        progress.NextPageNumber,
                        ConfigUpPageSize,
                        ck
                    );
                }
                catch (Exception exception)
                {
                    progress = await MarkConfigUpRetryableFailureAsync(
                        ck.UserId,
                        upId,
                        progress,
                        $"取第{progress.NextPageNumber}页视频失败：{exception.Message}"
                    );
                    return new DonateCoinSourceSearchResult(
                        DonateCoinVideoSource.ConfigUp,
                        FailureReason: progress.FailureReason,
                        ConfigUpId: upId,
                        ConfigUpScanStatus: DonateCoinConfigUpScanStatus.RetryableFailure
                    );
                }

                if (pageVideos.Count == 0)
                {
                    progress = await MarkConfigUpRetryableFailureAsync(
                        ck.UserId,
                        upId,
                        progress,
                        $"第{progress.NextPageNumber}页返回空列表"
                    );
                    return new DonateCoinSourceSearchResult(
                        DonateCoinVideoSource.ConfigUp,
                        FailureReason: progress.FailureReason,
                        ConfigUpId: upId,
                        ConfigUpScanStatus: DonateCoinConfigUpScanStatus.RetryableFailure
                    );
                }

                var orderedPageVideos = pageVideos.Reverse().ToList();
                for (
                    var offset = 0;
                    offset < orderedPageVideos.Count;
                    offset += ConfigUpCoinStatusConcurrency
                )
                {
                    var batch = orderedPageVideos
                        .Skip(offset)
                        .Take(ConfigUpCoinStatusConcurrency)
                        .ToList();
                    var coinStatuses = await GetConfigUpCoinStatusesAsync(batch, ck);

                    foreach (var video in batch)
                    {
                        var eligibility = coinStatuses.TryGetValue(video.Aid, out var multiply)
                            ? await GetVideoEligibilityAsync(
                                video.Aid,
                                ck,
                                upId,
                                multiply,
                                statusAlreadyChecked: true
                            )
                            : await GetVideoEligibilityAsync(video.Aid, ck, upId);
                        if (eligibility == DonateCoinVideoEligibility.Eligible)
                        {
                            return new DonateCoinSourceSearchResult(
                                DonateCoinVideoSource.ConfigUp,
                                video,
                                ConfigUpId: upId
                            );
                        }

                        if (eligibility == DonateCoinVideoEligibility.CheckFailed)
                        {
                            progress = await MarkConfigUpRetryableFailureAsync(
                                ck.UserId,
                                upId,
                                progress,
                                $"检查Av{video.Aid}投币状态失败，保留第{progress.NextPageNumber}页待重试"
                            );
                            return new DonateCoinSourceSearchResult(
                                DonateCoinVideoSource.ConfigUp,
                                FailureReason: progress.FailureReason,
                                ConfigUpId: upId,
                                ConfigUpScanStatus: DonateCoinConfigUpScanStatus.RetryableFailure
                            );
                        }

                        if (eligibility == DonateCoinVideoEligibility.DuplicateInCurrentRun)
                        {
                            progress = await MarkConfigUpRetryableFailureAsync(
                                ck.UserId,
                                upId,
                                progress,
                                $"Av{video.Aid}本轮已尝试但尚无持久终态，保留第{progress.NextPageNumber}页待重试"
                            );
                            return new DonateCoinSourceSearchResult(
                                DonateCoinVideoSource.ConfigUp,
                                FailureReason: progress.FailureReason,
                                ConfigUpId: upId,
                                ConfigUpScanStatus: DonateCoinConfigUpScanStatus.RetryableFailure
                            );
                        }
                    }
                }

                progress = await MoveConfigUpCursorBackwardAsync(ck.UserId, upId, progress);
            }

            progress = await MarkConfigUpConfirmedExhaustedAsync(ck.UserId, upId, progress);
        }
        catch (Exception e)
        {
            if (progress != null)
            {
                progress = await MarkConfigUpRetryableFailureAsync(
                    ck.UserId,
                    upId,
                    progress,
                    $"配置UP扫描失败：{e.Message}"
                );
            }

            return new DonateCoinSourceSearchResult(
                DonateCoinVideoSource.ConfigUp,
                FailureReason: progress?.FailureReason ?? $"取视频失败：{e.Message}",
                ConfigUpId: upId,
                ConfigUpScanStatus: DonateCoinConfigUpScanStatus.RetryableFailure
            );
        }

        return new DonateCoinSourceSearchResult(
            DonateCoinVideoSource.ConfigUp,
            FailureReason: DonateCoinLogFormatter.BuildConfigUpConfirmedExhausted(),
            ConfigUpId: upId,
            ConfigUpScanStatus: DonateCoinConfigUpScanStatus.ConfirmedExhausted
        );
    }

    private async Task<DonateCoinConfigUpProgressSnapshot> EnsureConfigUpProgressAsync(
        long upId,
        BiliCookie ck
    )
    {
        var today = DateOnly.FromDateTime(DateTime.Now);
        _configUpProgressByUpId.TryGetValue(upId, out var previous);
        if (
            previous is not null
            && previous.VideoCountUpdatedOn == today
            && previous.Status != DonateCoinConfigUpScanStatus.Unknown
            && (
                previous.Status == DonateCoinConfigUpScanStatus.ConfirmedExhausted
                || previous.NextPageNumber > 0
            )
        )
        {
            return previous;
        }

        var videoCount = await GetVideoCountOfUpWithCacheAsync(upId, ck);
        var recordedVideoCount = previous?.RecordedVideoCount ?? 0;
        if (
            previous?.Status == DonateCoinConfigUpScanStatus.ConfirmedExhausted
            && videoCount >= previous.VideoCount
        )
        {
            var newVideoCount = Math.Max(0, videoCount - previous.VideoCount);
            var optimizedStatus =
                newVideoCount == 0
                    ? DonateCoinConfigUpScanStatus.ConfirmedExhausted
                    : DonateCoinConfigUpScanStatus.InProgress;
            var optimizedProgress = new DonateCoinConfigUpProgressSnapshot(
                videoCount,
                today,
                GetConfigUpStartPageNumber(newVideoCount),
                recordedVideoCount,
                optimizedStatus
            );
            await SaveConfigUpProgressAsync(ck.UserId, upId, optimizedProgress);
            return optimizedProgress;
        }

        var progress = new DonateCoinConfigUpProgressSnapshot(
            videoCount,
            today,
            GetConfigUpStartPageNumber(videoCount),
            recordedVideoCount,
            DonateCoinConfigUpScanStatus.Unknown
        );
        await SaveConfigUpProgressAsync(ck.UserId, upId, progress);
        return progress;
    }

    private async Task<DonateCoinConfigUpProgressSnapshot> MoveConfigUpCursorBackwardAsync(
        string userId,
        long upId,
        DonateCoinConfigUpProgressSnapshot progress
    )
    {
        var nextPageNumber = Math.Max(0, progress.NextPageNumber - 1);
        var updated = progress with
        {
            NextPageNumber = nextPageNumber,
            Status =
                nextPageNumber == 0
                    ? DonateCoinConfigUpScanStatus.ConfirmedExhausted
                    : DonateCoinConfigUpScanStatus.InProgress,
            FailureReason = null,
        };
        await SaveConfigUpProgressAsync(userId, upId, updated);
        LogConfigUpProgress(upId, updated);
        return updated;
    }

    private async Task<DonateCoinConfigUpProgressSnapshot> MarkConfigUpRetryableFailureAsync(
        string userId,
        long upId,
        DonateCoinConfigUpProgressSnapshot progress,
        string reason
    )
    {
        var updated = progress with
        {
            Status = DonateCoinConfigUpScanStatus.RetryableFailure,
            FailureReason = reason,
        };
        await SaveConfigUpProgressAsync(userId, upId, updated);
        LogConfigUpProgress(upId, updated);
        return updated;
    }

    private async Task<DonateCoinConfigUpProgressSnapshot> MarkConfigUpConfirmedExhaustedAsync(
        string userId,
        long upId,
        DonateCoinConfigUpProgressSnapshot progress
    )
    {
        var updated = progress with
        {
            NextPageNumber = 0,
            Status = DonateCoinConfigUpScanStatus.ConfirmedExhausted,
            FailureReason = null,
        };
        await SaveConfigUpProgressAsync(userId, upId, updated);
        LogConfigUpProgress(upId, updated);
        return updated;
    }

    private void LogConfigUpProgress(long upId, DonateCoinConfigUpProgressSnapshot progress)
    {
        var statusMessage = progress.Status switch
        {
            DonateCoinConfigUpScanStatus.ConfirmedExhausted =>
                DonateCoinLogFormatter.BuildConfigUpConfirmedExhausted(),
            DonateCoinConfigUpScanStatus.RetryableFailure =>
                DonateCoinLogFormatter.BuildConfigUpRetryableFailure(),
            _ => DonateCoinLogFormatter.BuildConfigUpInProgress(),
        };

        logger.LogInformation(
            "【配置UP】{upId}：已记录 {recordedCount} / 当前视频 {videoCount}，{status}",
            upId,
            progress.RecordedVideoCount,
            progress.VideoCount,
            statusMessage
        );

        if (
            progress.Status == DonateCoinConfigUpScanStatus.RetryableFailure
            && !string.IsNullOrWhiteSpace(progress.FailureReason)
        )
        {
            logger.LogWarning("【配置UP】{upId}：{reason}", upId, progress.FailureReason);
        }
    }

    private static int GetConfigUpStartPageNumber(int videoCount)
    {
        return videoCount <= 0 ? 0 : (int)Math.Ceiling(videoCount / (double)ConfigUpPageSize);
    }

    private async Task<DonateCoinSourceSearchResult> TryGetCanDonateVideoBySpecialUps(BiliCookie ck)
    {
        var request = new GetSpecialFollowingsRequest(long.Parse(ck.UserId));
        BiliApiResponse<List<UpInfo>> specials = await relationApi.GetFollowingsByTag(
            request,
            ck.ToString()
        );
        if (specials.Code != 0 || specials.Data is null)
        {
            return new DonateCoinSourceSearchResult(
                DonateCoinVideoSource.SpecialFollowings,
                FailureReason: $"获取特别关注列表失败：{specials.Message ?? specials.Code.ToString()}",
                RetryableFailure: true
            );
        }

        if (specials.Data.Count == 0)
        {
            return new DonateCoinSourceSearchResult(
                DonateCoinVideoSource.SpecialFollowings,
                FailureReason: "列表为空，跳过"
            );
        }

        return await TryCanDonateVideoByUps(
            specials.Data.Select(x => x.Mid).ToList(),
            SpecialFollowingTryCount,
            ck,
            DonateCoinVideoSource.SpecialFollowings
        );
    }

    private async Task<DonateCoinSourceSearchResult> TryGetCanDonateVideoByFollowingUps(
        BiliCookie ck
    )
    {
        var request = new GetFollowingsRequest(long.Parse(ck.UserId));
        BiliApiResponse<GetFollowingsResponse> result = await relationApi.GetFollowings(
            request,
            ck.ToString()
        );
        if (result.Code != 0 || result.Data is null)
            return new DonateCoinSourceSearchResult(
                DonateCoinVideoSource.Followings,
                FailureReason: $"获取关注列表失败：{result.Message ?? result.Code.ToString()}",
                RetryableFailure: true
            );
        if (result.Data.Total == 0)
        {
            return new DonateCoinSourceSearchResult(
                DonateCoinVideoSource.Followings,
                FailureReason: "关注列表为空，跳过"
            );
        }

        return await TryCanDonateVideoByUps(
            result.Data.List.Select(x => x.Mid).ToList(),
            FollowingTryCount,
            ck,
            DonateCoinVideoSource.Followings
        );
    }

    private async Task<DonateCoinSourceSearchResult> TryGetCanDonateVideoByRegion(
        int tryCount,
        BiliCookie ck
    )
    {
        try
        {
            for (int i = 0; i < tryCount; i++)
            {
                var video = await videoDomainService.GetRandomVideoOfRanking();
                var eligibility = await GetVideoEligibilityAsync(video.Aid, ck);
                if (eligibility != DonateCoinVideoEligibility.Eligible)
                {
                    continue;
                }

                return new DonateCoinSourceSearchResult(
                    DonateCoinVideoSource.Ranking,
                    new UpVideoInfo
                    {
                        Aid = video.Aid,
                        Bvid = video.Bvid,
                        Title = video.Title,
                        Length = "00:15",
                    }
                );
            }
        }
        catch (Exception e)
        {
            return new DonateCoinSourceSearchResult(
                DonateCoinVideoSource.Ranking,
                FailureReason: $"获取失败，可能触发风控或验证码，已跳过。{e.Message}"
            );
        }

        return new DonateCoinSourceSearchResult(
            DonateCoinVideoSource.Ranking,
            FailureReason: "多次尝试后仍未找到可投视频"
        );
    }

    private async Task<DonateCoinSourceSearchResult> TryCanDonateVideoByUps(
        List<long> upIds,
        int tryCount,
        BiliCookie ck,
        DonateCoinVideoSource source
    )
    {
        var candidateUpIds = upIds
            .Where(x => x != 0 && x != long.MinValue)
            .Where(x => x.ToString() != ck.UserId)
            .Distinct()
            .ToList();
        if (candidateUpIds.Count == 0)
        {
            return new DonateCoinSourceSearchResult(source, FailureReason: "没有可用的UP主");
        }

        try
        {
            var attempts = 0;
            while (attempts < tryCount)
            {
                foreach (var upId in Shuffle(candidateUpIds))
                {
                    if (attempts >= tryCount)
                    {
                        break;
                    }

                    attempts++;
                    var videoCount = await GetVideoCountOfUpWithCacheAsync(upId, ck);
                    if (videoCount <= 0)
                    {
                        continue;
                    }

                    var videoInfo = await videoDomainService.GetRandomVideoOfUp(
                        upId,
                        videoCount,
                        ck
                    );
                    logger.LogDebug("获取到视频{aid}({title})", videoInfo?.Aid, videoInfo?.Title);

                    if (videoInfo == null)
                    {
                        continue;
                    }

                    var eligibility = await GetVideoEligibilityAsync(videoInfo.Aid, ck);
                    if (eligibility == DonateCoinVideoEligibility.Eligible)
                    {
                        return new DonateCoinSourceSearchResult(source, videoInfo);
                    }
                }
            }
        }
        catch (Exception e)
        {
            return new DonateCoinSourceSearchResult(
                source,
                FailureReason: $"取视频失败：{e.Message}"
            );
        }

        return new DonateCoinSourceSearchResult(source, FailureReason: "多次尝试无可投视频");
    }

    private async Task<int> GetVideoCountOfUpWithCacheAsync(long upId, BiliCookie ck)
    {
        if (_upVideoCountDicCatch.TryGetValue(upId, out var videoCount))
        {
            return videoCount;
        }

        videoCount = await videoDomainService.GetVideoCountOfUp(upId, ck);
        _upVideoCountDicCatch[upId] = videoCount;
        return videoCount;
    }

    private static List<long> Shuffle(IReadOnlyCollection<long> source)
    {
        var shuffled = source.ToList();
        for (int i = shuffled.Count - 1; i > 0; i--)
        {
            var randomIndex = Random.Shared.Next(i + 1);
            (shuffled[i], shuffled[randomIndex]) = (shuffled[randomIndex], shuffled[i]);
        }

        return shuffled;
    }

    private void LogSourceFailure(
        DonateCoinSourceSearchResult result,
        DonateCoinVideoSource? nextSource = null
    )
    {
        if (string.IsNullOrWhiteSpace(result.FailureReason))
        {
            return;
        }

        var message = nextSource.HasValue
            ? DonateCoinLogFormatter.BuildSourceSkippedWithFallback(
                result.Source,
                result.FailureReason,
                nextSource.Value
            )
            : DonateCoinLogFormatter.BuildSourceSkipped(result.Source, result.FailureReason);
        logger.LogInformation("{message}", message);
    }

    private async Task<DonateCoinVideoEligibility> GetVideoEligibilityAsync(
        long aid,
        BiliCookie ck,
        long? configUpId = null,
        int? knownMultiply = null,
        bool statusAlreadyChecked = false
    )
    {
        if (_blacklistedAidSet.Contains(aid))
        {
            logger.LogInformation("跳过候选视频 Av{aid}：已在投币进度中记录", aid);
            return DonateCoinVideoEligibility.Blacklisted;
        }

        var aidText = aid.ToString();
        if (_attemptedVideoAidSet.Contains(aidText))
        {
            logger.LogInformation("跳过候选视频 Av{aid}：本轮已尝试过", aid);
            return DonateCoinVideoEligibility.DuplicateInCurrentRun;
        }

        var multiply = statusAlreadyChecked
            ? knownMultiply
            : await TryGetDonatedCoinsForVideoAsync(aidText, ck);
        if (!multiply.HasValue)
        {
            return DonateCoinVideoEligibility.CheckFailed;
        }

        _attemptedVideoAidSet.Add(aidText);
        logger.LogDebug("已为Av{aid}投过{num}枚硬币", aid, multiply.Value);

        if (multiply.Value > 0)
        {
            await MarkVideoAsBlacklistedAsync(ck.UserId, aid);
            if (configUpId.HasValue)
            {
                await IncreaseConfigUpRecordedCountIfNeededAsync(ck.UserId, configUpId.Value, aid);
            }
            logger.LogInformation("跳过候选视频 Av{aid}：已投过{num}枚硬币", aid, multiply.Value);
            return DonateCoinVideoEligibility.AlreadyDonated;
        }

        return DonateCoinVideoEligibility.Eligible;
    }

    private async Task<IReadOnlyDictionary<long, int?>> GetConfigUpCoinStatusesAsync(
        IReadOnlyCollection<UpVideoInfo> videos,
        BiliCookie ck
    )
    {
        var candidateAids = videos
            .Where(video =>
                !_blacklistedAidSet.Contains(video.Aid)
                && !_attemptedVideoAidSet.Contains(video.Aid.ToString())
            )
            .Select(video => video.Aid)
            .Distinct()
            .ToList();
        if (candidateAids.Count == 0)
        {
            return new Dictionary<long, int?>();
        }

        using var limiter = new SemaphoreSlim(ConfigUpCoinStatusConcurrency);
        var tasks = candidateAids.Select(async aid =>
        {
            await limiter.WaitAsync();
            try
            {
                return (
                    Aid: aid,
                    Multiply: await TryGetDonatedCoinsForVideoAsync(aid.ToString(), ck)
                );
            }
            finally
            {
                limiter.Release();
            }
        });

        var results = await Task.WhenAll(tasks);
        return results.ToDictionary(result => result.Aid, result => result.Multiply);
    }

    private async Task<int?> TryGetDonatedCoinsForVideoAsync(string aid, BiliCookie ck)
    {
        try
        {
            if (_videoCoinCountCache.TryGetValue(aid, out var multiply))
            {
                return multiply;
            }

            var response = await videoApi.GetDonatedCoinsForVideo(
                new GetAlreadyDonatedCoinsRequest(long.Parse(aid)),
                ck.ToString()
            );
            if (response.Code != 0 || response.Data is null)
                return null;
            multiply = response.Data.Multiply;
            _videoCoinCountCache[aid] = multiply;
            return multiply;
        }
        catch (Exception e)
        {
            logger.LogWarning("获取视频投币状态异常：{msg}", e.Message);
            return null;
        }
    }

    private async Task IncreaseConfigUpRecordedCountIfNeededAsync(
        string userId,
        long upId,
        long aid
    )
    {
        if (!_configUpProgressByUpId.TryGetValue(upId, out var progress))
        {
            return;
        }

        var updated = progress with
        {
            RecordedVideoCount = Math.Min(progress.VideoCount, progress.RecordedVideoCount + 1),
        };
        await SaveConfigUpProgressAsync(userId, upId, updated);
    }

    #endregion

    private sealed record DonateCoinVideoSelectionResult(
        UpVideoInfo Video,
        DonateCoinVideoSource Source,
        long? ConfigUpId = null
    );

    private sealed record DonateCoinSourceSearchResult(
        DonateCoinVideoSource Source,
        UpVideoInfo? Video = null,
        string? FailureReason = null,
        long? ConfigUpId = null,
        DonateCoinConfigUpScanStatus? ConfigUpScanStatus = null,
        bool RetryableFailure = false
    );

    private enum DonateCoinVideoEligibility
    {
        Eligible,
        Blacklisted,
        DuplicateInCurrentRun,
        AlreadyDonated,
        CheckFailed,
    }
}
