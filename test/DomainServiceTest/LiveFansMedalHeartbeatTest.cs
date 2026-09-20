using System.Reflection;
using Microsoft.Extensions.Logging.Abstractions;
using Ray.BiliBiliTool.Agent.BiliBiliAgent.Dtos;
using Ray.BiliBiliTool.Agent.BiliBiliAgent.Dtos.Live;
using Ray.BiliBiliTool.Agent.BiliBiliAgent.Interfaces;
using Ray.BiliBiliTool.Config.Options;
using Ray.BiliBiliTool.DomainService;
using Ray.BiliBiliTool.DomainService.Dtos;
using Ray.BiliBiliTool.DomainService.Interfaces;

namespace DomainServiceTest;

public sealed class LiveFansMedalHeartbeatTest
{
    [Fact]
    public async Task FailureWithValidChain_ShouldAdoptChainAndRecoverSameSequence()
    {
        var trace = new TraceScenario(
            EnterResponse(interval: 9, key: "enter-key", rule: [0], timestamp: 100),
            FailureResponse(interval: 17, key: "next-key", rule: [2], timestamp: 200),
            SuccessResponse(interval: 23, key: "final-key", rule: [1], timestamp: 300)
        );
        var delay = new RecordingDelay();
        var service = CreateService(trace.Api, delay, heartBeatNumber: 2);

        var result = await service.SendHeartBeatToFansMedalLive(TestDoubles.Cookie());

        Assert.Equal(TaskStepStatus.Succeeded, result.Status);
        Assert.Single(trace.EnterRequests);
        Assert.Equal(2, trace.HeartBeatRequests.Count);
        Assert.Equal(trace.HeartBeatRequests[0].Id, trace.HeartBeatRequests[1].Id);
        Assert.Equal(200, trace.HeartBeatRequests[1].Ets);
        Assert.Equal("next-key", trace.HeartBeatRequests[1].Benchmark);
        Assert.Equal(17, trace.HeartBeatRequests[1].Time);
        Assert.Equal(2, delay.Seconds.Count);
        Assert.InRange(delay.Seconds[0], 8, 9);
        Assert.InRange(delay.Seconds[1], 16, 17);
    }

    [Fact]
    public async Task MissingChain_ShouldReenterWithoutResettingTotalProgress()
    {
        var trace = new TraceScenario(
            EnterResponse(interval: 5, key: "first-key", rule: [0], timestamp: 100),
            new BiliApiResponse<HeartBeatResponse> { Code = -1, Message = "empty" },
            EnterResponse(interval: 7, key: "rebuilt-key", rule: [1], timestamp: 200)
        );
        var delay = new RecordingDelay();
        var service = CreateService(trace.Api, delay, heartBeatNumber: 2);

        var result = await service.SendHeartBeatToFansMedalLive(TestDoubles.Cookie());

        Assert.Equal(TaskStepStatus.Succeeded, result.Status);
        Assert.Equal(2, trace.EnterRequests.Count);
        Assert.Single(trace.HeartBeatRequests);
        Assert.All(trace.EnterRequests, request => Assert.Contains(",0,", request.Id));
        Assert.Equal(2, delay.Seconds.Count);
        Assert.All(delay.Seconds, seconds => Assert.InRange(seconds, 4, 5));
    }

    [Fact]
    public async Task CancellationDuringWait_ShouldEscapeAsCancellation()
    {
        using var cts = new CancellationTokenSource();
        var trace = new TraceScenario(
            EnterResponse(interval: 60, key: "key", rule: [0], timestamp: 100)
        );
        var delay = new CancelingDelay(cts);
        var service = CreateService(trace.Api, delay, heartBeatNumber: 2);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            service.SendHeartBeatToFansMedalLive(TestDoubles.Cookie(), cts.Token)
        );

        Assert.Single(trace.EnterRequests);
        Assert.Empty(trace.HeartBeatRequests);
    }

    [Fact]
    public async Task MissingChain_ShouldStopAfterLimitedRebuilds()
    {
        var trace = new TraceScenario(
            EmptyChainResponse(),
            EmptyChainResponse(),
            EmptyChainResponse()
        );
        var service = CreateService(trace.Api, new RecordingDelay(), heartBeatNumber: 1);

        var result = await service.SendHeartBeatToFansMedalLive(TestDoubles.Cookie());

        Assert.Equal(TaskStepStatus.Failed, result.Status);
        Assert.Equal(3, trace.EnterRequests.Count);
        Assert.Empty(trace.HeartBeatRequests);
    }

    [Fact]
    public async Task ZeroInterval_ShouldKeepDefaultInterval()
    {
        var trace = new TraceScenario(
            EnterResponse(interval: 0, key: "enter-key", rule: [0], timestamp: 100),
            SuccessResponse(interval: 0, key: "next-key", rule: [1], timestamp: 200)
        );
        var delay = new RecordingDelay();
        var service = CreateService(trace.Api, delay, heartBeatNumber: 2);

        var result = await service.SendHeartBeatToFansMedalLive(TestDoubles.Cookie());

        Assert.Equal(TaskStepStatus.Succeeded, result.Status);
        Assert.Single(trace.HeartBeatRequests);
        Assert.Equal(60, trace.HeartBeatRequests[0].Time);
        Assert.Single(delay.Seconds);
        Assert.InRange(delay.Seconds[0], 59, 60);
    }

    private static LiveDomainService CreateService(
        ILiveTraceApi traceApi,
        ITaskDelay delay,
        int heartBeatNumber
    )
    {
        var liveApi = TestDoubles.Api<ILiveApi>(
            (method, _) =>
                method.Name switch
                {
                    nameof(ILiveApi.GetMedalWall) => Task.FromResult(
                        new BiliApiResponse<MedalWallResponse>
                        {
                            Code = 0,
                            Data = new MedalWallResponse
                            {
                                List =
                                [
                                    new MedalWallDto
                                    {
                                        Target_name = "up",
                                        Link = "https://example.com",
                                        Medal_info = new MedalInfoDto
                                        {
                                            Medal_name = "medal",
                                            Target_id = 20002,
                                            Level = 1,
                                        },
                                    },
                                ],
                            },
                        }
                    ),
                    nameof(ILiveApi.GetLiveRoomInfo) => Task.FromResult(
                        new BiliApiResponse<GetLiveRoomInfoResponse>
                        {
                            Code = 0,
                            Data = new GetLiveRoomInfoResponse
                            {
                                Room_id = 10001,
                                Area_id = 2,
                                Parent_area_id = 1,
                                Live_Status = 1,
                                Uid = 20002,
                            },
                        }
                    ),
                    _ => throw new InvalidOperationException($"Unexpected API: {method.Name}"),
                }
        );
        var upInfoApi = TestDoubles.Api<IUpInfoApi>(
            (method, _) =>
                method.Name == nameof(IUpInfoApi.GetSpaceInfo)
                    ? Task.FromResult(
                        new BiliApiResponse<GetSpaceInfoResponse>
                        {
                            Code = 0,
                            Data = new GetSpaceInfoResponse
                            {
                                Mid = 20002,
                                Name = "up",
                                Live_room = new SpaceLiveRoomInfoDto
                                {
                                    Title = "live",
                                    Roomid = 10001,
                                },
                            },
                        }
                    )
                    : throw new InvalidOperationException($"Unexpected API: {method.Name}")
        );

        return new LiveDomainService(
            NullLogger<LiveDomainService>.Instance,
            liveApi,
            TestDoubles.Api<IRelationApi>(
                (method, _) => throw new InvalidOperationException($"Unexpected API: {method.Name}")
            ),
            traceApi,
            new TestDoubles.OptionsMonitor<DailyTaskOptions>(new()),
            new TestDoubles.OptionsMonitor<LiveLotteryTaskOptions>(new()),
            new TestDoubles.OptionsMonitor<LiveFansMedalTaskOptions>(
                new() { HeartBeatNumber = heartBeatNumber, HeartBeatSendGiveUpThreshold = 5 }
            ),
            new TestDoubles.OptionsMonitor<SecurityOptions>(new() { UserAgent = "test-agent" }),
            new TestDoubles.OptionsMonitor<Silver2CoinTaskOptions>(new()),
            upInfoApi,
            delay
        );
    }

    private static BiliApiResponse<HeartBeatResponse> EnterResponse(
        int interval,
        string key,
        List<int> rule,
        long timestamp
    ) => SuccessResponse(interval, key, rule, timestamp);

    private static BiliApiResponse<HeartBeatResponse> SuccessResponse(
        int interval,
        string key,
        List<int> rule,
        long timestamp
    ) => new() { Code = 0, Data = Chain(interval, key, rule, timestamp) };

    private static BiliApiResponse<HeartBeatResponse> FailureResponse(
        int interval,
        string key,
        List<int> rule,
        long timestamp
    ) =>
        new()
        {
            Code = -1,
            Message = "time check failed",
            Data = Chain(interval, key, rule, timestamp),
        };

    private static BiliApiResponse<HeartBeatResponse> EmptyChainResponse() =>
        new() { Code = -1, Message = "empty" };

    private static HeartBeatResponse Chain(
        int interval,
        string key,
        List<int> rule,
        long timestamp
    ) =>
        new()
        {
            Heartbeat_interval = interval,
            Secret_key = key,
            Secret_rule = rule,
            Timestamp = timestamp,
        };

    private sealed class TraceScenario(params BiliApiResponse<HeartBeatResponse>[] responses)
    {
        private readonly Queue<BiliApiResponse<HeartBeatResponse>> _responses = new(responses);

        public List<EnterRoomRequest> EnterRequests { get; } = [];
        public List<HeartBeatRequest> HeartBeatRequests { get; } = [];

        public ILiveTraceApi Api => TestDoubles.Api<ILiveTraceApi>(Handle);

        private object Handle(MethodInfo method, object?[] args)
        {
            switch (method.Name)
            {
                case nameof(ILiveTraceApi.EnterRoom):
                    EnterRequests.Add((EnterRoomRequest)args[0]!);
                    break;
                case nameof(ILiveTraceApi.HeartBeat):
                    HeartBeatRequests.Add((HeartBeatRequest)args[0]!);
                    break;
                default:
                    throw new InvalidOperationException($"Unexpected API: {method.Name}");
            }

            return Task.FromResult(_responses.Dequeue());
        }
    }

    private sealed class RecordingDelay : ITaskDelay
    {
        public List<int> Seconds { get; } = [];

        public Task Delay(TimeSpan delay, CancellationToken cancellationToken)
        {
            Seconds.Add((int)delay.TotalSeconds);
            cancellationToken.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }
    }

    private sealed class CancelingDelay(CancellationTokenSource cancellation) : ITaskDelay
    {
        public Task Delay(TimeSpan delay, CancellationToken cancellationToken)
        {
            cancellation.Cancel();
            return Task.FromCanceled(cancellationToken);
        }
    }
}
