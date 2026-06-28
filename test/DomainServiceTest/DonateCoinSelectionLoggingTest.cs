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

public class DonateCoinSelectionLoggingTest
{
    [Fact]
    public async Task AddCoinsForVideos_ShouldLogSelectionPlanOnlyOncePerAccount()
    {
        var logger = new ListLogger<DonateCoinDomainService>();
        var options = new TestOptionsMonitor<DailyTaskOptions>(
            new DailyTaskOptions
            {
                NumberOfCoins = 2,
                NumberOfProtectedCoins = 0,
                SelectLike = false,
                SupportUpIds = "487417170",
            }
        );
        var domainService = new DonateCoinDomainService(
            logger,
            options,
            new FakeAccountApi(),
            new FakeCoinDomainService(),
            new FakeVideoDomainService(),
            new FakeRelationApi(),
            new FakeVideoApi(),
            new DonateCoinSelectionStateStore(
                NullLogger<DonateCoinSelectionStateStore>.Instance,
                Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.json")
            )
        );
        var ck = new BiliCookie(
            new Dictionary<string, string>
            {
                ["DedeUserID"] = "10001",
                ["SESSDATA"] = "sess",
                ["bili_jct"] = "csrf",
                ["buvid3"] = "buvid",
            }
        );

        await domainService.AddCoinsForVideos(ck);

        Assert.Equal(
            1,
            logger.Messages.Count(x =>
                x.Contains("【选视频】按顺序尝试：配置UP -> 特别关注 -> 普通关注 -> 排行榜")
            )
        );
    }

    private sealed class FakeCoinDomainService : ICoinDomainService
    {
        public Task<decimal> GetCoinBalance(BiliCookie ck) => Task.FromResult(10m);

        public Task<int> GetDonatedCoins(BiliCookie ck) => Task.FromResult(0);
    }

    private sealed class FakeVideoDomainService : IVideoDomainService
    {
        private readonly List<UpVideoInfo> _videos =
        [
            new UpVideoInfo
            {
                Aid = 100,
                Bvid = "BV100",
                Title = "video-100",
                Length = "00:15",
            },
            new UpVideoInfo
            {
                Aid = 101,
                Bvid = "BV101",
                Title = "video-101",
                Length = "00:15",
            },
        ];

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
            throw new NotImplementedException();
        }

        public Task<UpVideoInfo?> GetRandomVideoOfUp(long upId, int total, BiliCookie ck)
        {
            return Task.FromResult(_videos.FirstOrDefault());
        }

        public Task<IReadOnlyList<UpVideoInfo>> GetVideosOfUp(
            long upId,
            int pageNumber,
            int pageSize,
            BiliCookie ck
        )
        {
            return Task.FromResult<IReadOnlyList<UpVideoInfo>>(_videos);
        }

        public Task<int> GetVideoCountOfUp(long upId, BiliCookie ck) => Task.FromResult(2);

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
        public Task<BiliApiResponse<GetFollowingsResponse>> GetFollowings(
            GetFollowingsRequest request,
            string ck
        )
        {
            throw new NotImplementedException();
        }

        public Task<BiliApiResponse<List<UpInfo>>> GetFollowingsByTag(
            GetSpecialFollowingsRequest request,
            string ck
        )
        {
            throw new NotImplementedException();
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
            return Task.FromResult(
                new BiliApiResponse<DonatedCoinsForVideo>
                {
                    Code = 0,
                    Data = new DonatedCoinsForVideo { Multiply = 0 },
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
