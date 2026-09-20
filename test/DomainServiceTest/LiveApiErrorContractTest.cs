using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Ray.BiliBiliTool.Agent;
using Ray.BiliBiliTool.Agent.BiliBiliAgent.Dtos;
using Ray.BiliBiliTool.Agent.BiliBiliAgent.Dtos.Live;
using Ray.BiliBiliTool.Agent.BiliBiliAgent.Dtos.Relation;
using Ray.BiliBiliTool.Agent.BiliBiliAgent.Interfaces;
using Ray.BiliBiliTool.Config.Options;
using Ray.BiliBiliTool.DomainService;

namespace DomainServiceTest;

public sealed class LiveApiErrorContractTest
{
    [Fact]
    public async Task ExchangeSilver2Coin_ShouldNotSucceed_WhenResponseDataIsMissing()
    {
        var silver2CoinCalls = 0;
        var liveApi = TestDoubles.Api<ILiveApi>(
            (method, _) =>
                method.Name switch
                {
                    nameof(ILiveApi.GetLiveWalletStatus) => Task.FromResult(
                        new BiliApiResponse<LiveWalletStatusResponse>
                        {
                            Code = 0,
                            Data = new LiveWalletStatusResponse { Silver_2_coin_left = 1 },
                        }
                    ),
                    nameof(ILiveApi.Silver2Coin) => CountSilver2CoinCall(),
                    _ => throw new InvalidOperationException($"Unexpected API: {method.Name}"),
                }
        );
        var service = CreateService(
            liveApi,
            silver2CoinOptions: new Silver2CoinTaskOptions { IsEnable = true }
        );

        var result = await service.ExchangeSilver2Coin(TestDoubles.Cookie());

        Assert.False(result);
        Assert.Equal(1, silver2CoinCalls);

        Task<BiliApiResponse<Silver2CoinResponse>> CountSilver2CoinCall()
        {
            silver2CoinCalls++;
            return Task.FromResult(new BiliApiResponse<Silver2CoinResponse> { Code = 0 });
        }
    }

    [Fact]
    public async Task TryJoinTianXuan_ShouldRejectErrorResponseWithData()
    {
        var logger = new ListLogger<LiveDomainService>();
        var liveApi = TestDoubles.Api<ILiveApi>(
            (method, _) =>
                method.Name switch
                {
                    nameof(ILiveApi.CheckTianXuan) => Task.FromResult(
                        new BiliApiResponse<CheckTianXuanDto>
                        {
                            Code = 1001,
                            Message = "天选查询失败",
                            Data = CreateCheckResult(),
                        }
                    ),
                    nameof(ILiveApi.Join) => Task.FromResult(
                        new BiliApiResponse<JoinTianXuanResponse> { Code = 0 }
                    ),
                    _ => throw new InvalidOperationException($"Unexpected API: {method.Name}"),
                }
        );
        var service = CreateService(liveApi, logger: logger);

        await service.TryJoinTianXuan(CreateTarget(), TestDoubles.Cookie());

        Assert.Contains(logger.Messages, message => message.Contains("天选查询失败"));
        Assert.DoesNotContain(logger.Messages, message => message.Contains("抽奖】成功"));
    }

    [Fact]
    public async Task TianXuan_ShouldRejectErrorResponseWithData_WhenReadingFollowings()
    {
        var relationApi = TestDoubles.Api<IRelationApi>(
            (method, _) =>
                method.Name == nameof(IRelationApi.GetFollowings)
                    ? Task.FromResult(
                        new BiliApiResponse<GetFollowingsResponse>
                        {
                            Code = 1002,
                            Message = "关注列表失败",
                            Data = new GetFollowingsResponse
                            {
                                Total = 1,
                                List = [new UpInfo { Mid = 20002, Uname = "up" }],
                            },
                        }
                    )
                    : throw new InvalidOperationException($"Unexpected API: {method.Name}")
        );
        var liveApi = TestDoubles.Api<ILiveApi>(
            (method, _) =>
                method.Name == nameof(ILiveApi.GetAreaList)
                    ? Task.FromResult(
                        new BiliApiResponse<GetArteaListResponse>
                        {
                            Code = 0,
                            Data = new GetArteaListResponse { Data = [] },
                        }
                    )
                    : throw new InvalidOperationException($"Unexpected API: {method.Name}")
        );
        var service = CreateService(
            liveApi,
            relationApi,
            new LiveLotteryTaskOptions { AutoGroupFollowings = true }
        );

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.TianXuan(TestDoubles.Cookie())
        );

        Assert.Contains("关注列表失败", exception.Message);
    }

    [Fact]
    public async Task GroupFollowing_ShouldRejectErrorResponseWithData_WhenReadingFollowings()
    {
        var relationApi = CreateRelationApiForJoinedFollow(
            followings: new BiliApiResponse<GetFollowingsResponse>
            {
                Code = 1003,
                Message = "关注列表失败",
                Data = new GetFollowingsResponse
                {
                    Total = 1,
                    List = [new UpInfo { Mid = 20002, Uname = "up" }],
                },
            }
        );
        var service = await CreateServiceWithJoinedFollowAsync(relationApi);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.GroupFollowing(TestDoubles.Cookie())
        );

        Assert.Contains("关注列表失败", exception.Message);
    }

    [Fact]
    public async Task GroupFollowing_ShouldRejectErrorResponseWithData_WhenReadingTags()
    {
        var relationApi = CreateRelationApiForJoinedFollow(
            tags: new BiliApiResponse<List<TagDto>>
            {
                Code = 1004,
                Message = "分组查询失败",
                Data = [new TagDto { Tagid = 1, Name = "天选时刻" }],
            }
        );
        var service = await CreateServiceWithJoinedFollowAsync(relationApi);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.GroupFollowing(TestDoubles.Cookie())
        );

        Assert.Contains("分组查询失败", exception.Message);
    }

    [Fact]
    public async Task GroupFollowing_ShouldRejectErrorResponseWithData_WhenCreatingTag()
    {
        var relationApi = CreateRelationApiForJoinedFollow(
            tags: new BiliApiResponse<List<TagDto>> { Code = 0, Data = [] },
            createTag: new BiliApiResponse<CreateTagResponse>
            {
                Code = 1005,
                Message = "创建分组失败",
                Data = new CreateTagResponse { Tagid = 1 },
            }
        );
        var service = await CreateServiceWithJoinedFollowAsync(relationApi);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.GroupFollowing(TestDoubles.Cookie())
        );

        Assert.Contains("创建分组失败", exception.Message);
    }

    private static async Task<LiveDomainService> CreateServiceWithJoinedFollowAsync(
        IRelationApi relationApi
    )
    {
        var liveApi = TestDoubles.Api<ILiveApi>(
            (method, _) =>
                method.Name switch
                {
                    nameof(ILiveApi.CheckTianXuan) => Task.FromResult(
                        new BiliApiResponse<CheckTianXuanDto>
                        {
                            Code = 0,
                            Data = CreateCheckResult(),
                        }
                    ),
                    nameof(ILiveApi.Join) => Task.FromResult(
                        new BiliApiResponse<JoinTianXuanResponse> { Code = 0 }
                    ),
                    _ => throw new InvalidOperationException($"Unexpected API: {method.Name}"),
                }
        );
        var service = CreateService(liveApi, relationApi);
        await service.TryJoinTianXuan(CreateTarget(), TestDoubles.Cookie());
        return service;
    }

    private static IRelationApi CreateRelationApiForJoinedFollow(
        BiliApiResponse<GetFollowingsResponse>? followings = null,
        BiliApiResponse<List<TagDto>>? tags = null,
        BiliApiResponse<CreateTagResponse>? createTag = null
    )
    {
        return TestDoubles.Api<IRelationApi>(
            (method, _) =>
                method.Name switch
                {
                    nameof(IRelationApi.GetFollowings) => Task.FromResult(
                        followings
                            ?? new BiliApiResponse<GetFollowingsResponse>
                            {
                                Code = 0,
                                Data = new GetFollowingsResponse
                                {
                                    Total = 1,
                                    List = [new UpInfo { Mid = 20002, Uname = "up" }],
                                },
                            }
                    ),
                    nameof(IRelationApi.GetTags) => Task.FromResult(
                        tags ?? new BiliApiResponse<List<TagDto>> { Code = 0, Data = [] }
                    ),
                    nameof(IRelationApi.CreateTag) => Task.FromResult(
                        createTag
                            ?? new BiliApiResponse<CreateTagResponse>
                            {
                                Code = 0,
                                Data = new CreateTagResponse { Tagid = 1 },
                            }
                    ),
                    nameof(IRelationApi.CopyUpsToGroup) => Task.FromResult(
                        new BiliApiResponse { Code = 0 }
                    ),
                    _ => throw new InvalidOperationException($"Unexpected API: {method.Name}"),
                }
        );
    }

    private static LiveDomainService CreateService(
        ILiveApi liveApi,
        IRelationApi? relationApi = null,
        LiveLotteryTaskOptions? lotteryOptions = null,
        Silver2CoinTaskOptions? silver2CoinOptions = null,
        ILogger<LiveDomainService>? logger = null
    )
    {
        return new LiveDomainService(
            logger ?? NullLogger<LiveDomainService>.Instance,
            liveApi,
            relationApi
                ?? TestDoubles.Api<IRelationApi>(
                    (method, _) =>
                        throw new InvalidOperationException($"Unexpected API: {method.Name}")
                ),
            TestDoubles.Api<ILiveTraceApi>(
                (method, _) => throw new InvalidOperationException($"Unexpected API: {method.Name}")
            ),
            new TestDoubles.OptionsMonitor<DailyTaskOptions>(new()),
            new TestDoubles.OptionsMonitor<LiveLotteryTaskOptions>(lotteryOptions ?? new()),
            new TestDoubles.OptionsMonitor<LiveFansMedalTaskOptions>(new()),
            new TestDoubles.OptionsMonitor<SecurityOptions>(new()),
            new TestDoubles.OptionsMonitor<Silver2CoinTaskOptions>(silver2CoinOptions ?? new()),
            TestDoubles.Api<IUpInfoApi>(
                (method, _) => throw new InvalidOperationException($"Unexpected API: {method.Name}")
            ),
            new ImmediateTaskDelay()
        );
    }

    private sealed class ImmediateTaskDelay : ITaskDelay
    {
        public Task Delay(TimeSpan delay, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }

    private static ListItemDto CreateTarget() =>
        new()
        {
            Roomid = 1,
            Uid = 20002,
            Title = "直播间",
            Uname = "主播",
            Parent_name = "分区",
        };

    private static CheckTianXuanDto CreateCheckResult() =>
        new()
        {
            Id = 1,
            Status = TianXuanStatus.Enable,
            Award_name = "奖品",
            Require_type = RequireType.Follow,
        };

    private sealed class ListLogger<T> : ILogger<T>
    {
        public List<string> Messages { get; } = [];

        public IDisposable BeginScope<TState>(TState state)
            where TState : notnull => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter
        ) => Messages.Add(formatter(state, exception));

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();

            public void Dispose() { }
        }
    }
}
