using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Ray.BiliBiliTool.Agent;
using Ray.BiliBiliTool.Agent.BiliBiliAgent.Dtos;
using Ray.BiliBiliTool.DomainService.Interfaces;
using Ray.BiliBiliTool.Infrastructure.Cookie;

namespace DomainServiceTest;

public class AccessKeyTaskAppServiceTest
{
    [Fact]
    public async Task DoTaskAsync_ShouldPersistAccessKeyToQingLong_WhenPlatformIsQingLong()
    {
        await GlobalServiceProviderTestLock.Gate.WaitAsync();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?> { ["PlatformType"] = "QingLong" }
            )
            .Build();
        var previousProvider = Global.ServiceProviderRoot;
        Global.ServiceProviderRoot = new ServiceCollection().AddLogging().BuildServiceProvider();

        try
        {
            var fakeLoginDomainService = new FakeLoginDomainService();
            var cookieFactory = new CookieStrFactory<BiliCookie>(
                new ConfigurationBuilder()
                    .AddInMemoryCollection(
                        new Dictionary<string, string?>
                        {
                            ["BiliBiliCookies:0"] =
                                "DedeUserID=565140580;SESSDATA=sess;bili_jct=csrf;buvid3=buvid",
                        }
                    )
                    .Build()
            );
            var appService = new Ray.BiliBiliTool.Application.AccessKeyTaskAppService(
                configuration,
                NullLogger<Ray.BiliBiliTool.Application.AccessKeyTaskAppService>.Instance,
                fakeLoginDomainService,
                new FakeAccountDomainService(),
                cookieFactory
            );

            await appService.DoTaskAsync();

            Assert.True(fakeLoginDomainService.TryGetAccessKeyCalled);
            Assert.True(fakeLoginDomainService.SaveAccessKeyToQingLongCalled);
            Assert.Equal("access-key-demo", fakeLoginDomainService.SavedAccessKey);
            Assert.False(fakeLoginDomainService.SaveAccessKeyToJsonCalled);
        }
        finally
        {
            Global.ServiceProviderRoot = previousProvider;
            GlobalServiceProviderTestLock.Gate.Release();
        }
    }

    private sealed class FakeLoginDomainService : ILoginDomainService
    {
        public bool TryGetAccessKeyCalled { get; private set; }
        public bool SaveAccessKeyToQingLongCalled { get; private set; }
        public bool SaveAccessKeyToJsonCalled { get; private set; }
        public string? SavedAccessKey { get; private set; }

        public Task<BiliCookie> LoginByQrCodeAsync(CancellationToken cancellationToken) =>
            throw new NotImplementedException();

        public Task<Ray.BiliBiliTool.DomainService.Dtos.PassportTvLoginResult> LoginByTvQrCodeAsync(
            CancellationToken cancellationToken
        ) => throw new NotImplementedException();

        public Task<BiliCookie> SetCookieAsync(
            BiliCookie cookie,
            CancellationToken cancellationToken
        ) => throw new NotImplementedException();

        public Task<string?> TryGetAccessKeyByTvQrCodeAsync(CancellationToken cancellationToken)
        {
            TryGetAccessKeyCalled = true;
            return Task.FromResult<string?>("access-key-demo");
        }

        public Task SaveCookieToJsonFileAsync(
            BiliCookie ckInfo,
            CancellationToken cancellationToken
        ) => throw new NotImplementedException();

        public Task SaveAccessKeyToJsonFileAsync(
            string userId,
            string accessKey,
            CancellationToken cancellationToken
        )
        {
            SaveAccessKeyToJsonCalled = true;
            SavedAccessKey = accessKey;
            return Task.CompletedTask;
        }

        public Task<bool> SaveCookieToQinLongAsync(
            BiliCookie ckInfo,
            CancellationToken cancellationToken
        ) => throw new NotImplementedException();

        public Task SaveAccessKeyToQingLongAsync(
            string userId,
            string accessKey,
            CancellationToken cancellationToken
        )
        {
            SaveAccessKeyToQingLongCalled = true;
            SavedAccessKey = accessKey;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeAccountDomainService : IAccountDomainService
    {
        public Task<UserInfo> LoginByCookie(BiliCookie cookie) =>
            Task.FromResult(
                new UserInfo
                {
                    IsLogin = true,
                    Mid = long.Parse(cookie.UserId),
                    Uname = "demo",
                    Wbi_img = new WbiImg
                    {
                        img_url = "https://i0.hdslb.com/bfs/wbi/a.png",
                        sub_url = "https://i0.hdslb.com/bfs/wbi/b.png",
                    },
                }
            );

        public Task<DailyTaskInfo> GetDailyTaskStatus(BiliCookie ck) =>
            throw new NotImplementedException();

        public Task UnfollowBatched(BiliCookie ck) => throw new NotImplementedException();

        public int CalculateUpgradeTime(UserInfo useInfo) => throw new NotImplementedException();
    }
}
