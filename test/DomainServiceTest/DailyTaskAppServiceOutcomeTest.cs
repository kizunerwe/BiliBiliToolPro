using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
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

public sealed class DailyTaskAppServiceOutcomeTest
{
    [Fact]
    public async Task StatusFailure_ShouldStillRunCoinAndVipSteps()
    {
        await GlobalServiceProviderTestLock.Gate.WaitAsync();
        try
        {
            Program.CreateHost(new[] { "--ENVIRONMENT=Development" });
            var dependencies = new Fakes
            {
                StatusException = new InvalidOperationException("状态接口失败"),
                VideoResult = TaskStepResult.Skip("状态不可用"),
                CoinResult = TaskStepResult.Skip("余额不足"),
                VipResult = TaskStepResult.Skip("非年度会员"),
            };

            var service = CreateService(dependencies);

            await Assert.ThrowsAsync<TaskExecutionException>(() => service.DoTaskAsync());

            Assert.Equal(1, dependencies.CoinCalls);
            Assert.Equal(1, dependencies.VipCalls);
        }
        finally
        {
            GlobalServiceProviderTestLock.Gate.Release();
        }
    }

    [Fact]
    public async Task ArticleFailure_ShouldStillRunVideoCoin_AndPreserveFailure()
    {
        await GlobalServiceProviderTestLock.Gate.WaitAsync();
        try
        {
            Program.CreateHost(new[] { "--ENVIRONMENT=Development" });
            var dependencies = new Fakes
            {
                ArticleResult = TaskStepResult.Fail("专栏接口拒绝"),
                CoinResult = TaskStepResult.Success(),
                VipResult = TaskStepResult.Skip("已领取"),
            };

            var service = CreateService(dependencies, donateArticle: true);

            var exception = await Assert.ThrowsAsync<TaskExecutionException>(() =>
                service.DoTaskAsync()
            );

            Assert.Equal(1, dependencies.CoinCalls);
            Assert.Contains("专栏接口拒绝", exception.Message);
        }
        finally
        {
            GlobalServiceProviderTestLock.Gate.Release();
        }
    }

    [Fact]
    public async Task AllSkippedSteps_ShouldCompleteNormally()
    {
        await GlobalServiceProviderTestLock.Gate.WaitAsync();
        try
        {
            Program.CreateHost(new[] { "--ENVIRONMENT=Development" });
            var dependencies = new Fakes
            {
                VideoResult = TaskStepResult.Skip("已完成"),
                CoinResult = TaskStepResult.Skip("余额不足"),
                VipResult = TaskStepResult.Skip("已领取"),
            };

            await CreateService(dependencies).DoTaskAsync();
        }
        finally
        {
            GlobalServiceProviderTestLock.Gate.Release();
        }
    }

    [Fact]
    public async Task MissingOnlyBuvid4_ShouldRefreshCookie()
    {
        await GlobalServiceProviderTestLock.Gate.WaitAsync();
        try
        {
            Program.CreateHost(new[] { "--ENVIRONMENT=Development" });
            var dependencies = new Fakes
            {
                VideoResult = TaskStepResult.Skip("已完成"),
                CoinResult = TaskStepResult.Skip("余额不足"),
                VipResult = TaskStepResult.Skip("已领取"),
            };

            await CreateService(
                    dependencies,
                    cookie: "DedeUserID=10001;SESSDATA=sess;bili_jct=csrf;buvid3=buvid;b_nut=nut"
                )
                .DoTaskAsync();

            Assert.Equal(1, dependencies.SetCookieCalls);
            Assert.Equal(1, dependencies.SaveCookieCalls);
        }
        finally
        {
            GlobalServiceProviderTestLock.Gate.Release();
        }
    }

    [Fact]
    public async Task MissingBuvid3_ShouldRefreshPersistAndUseUpdatedCookieOnlyOnce()
    {
        await GlobalServiceProviderTestLock.Gate.WaitAsync();
        try
        {
            Program.CreateHost(new[] { "--ENVIRONMENT=Development" });
            var dependencies = new Fakes
            {
                VideoResult = TaskStepResult.Skip("已完成"),
                CoinResult = TaskStepResult.Skip("余额不足"),
                VipResult = TaskStepResult.Skip("已领取"),
            };

            await CreateService(
                    dependencies,
                    cookie: "DedeUserID=10001;SESSDATA=sess;bili_jct=csrf;buvid4=buvid4;b_nut=nut"
                )
                .DoTaskAsync();

            Assert.Equal(1, dependencies.SetCookieCalls);
            Assert.Equal(1, dependencies.SaveCookieCalls);
            Assert.NotNull(dependencies.VideoCookie);
            Assert.Equal(
                "refreshed-buvid3",
                dependencies.VideoCookie.CookieItemDictionary["buvid3"]
            );
        }
        finally
        {
            GlobalServiceProviderTestLock.Gate.Release();
        }
    }

    [Fact]
    public async Task MissingBNut_ShouldRefreshCookie()
    {
        await GlobalServiceProviderTestLock.Gate.WaitAsync();
        try
        {
            Program.CreateHost(new[] { "--ENVIRONMENT=Development" });
            var dependencies = new Fakes
            {
                VideoResult = TaskStepResult.Skip("已完成"),
                CoinResult = TaskStepResult.Skip("余额不足"),
                VipResult = TaskStepResult.Skip("已领取"),
            };

            await CreateService(
                    dependencies,
                    cookie: "DedeUserID=10001;SESSDATA=sess;bili_jct=csrf;buvid3=buvid;buvid4=buvid4"
                )
                .DoTaskAsync();

            Assert.Equal(1, dependencies.SetCookieCalls);
            Assert.Equal(1, dependencies.SaveCookieCalls);
        }
        finally
        {
            GlobalServiceProviderTestLock.Gate.Release();
        }
    }

    [Fact]
    public async Task CompleteDeviceCookie_ShouldStillRefreshCookie()
    {
        await GlobalServiceProviderTestLock.Gate.WaitAsync();
        try
        {
            Program.CreateHost(new[] { "--ENVIRONMENT=Development" });
            var dependencies = new Fakes
            {
                VideoResult = TaskStepResult.Skip("已完成"),
                CoinResult = TaskStepResult.Skip("余额不足"),
                VipResult = TaskStepResult.Skip("已领取"),
            };

            await CreateService(dependencies).DoTaskAsync();

            Assert.Equal(1, dependencies.SetCookieCalls);
            Assert.Equal(1, dependencies.SaveCookieCalls);
        }
        finally
        {
            GlobalServiceProviderTestLock.Gate.Release();
        }
    }

    private static DailyTaskAppService CreateService(
        Fakes fakes,
        bool donateArticle = false,
        string? cookie = null
    )
    {
        var options = new DailyTaskOptions
        {
            IsEnable = true,
            IsWatchVideo = false,
            IsShareVideo = false,
            IsDonateCoinForArticle = donateArticle,
        };
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["BiliBiliCookies:0"] =
                        cookie
                        ?? "DedeUserID=10001;SESSDATA=sess;bili_jct=csrf;buvid3=buvid;buvid4=buvid4;b_nut=nut",
                }
            )
            .Build();

        return new DailyTaskAppService(
            NullLogger<DailyTaskAppService>.Instance,
            fakes.Account,
            fakes.Video,
            fakes.Article,
            fakes.Donate,
            fakes.Vip,
            new TestOptionsMonitor<DailyTaskOptions>(options),
            fakes.Login,
            configuration,
            new CookieStrFactory<BiliCookie>(configuration)
        );
    }

    private sealed class Fakes
    {
        public int CoinCalls { get; private set; }
        public int VipCalls { get; private set; }
        public int SetCookieCalls { get; private set; }
        public int SaveCookieCalls { get; private set; }
        public BiliCookie? VideoCookie { get; private set; }
        public Exception? StatusException { get; init; }
        public TaskStepResult VideoResult { get; init; } = TaskStepResult.Skip("未配置");
        public TaskStepResult ArticleResult { get; init; } = TaskStepResult.Skip("未配置");
        public TaskStepResult CoinResult { get; init; } = TaskStepResult.Skip("未配置");
        public TaskStepResult VipResult { get; init; } = TaskStepResult.Skip("未配置");

        public IAccountDomainService Account { get; }
        public IVideoDomainService Video { get; }
        public IArticleDomainService Article { get; }
        public IDonateCoinDomainService Donate { get; }
        public IVipPrivilegeDomainService Vip { get; }
        public ILoginDomainService Login { get; }

        public Fakes()
        {
            Account = new FakeAccount(this);
            Video = new FakeVideo(this);
            Article = new FakeArticle(this);
            Donate = new FakeDonate(this);
            Vip = new FakeVip(this);
            Login = new FakeLogin(this);
        }

        private sealed class FakeAccount(Fakes owner) : IAccountDomainService
        {
            public Task<UserInfo> LoginByCookie(BiliCookie cookie) =>
                Task.FromResult(
                    new UserInfo
                    {
                        Mid = 10001,
                        IsLogin = true,
                        Level_info = new LevelInfo { Current_level = 5 },
                        Wbi_img = new WbiImg
                        {
                            img_url = "https://example.com/img.png",
                            sub_url = "https://example.com/sub.png",
                        },
                    }
                );

            public Task<DailyTaskInfo> GetDailyTaskStatus(BiliCookie ck) =>
                owner.StatusException is null
                    ? Task.FromResult(new DailyTaskInfo())
                    : Task.FromException<DailyTaskInfo>(owner.StatusException);

            public Task UnfollowBatched(BiliCookie ck) => throw new NotImplementedException();

            public int CalculateUpgradeTime(UserInfo useInfo) => 0;
        }

        private sealed class FakeVideo(Fakes owner) : IVideoDomainService
        {
            public Task<TaskStepResult> WatchAndShareVideo(DailyTaskInfo status, BiliCookie ck)
            {
                owner.VideoCookie = ck;
                return Task.FromResult(owner.VideoResult);
            }

            public Task<VideoDetail> GetVideoDetail(string aid) =>
                throw new NotImplementedException();

            public Task<RankingInfo> GetRandomVideoOfRanking() =>
                throw new NotImplementedException();

            public Task<UpVideoInfo?> GetRandomVideoOfUp(long upId, int total, BiliCookie ck) =>
                throw new NotImplementedException();

            public Task<IReadOnlyList<UpVideoInfo>> GetVideosOfUp(
                long upId,
                int pageNumber,
                int pageSize,
                BiliCookie ck
            ) => throw new NotImplementedException();

            public Task<int> GetVideoCountOfUp(long upId, BiliCookie ck) =>
                throw new NotImplementedException();

            public Task<bool> WatchVideoForUpFriendlyMode(
                VideoDetail video,
                BiliCookie ck,
                CancellationToken cancellationToken
            ) => throw new NotImplementedException();

            public Task WatchVideo(VideoInfoDto videoInfo, BiliCookie ck) =>
                throw new NotImplementedException();

            public Task ShareVideo(VideoInfoDto videoInfo, BiliCookie ck) =>
                throw new NotImplementedException();
        }

        private sealed class FakeArticle(Fakes owner) : IArticleDomainService
        {
            public Task<TaskStepResult> AddCoinForArticles(BiliCookie ck) =>
                Task.FromResult(owner.ArticleResult);

            public Task<bool> AddCoinForArticle(long cvid, long mid, BiliCookie ck) =>
                Task.FromResult(false);

            public Task LikeArticle(long cvid, BiliCookie ck) => Task.CompletedTask;
        }

        private sealed class FakeDonate(Fakes owner) : IDonateCoinDomainService
        {
            public Task<TaskStepResult> AddCoinsForVideos(BiliCookie ck)
            {
                owner.CoinCalls++;
                return Task.FromResult(owner.CoinResult);
            }

            public Task<UpVideoInfo?> TryGetCanDonatedVideo(BiliCookie ck) =>
                Task.FromResult<UpVideoInfo?>(null);

            public Task<bool> DoAddCoinForVideo(
                UpVideoInfo video,
                bool select_like,
                BiliCookie ck
            ) => Task.FromResult(false);
        }

        private sealed class FakeVip(Fakes owner) : IVipPrivilegeDomainService
        {
            public Task<TaskStepResult> ReceiveVipPrivilege(UserInfo userInfo, BiliCookie ck)
            {
                owner.VipCalls++;
                return Task.FromResult(owner.VipResult);
            }
        }

        private sealed class FakeLogin(Fakes owner) : ILoginDomainService
        {
            public Task<BiliCookie> LoginByQrCodeAsync(CancellationToken cancellationToken) =>
                throw new NotImplementedException();

            public Task<PassportTvLoginResult> LoginByTvQrCodeAsync(
                CancellationToken cancellationToken
            ) => throw new NotImplementedException();

            public Task<BiliCookie> SetCookieAsync(
                BiliCookie cookie,
                CancellationToken cancellationToken
            )
            {
                owner.SetCookieCalls++;
                cookie.CookieItemDictionary["buvid3"] = "refreshed-buvid3";
                return Task.FromResult(cookie);
            }

            public Task<string?> TryGetAccessKeyByTvQrCodeAsync(
                CancellationToken cancellationToken
            ) => throw new NotImplementedException();

            public Task SaveCookieToJsonFileAsync(
                BiliCookie ckInfo,
                CancellationToken cancellationToken
            )
            {
                owner.SaveCookieCalls++;
                return Task.CompletedTask;
            }

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
    }

    private sealed class TestOptionsMonitor<T>(T value) : IOptionsMonitor<T>
    {
        public T CurrentValue => value;

        public T Get(string? name) => value;

        public IDisposable? OnChange(Action<T, string?> listener) => null;
    }
}
