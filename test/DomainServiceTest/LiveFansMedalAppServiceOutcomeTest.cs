using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Ray.BiliBiliTool.Agent;
using Ray.BiliBiliTool.Agent.BiliBiliAgent.Dtos.Live;
using Ray.BiliBiliTool.Application;
using Ray.BiliBiliTool.Application.Contracts;
using Ray.BiliBiliTool.Config.Options;
using Ray.BiliBiliTool.DomainService.Dtos;
using Ray.BiliBiliTool.DomainService.Interfaces;
using Ray.BiliBiliTool.Infrastructure.Cookie;

namespace DomainServiceTest;

public sealed class LiveFansMedalAppServiceOutcomeTest
{
    public LiveFansMedalAppServiceOutcomeTest()
    {
        Program.CreateHost(new[] { "--ENVIRONMENT=Development" });
    }

    [Fact]
    public async Task FailedAction_ShouldNotPreventLaterLiveActions()
    {
        var live = new FakeLiveDomainService
        {
            DanmakuResult = TaskStepResult.Fail("弹幕接口拒绝"),
            LikeResult = TaskStepResult.Success(),
            HeartBeatResult = TaskStepResult.Skip("没有直播间"),
        };
        var service = CreateService(live);

        var exception = await Assert.ThrowsAsync<TaskExecutionException>(() =>
            service.DoTaskAsync()
        );

        Assert.Equal(1, live.DanmakuCalls);
        Assert.Equal(1, live.LikeCalls);
        Assert.Equal(1, live.HeartBeatCalls);
        Assert.Contains("弹幕接口拒绝", exception.Message);
    }

    [Fact]
    public async Task AllSkippedActions_ShouldCompleteNormally()
    {
        var live = new FakeLiveDomainService();

        await CreateService(live).DoTaskAsync();
    }

    private static LiveFansMedalAppService CreateService(FakeLiveDomainService live)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["BiliBiliCookies:0"] =
                        "DedeUserID=10001;SESSDATA=sess;bili_jct=csrf;buvid3=buvid",
                }
            )
            .Build();
        return new LiveFansMedalAppService(
            NullLogger<LiveFansMedalAppService>.Instance,
            new TestOptionsMonitor<LiveFansMedalTaskOptions>(
                new LiveFansMedalTaskOptions { IsEnable = true }
            ),
            live,
            new CookieStrFactory<BiliCookie>(configuration)
        );
    }

    private sealed class FakeLiveDomainService : ILiveDomainService
    {
        public TaskStepResult DanmakuResult { get; init; } = TaskStepResult.Skip("无目标");
        public TaskStepResult LikeResult { get; init; } = TaskStepResult.Skip("无目标");
        public TaskStepResult HeartBeatResult { get; init; } = TaskStepResult.Skip("无目标");
        public int DanmakuCalls { get; private set; }
        public int LikeCalls { get; private set; }
        public int HeartBeatCalls { get; private set; }

        public Task<TaskStepResult> SendDanmakuToFansMedalLive(
            BiliCookie ck,
            CancellationToken cancellationToken = default
        )
        {
            DanmakuCalls++;
            return Task.FromResult(DanmakuResult);
        }

        public Task<TaskStepResult> LikeFansMedalLive(
            BiliCookie ck,
            CancellationToken cancellationToken = default
        )
        {
            LikeCalls++;
            return Task.FromResult(LikeResult);
        }

        public Task<TaskStepResult> SendHeartBeatToFansMedalLive(
            BiliCookie ck,
            CancellationToken cancellationToken = default
        )
        {
            HeartBeatCalls++;
            return Task.FromResult(HeartBeatResult);
        }

        public Task LiveSign(BiliCookie ck) => throw new NotImplementedException();

        public Task<bool> ExchangeSilver2Coin(BiliCookie ck) => throw new NotImplementedException();

        public Task TianXuan(BiliCookie ck) => throw new NotImplementedException();

        public Task TryJoinTianXuan(ListItemDto target, BiliCookie ck) =>
            throw new NotImplementedException();

        public Task GroupFollowing(BiliCookie ck) => throw new NotImplementedException();
    }

    private sealed class TestOptionsMonitor<T>(T value) : IOptionsMonitor<T>
    {
        public T CurrentValue => value;

        public T Get(string? name) => value;

        public IDisposable? OnChange(Action<T, string?> listener) => null;
    }
}
