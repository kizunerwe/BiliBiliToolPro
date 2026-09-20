using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Ray.BiliBiliTool.Agent;
using Ray.BiliBiliTool.Agent.BiliBiliAgent.Dtos;
using Ray.BiliBiliTool.Agent.BiliBiliAgent.Dtos.Relation;
using Ray.BiliBiliTool.Agent.BiliBiliAgent.Interfaces;
using Ray.BiliBiliTool.Config.Options;
using Ray.BiliBiliTool.DomainService;
using Ray.BiliBiliTool.DomainService.Dtos;
using Ray.BiliBiliTool.DomainService.Interfaces;

namespace DomainServiceTest;

public sealed class VideoDomainServiceOutcomeTest
{
    [Fact]
    public async Task ShareRequest_ShouldUseCurrentVideoPageAsReferer()
    {
        string? referer = null;
        var videoApi = TestDoubles.Api<IVideoApi>(
            (method, arguments) =>
            {
                if (method.Name != nameof(IVideoApi.ShareVideo))
                    throw new InvalidOperationException($"Unexpected API: {method.Name}");

                referer = Assert.IsType<string>(arguments[2]);
                return Task.FromResult(new BiliApiResponse { Code = 0 });
            }
        );
        var service = CreateService(new DailyTaskOptions(), videoApi: videoApi);

        await service.ShareVideo(
            new()
            {
                Aid = "123",
                Bvid = "BV1test",
                Title = "test",
            },
            TestDoubles.Cookie()
        );

        Assert.Equal("https://www.bilibili.com/video/BV1test", referer);
    }

    [Fact]
    public async Task DisabledWatchAndShare_ShouldBeSkipped()
    {
        var service = CreateService(new DailyTaskOptions());

        var result = await service.WatchAndShareVideo(new(), TestDoubles.Cookie());

        Assert.Equal(TaskStepStatus.Skipped, result.Status);
    }

    [Fact]
    public async Task CompletedWatchAndShare_ShouldLogSkipWithoutCallingVideoApis()
    {
        var logger = new ListLogger<VideoDomainService>();
        var apiCalls = 0;
        var videoApi = TestDoubles.Api<IVideoApi>(
            (method, _) =>
            {
                apiCalls++;
                throw new InvalidOperationException($"Unexpected API: {method.Name}");
            }
        );
        var rankingApi = TestDoubles.Api<IVideoWithoutCookieApi>(
            (method, _) =>
            {
                apiCalls++;
                throw new InvalidOperationException($"Unexpected API: {method.Name}");
            }
        );
        var service = CreateService(
            new DailyTaskOptions { IsWatchVideo = true, IsShareVideo = true },
            videoApi: videoApi,
            rankingApi: rankingApi,
            logger: logger
        );

        var result = await service.WatchAndShareVideo(
            new() { Watch = true, Share = true },
            TestDoubles.Cookie()
        );

        Assert.Equal(TaskStepStatus.Skipped, result.Status);
        Assert.Contains(
            logger.Messages,
            message => message.Contains("今日观看和分享任务均已完成，不需要重复执行")
        );
        Assert.Equal(0, apiCalls);
    }

    [Fact]
    public async Task CompletedWatch_ShouldKeepSkipLogAndExecuteShare()
    {
        var logger = new ListLogger<VideoDomainService>();
        var shareCalls = 0;
        var videoApi = TestDoubles.Api<IVideoApi>(
            (method, _) =>
                method.Name switch
                {
                    nameof(IVideoApi.UploadVideoHeartbeat) => Task.FromResult(
                        new BiliApiResponse { Code = 0 }
                    ),
                    nameof(IVideoApi.ShareVideo) => Task.FromResult(
                        new BiliApiResponse { Code = ++shareCalls == 1 ? 0 : -1 }
                    ),
                    _ => throw new InvalidOperationException($"Unexpected API: {method.Name}"),
                }
        );
        var service = CreateService(
            new DailyTaskOptions { IsWatchVideo = true, IsShareVideo = true },
            videoApi: videoApi,
            logger: logger
        );

        var result = await service.WatchAndShareVideo(
            new() { Watch = true, Share = false },
            TestDoubles.Cookie()
        );

        Assert.Equal(TaskStepStatus.Succeeded, result.Status);
        Assert.Contains(
            logger.Messages,
            message => message.Contains("今天已经观看过了，不需要再看啦")
        );
        Assert.Equal(1, shareCalls);
    }

    [Fact]
    public async Task CompletedShare_ShouldKeepSkipLogAndExecuteWatch()
    {
        var logger = new ListLogger<VideoDomainService>();
        var heartbeatCalls = 0;
        var shareCalls = 0;
        var videoApi = TestDoubles.Api<IVideoApi>(
            (method, _) =>
                method.Name switch
                {
                    nameof(IVideoApi.UploadVideoHeartbeat) => Task.FromResult(
                        new BiliApiResponse { Code = ++heartbeatCalls > 0 ? 0 : -1 }
                    ),
                    nameof(IVideoApi.ShareVideo) => Task.FromResult(
                        new BiliApiResponse { Code = ++shareCalls == 0 ? 0 : -1 }
                    ),
                    _ => throw new InvalidOperationException($"Unexpected API: {method.Name}"),
                }
        );
        var service = CreateService(
            new DailyTaskOptions { IsWatchVideo = true, IsShareVideo = true },
            videoApi: videoApi,
            logger: logger
        );

        var result = await service.WatchAndShareVideo(
            new() { Watch = false, Share = true },
            TestDoubles.Cookie()
        );

        Assert.Equal(TaskStepStatus.Succeeded, result.Status);
        Assert.Contains(
            logger.Messages,
            message => message.Contains("今天已经分享过了，不用再分享啦")
        );
        Assert.True(heartbeatCalls > 0);
        Assert.Equal(0, shareCalls);
    }

    [Fact]
    public async Task ShareRejection_ShouldBeFailed()
    {
        var videoApi = TestDoubles.Api<IVideoApi>(
            (method, _) =>
                method.Name switch
                {
                    nameof(IVideoApi.UploadVideoHeartbeat) => Task.FromResult(
                        new BiliApiResponse { Code = 0 }
                    ),
                    nameof(IVideoApi.ShareVideo) => Task.FromResult(
                        new BiliApiResponse { Code = -403, Message = "账号异常" }
                    ),
                    _ => throw new InvalidOperationException($"Unexpected API: {method.Name}"),
                }
        );
        var service = CreateService(
            new DailyTaskOptions { IsShareVideo = true },
            videoApi: videoApi
        );

        var result = await service.WatchAndShareVideo(
            new() { Watch = true, Share = false },
            TestDoubles.Cookie()
        );

        Assert.Equal(TaskStepStatus.Failed, result.Status);
        Assert.Contains("-403", result.Reason);
        Assert.Contains("账号异常", result.Reason);
    }

    [Fact]
    public async Task ShareRejection403_ShouldRetryOnceAndSucceed()
    {
        var shareCalls = 0;
        var delays = 0;
        var videoApi = TestDoubles.Api<IVideoApi>(
            (method, _) =>
                method.Name switch
                {
                    nameof(IVideoApi.UploadVideoHeartbeat) => Task.FromResult(
                        new BiliApiResponse { Code = 0 }
                    ),
                    nameof(IVideoApi.ShareVideo) => Task.FromResult(
                        ++shareCalls == 1
                            ? new BiliApiResponse { Code = -403, Message = "账号异常" }
                            : new BiliApiResponse { Code = 0 }
                    ),
                    _ => throw new InvalidOperationException($"Unexpected API: {method.Name}"),
                }
        );
        var service = CreateService(
            new DailyTaskOptions { IsShareVideo = true },
            videoApi: videoApi,
            taskDelay: new RecordingTaskDelay(() => delays++)
        );

        var result = await service.WatchAndShareVideo(
            new() { Watch = true, Share = false },
            TestDoubles.Cookie()
        );

        Assert.Equal(TaskStepStatus.Succeeded, result.Status);
        Assert.Equal(2, shareCalls);
        Assert.Equal(1, delays);
    }

    [Fact]
    public async Task ShareRejection403_ShouldRebuildRequestBeforeRetry()
    {
        var requests = new List<ShareVideoRequest>();
        var shareCalls = 0;
        var videoApi = TestDoubles.Api<IVideoApi>(
            (method, arguments) =>
                method.Name switch
                {
                    nameof(IVideoApi.UploadVideoHeartbeat) => Task.FromResult(
                        new BiliApiResponse { Code = 0 }
                    ),
                    nameof(IVideoApi.ShareVideo) => CaptureShareRequest(
                        arguments,
                        requests,
                        ++shareCalls
                    ),
                    _ => throw new InvalidOperationException($"Unexpected API: {method.Name}"),
                }
        );
        var service = CreateService(
            new DailyTaskOptions { IsShareVideo = true },
            videoApi: videoApi,
            taskDelay: new RecordingTaskDelay(() => { })
        );

        var result = await service.WatchAndShareVideo(
            new() { Watch = true, Share = false },
            TestDoubles.Cookie()
        );

        Assert.Equal(TaskStepStatus.Succeeded, result.Status);
        Assert.Equal(2, requests.Count);
        Assert.NotSame(requests[0], requests[1]);
    }

    private static Task<BiliApiResponse> CaptureShareRequest(
        object?[] arguments,
        ICollection<ShareVideoRequest> requests,
        int shareCall
    )
    {
        requests.Add(Assert.IsType<ShareVideoRequest>(arguments[0]));
        return Task.FromResult(
            shareCall == 1
                ? new BiliApiResponse { Code = -403, Message = "账号异常" }
                : new BiliApiResponse { Code = 0 }
        );
    }

    [Fact]
    public async Task ShareRejection403_ShouldRetryOnceAndRemainFailed()
    {
        var shareCalls = 0;
        var videoApi = TestDoubles.Api<IVideoApi>(
            (method, _) =>
                method.Name switch
                {
                    nameof(IVideoApi.UploadVideoHeartbeat) => Task.FromResult(
                        new BiliApiResponse { Code = 0 }
                    ),
                    nameof(IVideoApi.ShareVideo) => Task.FromResult(
                        ++shareCalls <= 2
                            ? new BiliApiResponse { Code = -403, Message = "账号异常" }
                            : new BiliApiResponse { Code = 0 }
                    ),
                    _ => throw new InvalidOperationException($"Unexpected API: {method.Name}"),
                }
        );
        var service = CreateService(
            new DailyTaskOptions { IsShareVideo = true },
            videoApi: videoApi
        );

        var result = await service.WatchAndShareVideo(
            new() { Watch = true, Share = false },
            TestDoubles.Cookie()
        );

        Assert.Equal(TaskStepStatus.Failed, result.Status);
        Assert.Equal(2, shareCalls);
        Assert.Contains("-403", result.Reason);
        Assert.Contains("账号异常", result.Reason);
    }

    [Fact]
    public async Task ShareRejectionNon403_ShouldNotRetry()
    {
        var shareCalls = 0;
        var videoApi = TestDoubles.Api<IVideoApi>(
            (method, _) =>
                method.Name switch
                {
                    nameof(IVideoApi.UploadVideoHeartbeat) => Task.FromResult(
                        new BiliApiResponse { Code = 0 }
                    ),
                    nameof(IVideoApi.ShareVideo) => Task.FromResult(
                        ++shareCalls == 1
                            ? new BiliApiResponse { Code = -412, Message = "频繁操作" }
                            : new BiliApiResponse { Code = 0 }
                    ),
                    _ => throw new InvalidOperationException($"Unexpected API: {method.Name}"),
                }
        );
        var service = CreateService(
            new DailyTaskOptions { IsShareVideo = true },
            videoApi: videoApi
        );

        var result = await service.WatchAndShareVideo(
            new() { Watch = true, Share = false },
            TestDoubles.Cookie()
        );

        Assert.Equal(TaskStepStatus.Failed, result.Status);
        Assert.Equal(1, shareCalls);
        Assert.Contains("-412", result.Reason);
    }

    [Fact]
    public async Task ShareRejection403_ShouldHonorCancellationBeforeRetry()
    {
        var shareCalls = 0;
        var videoApi = TestDoubles.Api<IVideoApi>(
            (method, _) =>
                method.Name switch
                {
                    nameof(IVideoApi.UploadVideoHeartbeat) => Task.FromResult(
                        new BiliApiResponse { Code = 0 }
                    ),
                    nameof(IVideoApi.ShareVideo) => Task.FromResult(
                        ++shareCalls == 1
                            ? new BiliApiResponse { Code = -403, Message = "账号异常" }
                            : new BiliApiResponse { Code = 0 }
                    ),
                    _ => throw new InvalidOperationException($"Unexpected API: {method.Name}"),
                }
        );
        using var cancellation = new CancellationTokenSource();
        var service = CreateService(
            new DailyTaskOptions { IsShareVideo = true },
            videoApi: videoApi,
            taskDelay: new RecordingTaskDelay(
                (_, cancellationToken) =>
                {
                    cancellation.Cancel();
                    cancellationToken.ThrowIfCancellationRequested();
                    return Task.CompletedTask;
                }
            )
        );

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            service.WatchAndShareVideo(
                new() { Watch = true, Share = false },
                TestDoubles.Cookie(),
                cancellation.Token
            )
        );
        Assert.Equal(1, shareCalls);
    }

    [Fact]
    public async Task ShareRejection_ShouldLogAidAndCookieKeyPresenceWithoutCookieValues()
    {
        var logger = new ListLogger<VideoDomainService>();
        var cookie = new BiliCookie(
            new Dictionary<string, string>
            {
                ["DedeUserID"] = "123",
                ["SESSDATA"] = "secret-session",
                ["bili_jct"] = "secret-csrf",
                ["buvid3"] = "secret-buvid",
            }
        );
        var videoApi = TestDoubles.Api<IVideoApi>(
            (method, _) =>
                method.Name switch
                {
                    nameof(IVideoApi.UploadVideoHeartbeat) => Task.FromResult(
                        new BiliApiResponse { Code = 0 }
                    ),
                    nameof(IVideoApi.ShareVideo) => Task.FromResult(
                        new BiliApiResponse { Code = -403, Message = "账号异常" }
                    ),
                    _ => throw new InvalidOperationException($"Unexpected API: {method.Name}"),
                }
        );
        var service = CreateService(
            new DailyTaskOptions { IsShareVideo = true },
            videoApi: videoApi,
            logger: logger
        );

        await service.WatchAndShareVideo(new() { Watch = true, Share = false }, cookie);

        Assert.Contains(logger.Messages, message => message.Contains("aid：1"));
        Assert.Contains(
            logger.Messages,
            message =>
                message.Contains("buvid3=存在")
                && message.Contains("buvid4=缺失")
                && message.Contains("buvid_fp=缺失")
                && message.Contains("b_nut=缺失")
        );
        Assert.DoesNotContain(logger.Messages, message => message.Contains("secret-session"));
        Assert.DoesNotContain(logger.Messages, message => message.Contains("secret-csrf"));
        Assert.DoesNotContain(logger.Messages, message => message.Contains("secret-buvid"));
    }

    [Fact]
    public async Task ShareRejectionWithoutMessage_ShouldKeepCodeAndFallbackReason()
    {
        var videoApi = TestDoubles.Api<IVideoApi>(
            (method, _) =>
                method.Name switch
                {
                    nameof(IVideoApi.UploadVideoHeartbeat) => Task.FromResult(
                        new BiliApiResponse { Code = 0 }
                    ),
                    nameof(IVideoApi.ShareVideo) => Task.FromResult(
                        new BiliApiResponse { Code = -412 }
                    ),
                    _ => throw new InvalidOperationException($"Unexpected API: {method.Name}"),
                }
        );
        var service = CreateService(
            new DailyTaskOptions { IsShareVideo = true },
            videoApi: videoApi
        );

        var result = await service.WatchAndShareVideo(
            new() { Watch = true, Share = false },
            TestDoubles.Cookie()
        );

        Assert.Equal(TaskStepStatus.Failed, result.Status);
        Assert.Contains("-412", result.Reason);
        Assert.Contains("未提供原因", result.Reason);
    }

    [Fact]
    public async Task ShareRejectionWithoutCode_ShouldReportMissingBusinessCode()
    {
        var videoApi = TestDoubles.Api<IVideoApi>(
            (method, _) =>
                method.Name switch
                {
                    nameof(IVideoApi.UploadVideoHeartbeat) => Task.FromResult(
                        new BiliApiResponse { Code = 0 }
                    ),
                    nameof(IVideoApi.ShareVideo) => Task.FromResult(
                        new BiliApiResponse { Message = "响应不完整" }
                    ),
                    _ => throw new InvalidOperationException($"Unexpected API: {method.Name}"),
                }
        );
        var service = CreateService(
            new DailyTaskOptions { IsShareVideo = true },
            videoApi: videoApi
        );

        var result = await service.WatchAndShareVideo(
            new() { Watch = true, Share = false },
            TestDoubles.Cookie()
        );

        Assert.Equal(TaskStepStatus.Failed, result.Status);
        Assert.Contains("业务码缺失", result.Reason);
        Assert.Contains("响应不完整", result.Reason);
    }

    [Fact]
    public async Task ShareNullResponse_ShouldReturnStableFailure()
    {
        var videoApi = TestDoubles.Api<IVideoApi>(
            (method, _) =>
                method.Name switch
                {
                    nameof(IVideoApi.UploadVideoHeartbeat) => Task.FromResult(
                        new BiliApiResponse { Code = 0 }
                    ),
                    nameof(IVideoApi.ShareVideo) => Task.FromResult<BiliApiResponse>(null!),
                    _ => throw new InvalidOperationException($"Unexpected API: {method.Name}"),
                }
        );
        var service = CreateService(
            new DailyTaskOptions { IsShareVideo = true },
            videoApi: videoApi
        );

        var result = await service.WatchAndShareVideo(
            new() { Watch = true, Share = false },
            TestDoubles.Cookie()
        );

        Assert.Equal(TaskStepStatus.Failed, result.Status);
        Assert.Contains("接口未返回有效响应", result.Reason);
    }

    [Fact]
    public async Task PublicShareNullResponse_ShouldLogStableFailureWithoutThrowing()
    {
        var logger = new ListLogger<VideoDomainService>();
        var videoApi = TestDoubles.Api<IVideoApi>(
            (method, _) =>
                method.Name == nameof(IVideoApi.ShareVideo)
                    ? Task.FromResult<BiliApiResponse>(null!)
                    : throw new InvalidOperationException($"Unexpected API: {method.Name}")
        );
        var service = CreateService(new DailyTaskOptions(), videoApi: videoApi, logger: logger);

        await service.ShareVideo(
            new()
            {
                Aid = "1",
                Bvid = "BV1",
                Title = "test",
            },
            TestDoubles.Cookie()
        );

        Assert.Contains(logger.Messages, message => message.Contains("接口未返回有效响应"));
    }

    [Fact]
    public async Task WatchRejection_ShouldRemainFailed_WhenShareSucceeds()
    {
        var heartbeatCalls = 0;
        var videoApi = TestDoubles.Api<IVideoApi>(
            (method, _) =>
                method.Name switch
                {
                    nameof(IVideoApi.UploadVideoHeartbeat) => Task.FromResult(
                        new BiliApiResponse
                        {
                            Code = ++heartbeatCalls == 2 ? -1 : 0,
                            Message = "播放拒绝",
                        }
                    ),
                    nameof(IVideoApi.ShareVideo) => Task.FromResult(
                        new BiliApiResponse { Code = 0 }
                    ),
                    _ => throw new InvalidOperationException($"Unexpected API: {method.Name}"),
                }
        );
        var service = CreateService(
            new DailyTaskOptions { IsWatchVideo = true, IsShareVideo = true },
            videoApi: videoApi
        );

        var result = await service.WatchAndShareVideo(new(), TestDoubles.Cookie());

        Assert.Equal(TaskStepStatus.Failed, result.Status);
    }

    [Fact]
    public async Task FollowingLookupFailure_ShouldBeFailed_InsteadOfUsingRankingFallback()
    {
        var relationApi = TestDoubles.Api<IRelationApi>(
            (method, _) =>
                method.Name == nameof(IRelationApi.GetFollowings)
                    ? Task.FromResult(
                        new BiliApiResponse<GetFollowingsResponse>
                        {
                            Code = 1001,
                            Message = "关注列表失败",
                        }
                    )
                    : throw new InvalidOperationException($"Unexpected API: {method.Name}")
        );
        var videoApi = TestDoubles.Api<IVideoApi>(
            (method, _) =>
                method.Name
                    is nameof(IVideoApi.UploadVideoHeartbeat)
                        or nameof(IVideoApi.ShareVideo)
                    ? Task.FromResult(new BiliApiResponse { Code = 0 })
                    : throw new InvalidOperationException($"Unexpected API: {method.Name}")
        );
        var service = CreateService(
            new DailyTaskOptions { IsShareVideo = true },
            videoApi,
            relationApi
        );

        var result = await service.WatchAndShareVideo(
            new() { Watch = true, Share = false },
            TestDoubles.Cookie()
        );

        Assert.Equal(TaskStepStatus.Failed, result.Status);
        Assert.Contains("关注列表失败", result.Reason);
    }

    [Fact]
    public async Task RankingFailure_ShouldNotUseDataFromErrorResponse()
    {
        var rankingApi = TestDoubles.Api<IVideoWithoutCookieApi>(
            (method, _) =>
                method.Name == nameof(IVideoWithoutCookieApi.GetRegionRankingVideosV2)
                    ? Task.FromResult(
                        new BiliApiResponse<Ranking>
                        {
                            Code = 1001,
                            Message = "排行榜失败",
                            Data = new Ranking
                            {
                                List =
                                [
                                    new RankingInfo
                                    {
                                        Aid = 1,
                                        Bvid = "BV1",
                                        Title = "错误响应中的视频",
                                    },
                                ],
                            },
                        }
                    )
                    : throw new InvalidOperationException($"Unexpected API: {method.Name}")
        );
        var service = CreateService(new DailyTaskOptions(), rankingApi: rankingApi);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.GetRandomVideoOfRanking()
        );

        Assert.Contains("排行榜失败", exception.Message);
    }

    private static VideoDomainService CreateService(
        DailyTaskOptions options,
        IVideoApi? videoApi = null,
        IRelationApi? relationApi = null,
        IVideoWithoutCookieApi? rankingApi = null,
        ILogger<VideoDomainService>? logger = null,
        ITaskDelay? taskDelay = null
    )
    {
        relationApi ??= TestDoubles.Api<IRelationApi>(
            (method, _) =>
                method.Name == nameof(IRelationApi.GetFollowings)
                    ? Task.FromResult(
                        new BiliApiResponse<GetFollowingsResponse>
                        {
                            Code = 0,
                            Data = new GetFollowingsResponse(),
                        }
                    )
                    : throw new InvalidOperationException($"Unexpected API: {method.Name}")
        );
        rankingApi ??= TestDoubles.Api<IVideoWithoutCookieApi>(
            (method, _) =>
                method.Name == nameof(IVideoWithoutCookieApi.GetRegionRankingVideosV2)
                    ? Task.FromResult(
                        new BiliApiResponse<Ranking>
                        {
                            Code = 0,
                            Data = new Ranking
                            {
                                List =
                                [
                                    new RankingInfo
                                    {
                                        Aid = 1,
                                        Bvid = "BV1",
                                        Cid = 1,
                                        Title = "test",
                                        Duration = 15,
                                    },
                                ],
                            },
                        }
                    )
                    : throw new InvalidOperationException($"Unexpected API: {method.Name}")
        );

        return new VideoDomainService(
            logger ?? NullLogger<VideoDomainService>.Instance,
            new TestDoubles.OptionsMonitor<DailyTaskOptions>(options),
            relationApi,
            videoApi
                ?? TestDoubles.Api<IVideoApi>(
                    (method, _) =>
                        throw new InvalidOperationException($"Unexpected API: {method.Name}")
                ),
            rankingApi,
            new RankingVideoCache(),
            taskDelay ?? new NoOpTaskDelay()
        );
    }

    private sealed class NoOpTaskDelay : ITaskDelay
    {
        public Task Delay(TimeSpan delay, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }

    private sealed class RecordingTaskDelay : ITaskDelay
    {
        private readonly Action? _onDelay;
        private readonly Func<TimeSpan, CancellationToken, Task>? _delay;

        public RecordingTaskDelay(Action onDelay) => _onDelay = onDelay;

        public RecordingTaskDelay(Func<TimeSpan, CancellationToken, Task> delay) => _delay = delay;

        public Task Delay(TimeSpan delay, CancellationToken cancellationToken)
        {
            _onDelay?.Invoke();
            return _delay?.Invoke(delay, cancellationToken) ?? Task.CompletedTask;
        }
    }

    private sealed class ListLogger<T> : ILogger<T>
    {
        public List<string> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter
        )
        {
            Messages.Add(formatter(state, exception));
        }
    }
}
