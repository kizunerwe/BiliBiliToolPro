using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Ray.BiliBiliTool.Agent;
using Ray.BiliBiliTool.Agent.BiliBiliAgent.Dtos;
using Ray.BiliBiliTool.Application;
using Ray.BiliBiliTool.Application.Contracts;
using Ray.BiliBiliTool.Config.Options;
using Ray.BiliBiliTool.DomainService.Dtos;
using Ray.BiliBiliTool.DomainService.Interfaces;
using Ray.BiliBiliTool.Infrastructure.Cookie;

namespace DomainServiceTest;

public sealed class ChargeTaskAppServiceOutcomeTest
{
    public ChargeTaskAppServiceOutcomeTest()
    {
        Program.CreateHost(new[] { "--ENVIRONMENT=Development" });
    }

    [Fact]
    public async Task FailedCharge_ShouldBecomeAnAccountTaskFailure()
    {
        var service = CreateService(TaskStepResult.Fail("业务拒绝"));

        var exception = await Assert.ThrowsAsync<TaskExecutionException>(() =>
            service.DoTaskAsync()
        );

        Assert.Contains("1 个账号执行失败", exception.Message);
    }

    [Fact]
    public async Task SkippedCharge_ShouldCompleteNormally()
    {
        var service = CreateService(TaskStepResult.Skip("不是月末"));

        await service.DoTaskAsync();
    }

    private static ChargeTaskAppService CreateService(TaskStepResult result)
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

        return new ChargeTaskAppService(
            NullLogger<ChargeTaskAppService>.Instance,
            new TestOptionsMonitor<ChargeTaskOptions>(new ChargeTaskOptions()),
            new FakeAccountDomainService(),
            new FakeChargeDomainService(result),
            new FakeLoginDomainService(),
            configuration,
            new CookieStrFactory<BiliCookie>(configuration)
        );
    }

    private sealed class FakeChargeDomainService(TaskStepResult result) : IChargeDomainService
    {
        public Task<TaskStepResult> Charge(UserInfo userInfo, BiliCookie ck) =>
            Task.FromResult(result);

        public Task ChargeComments(string token, BiliCookie ck) => Task.CompletedTask;
    }

    private sealed class FakeAccountDomainService : IAccountDomainService
    {
        public Task<UserInfo> LoginByCookie(BiliCookie cookie) =>
            Task.FromResult(
                new UserInfo
                {
                    Mid = 10001,
                    IsLogin = true,
                    Wbi_img = new WbiImg
                    {
                        img_url = "https://example.com/wbi/img.png",
                        sub_url = "https://example.com/wbi/sub.png",
                    },
                }
            );

        public Task<DailyTaskInfo> GetDailyTaskStatus(BiliCookie ck) =>
            throw new NotImplementedException();

        public Task UnfollowBatched(BiliCookie ck) => throw new NotImplementedException();

        public int CalculateUpgradeTime(UserInfo useInfo) => throw new NotImplementedException();
    }

    private sealed class FakeLoginDomainService : ILoginDomainService
    {
        public Task<BiliCookie> LoginByQrCodeAsync(CancellationToken cancellationToken) =>
            throw new NotImplementedException();

        public Task<PassportTvLoginResult> LoginByTvQrCodeAsync(
            CancellationToken cancellationToken
        ) => throw new NotImplementedException();

        public Task<BiliCookie> SetCookieAsync(
            BiliCookie cookie,
            CancellationToken cancellationToken
        ) => throw new NotImplementedException();

        public Task<string?> TryGetAccessKeyByTvQrCodeAsync(CancellationToken cancellationToken) =>
            throw new NotImplementedException();

        public Task SaveCookieToJsonFileAsync(
            BiliCookie ckInfo,
            CancellationToken cancellationToken
        ) => throw new NotImplementedException();

        public Task SaveAccessKeyToJsonFileAsync(
            string userId,
            string accessKey,
            CancellationToken cancellationToken
        ) => throw new NotImplementedException();

        public Task<bool> SaveAccessKeyToQingLongAsync(
            string userId,
            string accessKey,
            CancellationToken cancellationToken
        ) => throw new NotImplementedException();

        public Task<bool> SaveCookieToQinLongAsync(
            BiliCookie ckInfo,
            CancellationToken cancellationToken
        ) => throw new NotImplementedException();
    }

    private sealed class TestOptionsMonitor<T>(T value) : IOptionsMonitor<T>
    {
        public T CurrentValue => value;

        public T Get(string? name) => value;

        public IDisposable? OnChange(Action<T, string?> listener) => null;
    }
}
