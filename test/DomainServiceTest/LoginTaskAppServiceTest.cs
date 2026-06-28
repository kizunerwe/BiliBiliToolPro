using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Ray.BiliBiliTool.Agent;
using Ray.BiliBiliTool.DomainService.Dtos;
using Ray.BiliBiliTool.DomainService.Interfaces;
using Ray.BiliBiliTool.Infrastructure;

namespace DomainServiceTest;

public class LoginTaskAppServiceTest
{
    [Fact]
    public async Task DoTaskAsync_ShouldPersistCookieAndAccessKey_FromSingleTvLogin()
    {
        await GlobalServiceProviderTestLock.Gate.WaitAsync();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["PlatformType"] = "Unknown" })
            .Build();
        var previousProvider = Global.ServiceProviderRoot;
        Global.ServiceProviderRoot = new ServiceCollection().AddLogging().BuildServiceProvider();

        try
        {
            var fakeLoginDomainService = new FakeLoginDomainService();
            var appService = new Ray.BiliBiliTool.Application.LoginTaskAppService(
                configuration,
                NullLogger<Ray.BiliBiliTool.Application.LoginTaskAppService>.Instance,
                fakeLoginDomainService
            );

            await appService.DoTaskAsync();

            Assert.True(fakeLoginDomainService.LoginByTvQrCodeCalled);
            Assert.True(fakeLoginDomainService.SetCookieCalled);
            Assert.True(fakeLoginDomainService.SaveCookieCalled);
            Assert.True(fakeLoginDomainService.SaveAccessKeyCalled);
            Assert.Equal("access-key-demo", fakeLoginDomainService.SavedAccessKey);
        }
        finally
        {
            Global.ServiceProviderRoot = previousProvider;
            GlobalServiceProviderTestLock.Gate.Release();
        }
    }

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
            var appService = new Ray.BiliBiliTool.Application.LoginTaskAppService(
                configuration,
                NullLogger<Ray.BiliBiliTool.Application.LoginTaskAppService>.Instance,
                fakeLoginDomainService
            );

            await appService.DoTaskAsync();

            Assert.True(fakeLoginDomainService.SaveCookieToQingLongCalled);
            Assert.True(fakeLoginDomainService.SaveAccessKeyToQingLongCalled);
            Assert.Equal("access-key-demo", fakeLoginDomainService.SavedAccessKey);
            Assert.False(fakeLoginDomainService.SaveAccessKeyCalled);
        }
        finally
        {
            Global.ServiceProviderRoot = previousProvider;
            GlobalServiceProviderTestLock.Gate.Release();
        }
    }

    private sealed class FakeLoginDomainService : ILoginDomainService
    {
        public bool LoginByTvQrCodeCalled { get; private set; }
        public bool SetCookieCalled { get; private set; }
        public bool SaveCookieCalled { get; private set; }
        public bool SaveCookieToQingLongCalled { get; private set; }
        public bool SaveAccessKeyCalled { get; private set; }
        public bool SaveAccessKeyToQingLongCalled { get; private set; }
        public string? SavedAccessKey { get; private set; }

        public Task<BiliCookie> LoginByQrCodeAsync(CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task<PassportTvLoginResult> LoginByTvQrCodeAsync(CancellationToken cancellationToken)
        {
            LoginByTvQrCodeCalled = true;
            return Task.FromResult(
                new PassportTvLoginResult(
                    new BiliCookie(
                        new Dictionary<string, string>
                        {
                            ["DedeUserID"] = "565140580",
                            ["SESSDATA"] = "sess",
                            ["bili_jct"] = "csrf",
                        }
                    ),
                    "access-key-demo"
                )
            );
        }

        public Task<BiliCookie> SetCookieAsync(
            BiliCookie cookie,
            CancellationToken cancellationToken
        )
        {
            SetCookieCalled = true;
            cookie.CookieItemDictionary["buvid3"] = "buvid";
            return Task.FromResult(cookie);
        }

        public Task<string?> TryGetAccessKeyByTvQrCodeAsync(CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task SaveCookieToJsonFileAsync(
            BiliCookie ckInfo,
            CancellationToken cancellationToken
        )
        {
            SaveCookieCalled = true;
            return Task.CompletedTask;
        }

        public Task SaveAccessKeyToJsonFileAsync(
            string userId,
            string accessKey,
            CancellationToken cancellationToken
        )
        {
            SaveAccessKeyCalled = true;
            SavedAccessKey = accessKey;
            return Task.CompletedTask;
        }

        public Task<bool> SaveCookieToQinLongAsync(
            BiliCookie ckInfo,
            CancellationToken cancellationToken
        )
        {
            SaveCookieToQingLongCalled = true;
            return Task.FromResult(true);
        }

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
}
