using Microsoft.Extensions.Logging.Abstractions;
using Ray.BiliBiliTool.Agent.BiliBiliAgent.Dtos;
using Ray.BiliBiliTool.Agent.BiliBiliAgent.Dtos.Live;
using Ray.BiliBiliTool.Agent.BiliBiliAgent.Interfaces;
using Ray.BiliBiliTool.Config.Options;
using Ray.BiliBiliTool.DomainService;
using Ray.BiliBiliTool.DomainService.Dtos;

namespace DomainServiceTest;

public sealed class LiveFansMedalOutcomeTest
{
    [Fact]
    public async Task NoEligibleLiveRoom_ShouldSkipAllThreeActions()
    {
        var service = CreateService();
        var cookie = TestDoubles.Cookie();

        var results = new[]
        {
            await service.SendDanmakuToFansMedalLive(cookie),
            await service.LikeFansMedalLive(cookie),
            await service.SendHeartBeatToFansMedalLive(cookie),
        };

        Assert.All(results, result => Assert.Equal(TaskStepStatus.Skipped, result.Status));
    }

    [Fact]
    public async Task MedalRoomLookupFailure_ShouldFailAllThreeActions()
    {
        var liveApi = TestDoubles.Api<ILiveApi>(
            (method, _) =>
                method.Name == nameof(ILiveApi.GetMedalWall)
                    ? Task.FromResult(
                        new BiliApiResponse<MedalWallResponse>
                        {
                            Code = 0,
                            Data = new MedalWallResponse
                            {
                                List =
                                [
                                    new MedalWallDto
                                    {
                                        Target_name = "主播",
                                        Link = "https://example.com",
                                        Medal_info = new MedalInfoDto
                                        {
                                            Medal_name = "粉丝牌",
                                            Target_id = 20002,
                                        },
                                    },
                                ],
                            },
                        }
                    )
                    : throw new InvalidOperationException($"Unexpected API: {method.Name}")
        );
        var upInfoApi = TestDoubles.Api<IUpInfoApi>(
            (method, _) =>
                method.Name == nameof(IUpInfoApi.GetSpaceInfo)
                    ? Task.FromResult(
                        new BiliApiResponse<GetSpaceInfoResponse>
                        {
                            Code = 1001,
                            Message = "空间信息失败",
                        }
                    )
                    : throw new InvalidOperationException($"Unexpected API: {method.Name}")
        );
        var service = CreateService(liveApi, upInfoApi);

        var results = new[]
        {
            await service.SendDanmakuToFansMedalLive(TestDoubles.Cookie()),
            await service.LikeFansMedalLive(TestDoubles.Cookie()),
            await service.SendHeartBeatToFansMedalLive(TestDoubles.Cookie()),
        };

        Assert.All(results, result => Assert.Equal(TaskStepStatus.Failed, result.Status));
    }

    private static LiveDomainService CreateService(
        ILiveApi? liveApi = null,
        IUpInfoApi? upInfoApi = null
    )
    {
        liveApi ??= TestDoubles.Api<ILiveApi>(
            (method, _) =>
                method.Name == nameof(ILiveApi.GetMedalWall)
                    ? Task.FromResult(
                        new BiliApiResponse<MedalWallResponse>
                        {
                            Code = 0,
                            Data = new MedalWallResponse { List = [] },
                        }
                    )
                    : throw new InvalidOperationException($"Unexpected API: {method.Name}")
        );
        upInfoApi ??= TestDoubles.Api<IUpInfoApi>(
            (method, _) => throw new InvalidOperationException($"Unexpected API: {method.Name}")
        );
        return new LiveDomainService(
            NullLogger<LiveDomainService>.Instance,
            liveApi,
            TestDoubles.Api<IRelationApi>(
                (method, _) => throw new InvalidOperationException($"Unexpected API: {method.Name}")
            ),
            TestDoubles.Api<ILiveTraceApi>(
                (method, _) => throw new InvalidOperationException($"Unexpected API: {method.Name}")
            ),
            new TestDoubles.OptionsMonitor<DailyTaskOptions>(new()),
            new TestDoubles.OptionsMonitor<LiveLotteryTaskOptions>(new()),
            new TestDoubles.OptionsMonitor<LiveFansMedalTaskOptions>(new()),
            new TestDoubles.OptionsMonitor<SecurityOptions>(new()),
            new TestDoubles.OptionsMonitor<Silver2CoinTaskOptions>(new()),
            upInfoApi,
            new ImmediateTaskDelay()
        );
    }

    private sealed class ImmediateTaskDelay : ITaskDelay
    {
        public Task Delay(TimeSpan delay, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }
}
