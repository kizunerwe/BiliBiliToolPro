using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Ray.BiliBiliTool.Agent;
using Ray.BiliBiliTool.Agent.BiliBiliAgent.Dtos;
using Ray.BiliBiliTool.Agent.BiliBiliAgent.Dtos.Relation;
using Ray.BiliBiliTool.Agent.BiliBiliAgent.Dtos.Video;
using Ray.BiliBiliTool.Agent.BiliBiliAgent.Interfaces;
using Ray.BiliBiliTool.Config.Options;
using Ray.BiliBiliTool.DomainService;
using Ray.BiliBiliTool.DomainService.Interfaces;

namespace DomainServiceTest;

public sealed class UpFriendlyWatchVideoTest
{
    [Fact]
    public async Task WatchVideo_ShouldUsePlaybackSessionWhenUpFriendlyModeEnabled()
    {
        var videoApi = new FakeVideoApi();
        var delay = new FakeTaskDelay();
        var videoWithoutCookieApi = new FakeVideoWithoutCookieApi();
        var service = new VideoDomainService(
            NullLogger<VideoDomainService>.Instance,
            new TestOptionsMonitor<DailyTaskOptions>(
                new() { IsUpFriendlyMode = true, UpFriendlyWatchSeconds = 60 }
            ),
            new FakeRelationApi(),
            videoApi,
            videoWithoutCookieApi,
            new RankingVideoCache(),
            delay
        );

        await service.WatchVideo(
            new()
            {
                Aid = "1",
                Bvid = "BV1",
                Title = "test",
                Cid = 2,
                Duration = 40,
            },
            CreateCookie()
        );

        Assert.Equal([15, 15, 10], delay.Seconds);
        Assert.Equal([1, 0, 0, 0, 2], videoApi.Requests.Select(x => x.Play_type));
        Assert.Equal([0, 15, 30, 40, 40], videoApi.Requests.Select(x => x.Played_time));
        Assert.Equal(2L, videoApi.Requests.Select(x => x.Cid).Distinct().Single());
        Assert.Empty(videoWithoutCookieApi.RequestedAids);
    }

    [Fact]
    public async Task WatchVideo_ShouldLoadVideoDetailWhenCidIsMissing()
    {
        var videoApi = new FakeVideoApi();
        var delay = new FakeTaskDelay();
        var videoWithoutCookieApi = new FakeVideoWithoutCookieApi
        {
            Detail = new VideoDetail
            {
                Aid = 1,
                Bvid = "BV1",
                Title = "test",
                Cid = 22,
                Duration = 30,
            },
        };
        var service = new VideoDomainService(
            NullLogger<VideoDomainService>.Instance,
            new TestOptionsMonitor<DailyTaskOptions>(
                new() { IsUpFriendlyMode = true, UpFriendlyWatchSeconds = 60 }
            ),
            new FakeRelationApi(),
            videoApi,
            videoWithoutCookieApi,
            new RankingVideoCache(),
            delay
        );

        await service.WatchVideo(
            new()
            {
                Aid = "1",
                Bvid = "BV1",
                Title = "test",
                Cid = 0,
                Duration = 30,
            },
            CreateCookie()
        );

        Assert.Equal(["1"], videoWithoutCookieApi.RequestedAids);
        Assert.Equal(22L, videoApi.Requests.Select(x => x.Cid).Distinct().Single());
        Assert.Equal([0, 15, 30, 30], videoApi.Requests.Select(x => x.Played_time));
    }

    [Fact]
    public async Task WatchVideo_ShouldWaitInFifteenSecondSegmentsAndPauseAtConfiguredProgress()
    {
        var videoApi = new FakeVideoApi();
        var delay = new FakeTaskDelay();
        var service = new VideoDomainService(
            NullLogger<VideoDomainService>.Instance,
            new TestOptionsMonitor<DailyTaskOptions>(new() { UpFriendlyWatchSeconds = 60 }),
            new FakeRelationApi(),
            videoApi,
            new FakeVideoWithoutCookieApi(),
            new RankingVideoCache(),
            delay
        );

        var result = await service.WatchVideoForUpFriendlyMode(
            new VideoDetail
            {
                Aid = 1,
                Bvid = "BV1",
                Title = "test",
                Cid = 2,
                Duration = 40,
            },
            CreateCookie(),
            CancellationToken.None
        );

        Assert.True(result);
        Assert.Equal([15, 15, 10], delay.Seconds);
        Assert.Equal([1, 0, 0, 0, 2], videoApi.Requests.Select(x => x.Play_type));
        Assert.Equal([0, 15, 30, 40, 40], videoApi.Requests.Select(x => x.Played_time));
        Assert.Single(videoApi.Requests.Select(x => x.Start_ts).Distinct());
    }

    [Fact]
    public async Task WatchVideo_ShouldStopWhenHeartbeatFails()
    {
        var videoApi = new FakeVideoApi { FailureAtCall = 2 };
        var delay = new FakeTaskDelay();
        var service = new VideoDomainService(
            NullLogger<VideoDomainService>.Instance,
            new TestOptionsMonitor<DailyTaskOptions>(new() { UpFriendlyWatchSeconds = 60 }),
            new FakeRelationApi(),
            videoApi,
            new FakeVideoWithoutCookieApi(),
            new RankingVideoCache(),
            delay
        );

        var result = await service.WatchVideoForUpFriendlyMode(
            new VideoDetail
            {
                Aid = 1,
                Bvid = "BV1",
                Title = "test",
                Cid = 2,
                Duration = 60,
            },
            CreateCookie(),
            CancellationToken.None
        );

        Assert.False(result);
        Assert.Equal([15], delay.Seconds);
        Assert.Equal(2, videoApi.Requests.Count);
    }

    private static BiliCookie CreateCookie() =>
        new(
            new Dictionary<string, string>
            {
                ["DedeUserID"] = "123",
                ["SESSDATA"] = "s",
                ["bili_jct"] = "csrf",
                ["buvid3"] = "b",
            }
        );

    private sealed class FakeTaskDelay : ITaskDelay
    {
        public List<int> Seconds { get; } = [];

        public Task Delay(TimeSpan delay, CancellationToken cancellationToken)
        {
            Seconds.Add((int)delay.TotalSeconds);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeVideoApi : IVideoApi
    {
        public List<UploadVideoHeartbeatRequest> Requests { get; } = [];
        public int FailureAtCall { get; set; }

        public Task<BiliApiResponse> UploadVideoHeartbeat(
            UploadVideoHeartbeatRequest request,
            string ck
        )
        {
            Requests.Add(request);
            return Task.FromResult(
                new BiliApiResponse
                {
                    Code = Requests.Count == FailureAtCall ? -1 : 0,
                    Message = "test",
                }
            );
        }

        public Task<BiliApiResponse> ShareVideo(ShareVideoRequest request, string ck) =>
            throw new NotImplementedException();

        public Task<BiliApiResponse> AddCoinForVideo(
            AddCoinRequest request,
            string ck,
            string refer = ""
        ) => throw new NotImplementedException();

        public Task<BiliApiResponse<DonatedCoinsForVideo>> GetDonatedCoinsForVideo(
            GetAlreadyDonatedCoinsRequest request,
            string ck
        ) => throw new NotImplementedException();

        public Task<BiliApiResponse<SearchUpVideosResponse>> SearchVideosByUpId(
            SearchVideosByUpIdDto request,
            string ck
        ) => throw new NotImplementedException();

        public Task<GetBangumiBySsidResponse> GetBangumiBySsid(long ssid, string ck) =>
            throw new NotImplementedException();
    }

    private sealed class FakeVideoWithoutCookieApi : IVideoWithoutCookieApi
    {
        public VideoDetail? Detail { get; set; }

        public List<string> RequestedAids { get; } = [];

        public Task<BiliApiResponse<VideoDetail>> GetVideoDetail(string aid)
        {
            RequestedAids.Add(aid);
            return Task.FromResult(
                new BiliApiResponse<VideoDetail>
                {
                    Code = 0,
                    Message = "0",
                    Data = Detail ?? throw new InvalidOperationException("Missing fake detail"),
                }
            );
        }

        public Task<BiliApiResponse<Ranking>> GetRegionRankingVideosV2() =>
            throw new NotImplementedException();
#pragma warning disable CS0612
        public Task<BiliApiResponse<List<RankingInfo>>> GetRegionRankingVideos(int rid, int day) =>
            throw new NotImplementedException();
#pragma warning restore CS0612
        public Task<BiliApiResponse> ShareVideo(ShareVideoRequest request, string ck) =>
            throw new NotImplementedException();

        public Task<BiliApiResponse> UploadVideoHeartbeat(
            UploadVideoHeartbeatRequest request,
            string ck
        ) => throw new NotImplementedException();

        public Task<BiliApiResponse> AddCoinForVideo(
            AddCoinRequest request,
            string ck,
            string refer = ""
        ) => throw new NotImplementedException();

        public Task<BiliApiResponse<DonatedCoinsForVideo>> GetDonatedCoinsForVideo(
            GetAlreadyDonatedCoinsRequest request,
            string ck
        ) => throw new NotImplementedException();

        public Task<BiliApiResponse<SearchUpVideosResponse>> SearchVideosByUpId(
            SearchVideosByUpIdDto request,
            string ck
        ) => throw new NotImplementedException();

        public Task<GetBangumiBySsidResponse> GetBangumiBySsid(long ssid, string ck) =>
            throw new NotImplementedException();
    }

    private sealed class FakeRelationApi : IRelationApi
    {
        public Task<BiliApiResponse<GetFollowingsResponse>> GetFollowings(
            GetFollowingsRequest request,
            string ck
        ) => throw new NotImplementedException();

        public Task<BiliApiResponse<List<UpInfo>>> GetFollowingsByTag(
            GetSpecialFollowingsRequest request,
            string ck
        ) => throw new NotImplementedException();

        public Task<BiliApiResponse<List<TagDto>>> GetTags(
            string ck,
            string referer = RelationApiConstant.GetTagsReferer
        ) => throw new NotImplementedException();

        public Task<BiliApiResponse<CreateTagResponse>> CreateTag(
            CreateTagRequest request,
            string ck,
            string referer = RelationApiConstant.GetTagsReferer
        ) => throw new NotImplementedException();

        public Task<BiliApiResponse> CopyUpsToGroup(
            CopyUserToGroupRequest request,
            string ck,
            string referer = RelationApiConstant.CopyReferer
        ) => throw new NotImplementedException();

        public Task<BiliApiResponse> ModifyRelation(
            ModifyRelationRequest request,
            string ck,
            string referer = RelationApiConstant.ModifyReferer
        ) => throw new NotImplementedException();
    }

    private sealed class TestOptionsMonitor<T>(T value) : IOptionsMonitor<T>
    {
        public T CurrentValue => value;

        public T Get(string? name) => value;

        public IDisposable? OnChange(Action<T, string?> listener) => null;
    }
}
