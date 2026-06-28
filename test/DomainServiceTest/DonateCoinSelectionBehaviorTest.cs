using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Ray.BiliBiliTool.Agent;
using Ray.BiliBiliTool.Agent.BiliBiliAgent.Dtos;
using Ray.BiliBiliTool.Agent.BiliBiliAgent.Dtos.Relation;
using Ray.BiliBiliTool.Agent.BiliBiliAgent.Dtos.Video;
using Ray.BiliBiliTool.Agent.BiliBiliAgent.Interfaces;
using Ray.BiliBiliTool.Config.Options;
using Ray.BiliBiliTool.DomainService;
using Ray.BiliBiliTool.DomainService.Dtos;
using Ray.BiliBiliTool.DomainService.Interfaces;

namespace DomainServiceTest;

public sealed class DonateCoinSelectionBehaviorTest
{
    [Fact]
    public async Task AddCoinsForVideos_ShouldProcessConfiguredUpFromOldToNewAndPersistBlacklist()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);

        try
        {
            var logger = new ListLogger<DonateCoinDomainService>();
            var stateStore = new DonateCoinSelectionStateStore(
                NullLogger<DonateCoinSelectionStateStore>.Instance,
                Path.Combine(tempDirectory, "donate-coin-state.json")
            );
            var videoDomainService = new FakeVideoDomainService();
            videoDomainService.SetConfigUpVideos(
                487417170,
                [
                    CreateVideo(102, "video-102"),
                    CreateVideo(101, "video-101"),
                    CreateVideo(100, "video-100"),
                ]
            );

            var videoApi = new FakeVideoApi();
            videoApi.SetDonatedCoins(100, 1);
            videoApi.SetDonatedCoins(101, 0);
            videoApi.SetDonatedCoins(102, 0);

            var domainService = CreateDomainService(
                logger,
                stateStore,
                videoDomainService,
                new FakeRelationApi(),
                videoApi,
                supportUpIds: "487417170",
                numberOfCoins: 2
            );

            await domainService.AddCoinsForVideos(CreateCookie("10001"));

            Assert.Contains("【视频来源】配置UP", logger.Messages);
            Assert.Contains(logger.Messages, x => x.Contains("【配置UP】487417170：0/3"));
            Assert.True(
                logger.Messages.IndexOf("【视频】video-101")
                    < logger.Messages.IndexOf("【视频】video-102")
            );

            var accountState = await stateStore.GetAccountStateAsync("10001");
            Assert.Contains(100, accountState.BlacklistedAids);
            Assert.Contains(101, accountState.BlacklistedAids);
            Assert.Contains(102, accountState.BlacklistedAids);
            Assert.Equal(3, accountState.ConfigUpProgressByUpId[487417170].RecordedVideoCount);
        }
        finally
        {
            if (Directory.Exists(tempDirectory))
            {
                Directory.Delete(tempDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public async Task AddCoinsForVideos_ShouldRetryFollowingsMultipleTimesBeforeFallingBack()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);

        try
        {
            var logger = new ListLogger<DonateCoinDomainService>();
            var stateStore = new DonateCoinSelectionStateStore(
                NullLogger<DonateCoinSelectionStateStore>.Instance,
                Path.Combine(tempDirectory, "donate-coin-state.json")
            );
            var videoDomainService = new FakeVideoDomainService();
            videoDomainService.SetRandomVideos(
                90001,
                [
                    CreateVideo(200, "video-200"),
                    CreateVideo(201, "video-201"),
                    CreateVideo(202, "video-202"),
                ]
            );

            var relationApi = new FakeRelationApi();
            relationApi.SetFollowings([90001]);

            var videoApi = new FakeVideoApi();
            videoApi.SetDonatedCoins(200, 2);
            videoApi.SetDonatedCoins(201, 2);
            videoApi.SetDonatedCoins(202, 0);

            var domainService = CreateDomainService(
                logger,
                stateStore,
                videoDomainService,
                relationApi,
                videoApi,
                supportUpIds: "",
                numberOfCoins: 1
            );

            await domainService.AddCoinsForVideos(CreateCookie("10001"));

            Assert.Contains("【视频来源】普通关注", logger.Messages);
            Assert.Contains("【视频】video-202", logger.Messages);
        }
        finally
        {
            if (Directory.Exists(tempDirectory))
            {
                Directory.Delete(tempDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public async Task AddCoinsForVideos_ShouldNotLogGenericRankingMissAfterRiskWarning()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);

        try
        {
            var logger = new ListLogger<DonateCoinDomainService>();
            var stateStore = new DonateCoinSelectionStateStore(
                NullLogger<DonateCoinSelectionStateStore>.Instance,
                Path.Combine(tempDirectory, "donate-coin-state.json")
            );
            var videoDomainService = new FakeVideoDomainService
            {
                RankingException = new InvalidOperationException("B站返回 -352"),
            };

            var domainService = CreateDomainService(
                logger,
                stateStore,
                videoDomainService,
                new FakeRelationApi(),
                new FakeVideoApi(),
                supportUpIds: "",
                numberOfCoins: 1
            );

            await domainService.AddCoinsForVideos(CreateCookie("10001"));

            Assert.Contains(
                logger.Messages,
                x => x.Contains("【选源】排行榜：获取失败，可能触发风控或验证码，已跳过。")
            );
            Assert.DoesNotContain(logger.Messages, x => x.Contains("【选源】排行榜未找到可投视频"));
        }
        finally
        {
            if (Directory.Exists(tempDirectory))
            {
                Directory.Delete(tempDirectory, recursive: true);
            }
        }
    }

    private static DonateCoinDomainService CreateDomainService(
        ILogger<DonateCoinDomainService> logger,
        DonateCoinSelectionStateStore stateStore,
        FakeVideoDomainService videoDomainService,
        FakeRelationApi relationApi,
        FakeVideoApi videoApi,
        string supportUpIds,
        int numberOfCoins
    )
    {
        var options = new TestOptionsMonitor<DailyTaskOptions>(
            new DailyTaskOptions
            {
                NumberOfCoins = numberOfCoins,
                NumberOfProtectedCoins = 0,
                SelectLike = false,
                SupportUpIds = supportUpIds,
            }
        );

        return new DonateCoinDomainService(
            logger,
            options,
            new FakeAccountApi(),
            new FakeCoinDomainService(),
            videoDomainService,
            relationApi,
            videoApi,
            stateStore
        );
    }

    private static BiliCookie CreateCookie(string userId)
    {
        return new BiliCookie(
            new Dictionary<string, string>
            {
                ["DedeUserID"] = userId,
                ["SESSDATA"] = "sess",
                ["bili_jct"] = "csrf",
                ["buvid3"] = "buvid",
            }
        );
    }

    private static UpVideoInfo CreateVideo(long aid, string title)
    {
        return new UpVideoInfo
        {
            Aid = aid,
            Bvid = $"BV{aid}",
            Title = title,
            Length = "00:15",
        };
    }

    private sealed class FakeCoinDomainService : ICoinDomainService
    {
        public Task<decimal> GetCoinBalance(BiliCookie ck) => Task.FromResult(10m);

        public Task<int> GetDonatedCoins(BiliCookie ck) => Task.FromResult(0);
    }

    private sealed class FakeVideoDomainService : IVideoDomainService
    {
        private readonly Dictionary<long, List<UpVideoInfo>> _configUpVideos = [];
        private readonly Dictionary<long, Queue<UpVideoInfo>> _randomVideos = [];

        public Exception? RankingException { get; set; }

        public void SetConfigUpVideos(long upId, List<UpVideoInfo> videos)
        {
            _configUpVideos[upId] = videos;
        }

        public void SetRandomVideos(long upId, List<UpVideoInfo> videos)
        {
            _randomVideos[upId] = new Queue<UpVideoInfo>(videos);
        }

        public Task<VideoDetail> GetVideoDetail(string aid)
        {
            return Task.FromResult(
                new VideoDetail
                {
                    Aid = long.Parse(aid),
                    Bvid = $"BV{aid}",
                    Title = $"video-{aid}",
                    Copyright = 1,
                }
            );
        }

        public Task<RankingInfo> GetRandomVideoOfRanking()
        {
            if (RankingException != null)
            {
                throw RankingException;
            }

            return Task.FromResult(
                new RankingInfo
                {
                    Aid = 300,
                    Bvid = "BV300",
                    Title = "ranking-300",
                }
            );
        }

        public Task<UpVideoInfo?> GetRandomVideoOfUp(long upId, int total, BiliCookie ck)
        {
            if (_randomVideos.TryGetValue(upId, out var queue) && queue.Count > 0)
            {
                return Task.FromResult<UpVideoInfo?>(queue.Dequeue());
            }

            return Task.FromResult<UpVideoInfo?>(null);
        }

        public Task<IReadOnlyList<UpVideoInfo>> GetVideosOfUp(
            long upId,
            int pageNumber,
            int pageSize,
            BiliCookie ck
        )
        {
            if (!_configUpVideos.TryGetValue(upId, out var videos))
            {
                return Task.FromResult<IReadOnlyList<UpVideoInfo>>([]);
            }

            var skip = (pageNumber - 1) * pageSize;
            return Task.FromResult<IReadOnlyList<UpVideoInfo>>(
                videos.Skip(skip).Take(pageSize).ToList()
            );
        }

        public Task<int> GetVideoCountOfUp(long upId, BiliCookie ck)
        {
            if (_configUpVideos.TryGetValue(upId, out var configVideos))
            {
                return Task.FromResult(configVideos.Count);
            }

            if (_randomVideos.TryGetValue(upId, out var randomVideos))
            {
                return Task.FromResult(randomVideos.Count);
            }

            return Task.FromResult(0);
        }

        public Task WatchAndShareVideo(DailyTaskInfo dailyTaskStatus, BiliCookie ck)
        {
            throw new NotImplementedException();
        }

        public Task WatchVideo(VideoInfoDto videoInfo, BiliCookie ck)
        {
            throw new NotImplementedException();
        }

        public Task ShareVideo(VideoInfoDto videoInfo, BiliCookie ck)
        {
            throw new NotImplementedException();
        }
    }

    private sealed class FakeAccountApi : IAccountApi
    {
        public Task<BiliApiResponse<CoinBalance>> GetCoinBalanceAsync(string ck)
        {
            return Task.FromResult(
                new BiliApiResponse<CoinBalance>
                {
                    Code = 0,
                    Data = new CoinBalance { Money = 8m },
                }
            );
        }
    }

    private sealed class FakeRelationApi : IRelationApi
    {
        private readonly List<long> _followings = [];

        public void SetFollowings(List<long> followings)
        {
            _followings.Clear();
            _followings.AddRange(followings);
        }

        public Task<BiliApiResponse<GetFollowingsResponse>> GetFollowings(
            GetFollowingsRequest request,
            string ck
        )
        {
            return Task.FromResult(
                new BiliApiResponse<GetFollowingsResponse>
                {
                    Code = 0,
                    Data = new GetFollowingsResponse
                    {
                        Total = _followings.Count,
                        List = _followings
                            .Select(mid => new UpInfo { Mid = mid, Uname = $"up-{mid}" })
                            .ToList(),
                    },
                }
            );
        }

        public Task<BiliApiResponse<List<UpInfo>>> GetFollowingsByTag(
            GetSpecialFollowingsRequest request,
            string ck
        )
        {
            return Task.FromResult(new BiliApiResponse<List<UpInfo>> { Code = 0, Data = [] });
        }

        public Task<BiliApiResponse<List<TagDto>>> GetTags(
            string ck,
            string referer = RelationApiConstant.GetTagsReferer
        )
        {
            throw new NotImplementedException();
        }

        public Task<BiliApiResponse<CreateTagResponse>> CreateTag(
            CreateTagRequest request,
            string ck,
            string referer = RelationApiConstant.GetTagsReferer
        )
        {
            throw new NotImplementedException();
        }

        public Task<BiliApiResponse> CopyUpsToGroup(
            CopyUserToGroupRequest request,
            string ck,
            string referer = RelationApiConstant.CopyReferer
        )
        {
            throw new NotImplementedException();
        }

        public Task<BiliApiResponse> ModifyRelation(
            ModifyRelationRequest request,
            string ck,
            string referer = RelationApiConstant.ModifyReferer
        )
        {
            throw new NotImplementedException();
        }
    }

    private sealed class FakeVideoApi : IVideoApi
    {
        private readonly Dictionary<long, int> _donatedCoins = [];

        public void SetDonatedCoins(long aid, int multiply)
        {
            _donatedCoins[aid] = multiply;
        }

        public Task<BiliApiResponse> ShareVideo(ShareVideoRequest request, string ck)
        {
            throw new NotImplementedException();
        }

        public Task<BiliApiResponse> UploadVideoHeartbeat(
            UploadVideoHeartbeatRequest request,
            string ck
        )
        {
            throw new NotImplementedException();
        }

        public Task<BiliApiResponse> AddCoinForVideo(
            AddCoinRequest request,
            string ck,
            string refer =
                "https://www.bilibili.com/video/BV123456/?spm_id_from=333.1007.tianma.1-1-1.click&vd_source=80c1601a7003934e7a90709c18dfcffd"
        )
        {
            return Task.FromResult(new BiliApiResponse { Code = 0, Message = "0" });
        }

        public Task<BiliApiResponse<DonatedCoinsForVideo>> GetDonatedCoinsForVideo(
            GetAlreadyDonatedCoinsRequest request,
            string ck
        )
        {
            var multiply = _donatedCoins.TryGetValue(request.Aid, out var value) ? value : 0;
            return Task.FromResult(
                new BiliApiResponse<DonatedCoinsForVideo>
                {
                    Code = 0,
                    Data = new DonatedCoinsForVideo { Multiply = multiply },
                }
            );
        }

        public Task<BiliApiResponse<SearchUpVideosResponse>> SearchVideosByUpId(
            SearchVideosByUpIdDto request,
            string ck
        )
        {
            throw new NotImplementedException();
        }

        public Task<GetBangumiBySsidResponse> GetBangumiBySsid(long ssid, string ck)
        {
            throw new NotImplementedException();
        }
    }

    private sealed class TestOptionsMonitor<T>(T value) : IOptionsMonitor<T>
    {
        public T CurrentValue => value;

        public T Get(string? name) => value;

        public IDisposable? OnChange(Action<T, string?> listener) => null;
    }

    private sealed class ListLogger<T> : ILogger<T>
    {
        public List<string> Messages { get; } = [];

        public IDisposable BeginScope<TState>(TState state)
            where TState : notnull => NullDisposable.Instance;

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

        private sealed class NullDisposable : IDisposable
        {
            public static readonly NullDisposable Instance = new();

            public void Dispose() { }
        }
    }
}
