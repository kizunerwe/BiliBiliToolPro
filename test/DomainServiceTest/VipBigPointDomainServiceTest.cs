using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Ray.BiliBiliTool.Agent;
using Ray.BiliBiliTool.Agent.BiliBiliAgent.Dtos;
using Ray.BiliBiliTool.Agent.BiliBiliAgent.Dtos.Mall;
using Ray.BiliBiliTool.Agent.BiliBiliAgent.Dtos.Passport;
using Ray.BiliBiliTool.Agent.BiliBiliAgent.Dtos.Relation;
using Ray.BiliBiliTool.Agent.BiliBiliAgent.Dtos.Video;
using Ray.BiliBiliTool.Agent.BiliBiliAgent.Dtos.ViewMall;
using Ray.BiliBiliTool.Agent.BiliBiliAgent.Dtos.VipTask;
using Ray.BiliBiliTool.Agent.BiliBiliAgent.Dtos.VipTask.ThreeDaysSign;
using Ray.BiliBiliTool.Agent.BiliBiliAgent.Interfaces;
using Ray.BiliBiliTool.Config.Options;
using Ray.BiliBiliTool.DomainService;
using Ray.BiliBiliTool.DomainService.Dtos;
using Ray.BiliBiliTool.DomainService.Interfaces;

namespace DomainServiceTest;

public class VipBigPointDomainServiceTest
{
    [Fact]
    public async Task CompleteOgvWatchAsync_ShouldReturnFalse_WhenBangumiHasNoEpisodes()
    {
        var service = new TestableVipBigPointDomainService(
            NullLogger<VipBigPointDomainService>.Instance,
            new TestOptionsMonitor<VipBigPointOptions>(
                new VipBigPointOptions { ViewBangumis = "33378" }
            ),
            new FakeVipBigPointApi(),
            new FakeMallApi(),
            new FakeVipMallApi(),
            new FakeVideoApiWithEmptyBangumi(),
            new FakeAccountDomainService(),
            new FakeVideoDomainService(),
            new VipBigPointAccessKeyStore()
        );

        bool result = await service.CompleteOgvWatchAsync(CreateCookie());

        Assert.False(result);
    }

    [Fact]
    public async Task CompleteOgvWatchAsync_ShouldWaitForCountdownBeforeComplete()
    {
        var vipApi = new FakeVipBigPointApi
        {
            StartResponse = new BiliApiResponse<StartOgvWatchResponse>
            {
                Code = 0,
                Data = new StartOgvWatchResponse
                {
                    watch_count_down_cfg = new WatchCountDownConfig
                    {
                        milliseconds = 600000,
                        task_id = "4320003",
                        token = "token-demo",
                    },
                },
            },
            CompleteResponse = new BiliApiResponse { Code = 0, Message = "success" },
        };

        var service = new TestableVipBigPointDomainService(
            NullLogger<VipBigPointDomainService>.Instance,
            new TestOptionsMonitor<VipBigPointOptions>(
                new VipBigPointOptions { ViewBangumis = "33378", AccessKey = "access-key-demo" }
            ),
            vipApi,
            new FakeMallApi(),
            new FakeVipMallApi(),
            new FakeVideoApiWithSingleBangumi(),
            new FakeAccountDomainService(),
            new FakeVideoDomainService(),
            new VipBigPointAccessKeyStore()
        );

        bool result = await service.CompleteOgvWatchAsync(CreateCookie());

        Assert.True(result);
        Assert.Equal(TimeSpan.FromMilliseconds(605000), service.RecordedDelay);
        Assert.True(vipApi.CompleteCalled);
    }

    [Fact]
    public async Task CompleteOgvWatchAsync_ShouldUseRuntimeAccessKey_ForCurrentAccount()
    {
        var vipApi = new FakeVipBigPointApi
        {
            StartResponse = new BiliApiResponse<StartOgvWatchResponse>
            {
                Code = 0,
                Data = new StartOgvWatchResponse
                {
                    watch_count_down_cfg = new WatchCountDownConfig
                    {
                        milliseconds = 0,
                        task_id = "4320003",
                        token = "token-demo",
                    },
                },
            },
            CompleteResponse = new BiliApiResponse { Code = 0, Message = "success" },
        };
        var accessKeyStore = new VipBigPointAccessKeyStore();
        accessKeyStore.Set("565140580", "runtime-access-key");

        var service = new TestableVipBigPointDomainService(
            NullLogger<VipBigPointDomainService>.Instance,
            new TestOptionsMonitor<VipBigPointOptions>(
                new VipBigPointOptions { ViewBangumis = "33378" }
            ),
            vipApi,
            new FakeMallApi(),
            new FakeVipMallApi(),
            new FakeVideoApiWithSingleBangumi(),
            new FakeAccountDomainService(),
            new FakeVideoDomainService(),
            accessKeyStore
        );

        bool result = await service.CompleteOgvWatchAsync(CreateCookie());

        Assert.True(result);
        Assert.Equal("runtime-access-key", vipApi.LastStartRequest?.access_key);
        Assert.Equal("runtime-access-key", vipApi.LastCompleteRequest?.access_key);
    }

    [Fact]
    public async Task CompleteOgvWatchAsync_ShouldSkip_WhenAccessKeyMissing()
    {
        var vipApi = new FakeVipBigPointApi();
        var service = new TestableVipBigPointDomainService(
            NullLogger<VipBigPointDomainService>.Instance,
            new TestOptionsMonitor<VipBigPointOptions>(
                new VipBigPointOptions { ViewBangumis = "33378" }
            ),
            vipApi,
            new FakeMallApi(),
            new FakeVipMallApi(),
            new FakeVideoApiWithSingleBangumi(),
            new FakeAccountDomainService(),
            new FakeVideoDomainService(),
            new VipBigPointAccessKeyStore()
        );

        bool result = await service.CompleteOgvWatchAsync(CreateCookie());

        Assert.False(result);
        Assert.False(vipApi.StartCalled);
        Assert.False(vipApi.CompleteCalled);
    }

    [Fact]
    public async Task ReceiveDailyMissionsAsync_ShouldSkip_WhenAccessKeyMissing()
    {
        var vipApi = new FakeVipBigPointApi();
        var combine = new VipBigPointCombine
        {
            point_info = new PointInfo(1, 0, 0, 0),
            Task_info = new TaskInfo
            {
                Sing_task_item = new SingTaskItem(),
                Modules =
                [
                    new ModuleItem
                    {
                        module_title = "日常任务",
                        common_task_item =
                        [
                            new CommonTaskItem
                            {
                                title = "浏览装扮商城主页",
                                task_code = "dress-view",
                                state = 0,
                            },
                        ],
                    },
                ],
            },
        };
        var service = new TestableVipBigPointDomainService(
            NullLogger<VipBigPointDomainService>.Instance,
            new TestOptionsMonitor<VipBigPointOptions>(new VipBigPointOptions()),
            vipApi,
            new FakeMallApi(),
            new FakeVipMallApi(),
            new FakeVideoApiWithSingleBangumi(),
            new FakeAccountDomainService(),
            new FakeVideoDomainService(),
            new VipBigPointAccessKeyStore()
        );

        await service.ReceiveDailyMissionsAsync(combine, CreateCookie());

        Assert.False(vipApi.ReceiveV2Called);
    }

    private static BiliCookie CreateCookie()
    {
        return new BiliCookie(
            new Dictionary<string, string>
            {
                ["DedeUserID"] = "565140580",
                ["SESSDATA"] = "sess",
                ["bili_jct"] = "csrf",
                ["buvid3"] = "buvid",
            }
        );
    }

    private sealed class FakeVipBigPointApi : IVipBigPointApi
    {
        public BiliApiResponse<StartOgvWatchResponse>? StartResponse { get; set; }
        public BiliApiResponse? CompleteResponse { get; set; }
        public bool StartCalled { get; private set; }
        public bool CompleteCalled { get; private set; }
        public bool ReceiveV2Called { get; private set; }
        public StartOgvWatchRequest? LastStartRequest { get; private set; }
        public CompleteOgvWatchRequest? LastCompleteRequest { get; private set; }

        public Task<BiliApiResponse<ThreeDaySignResponse>> GetThreeDaySignAsync(
            ThreeDaySignRequest request,
            string ck
        ) => throw new NotImplementedException();

        public Task<BiliApiResponse<VipBigPointCombine>> GetCombineAsync(string ck) =>
            throw new NotImplementedException();

        public Task<BiliApiResponse> SignAsync(SignRequest request, string ck) =>
            throw new NotImplementedException();

        public Task<BiliApiResponse> Receive(ReceiveOrCompleteTaskRequest request, string ck) =>
            throw new NotImplementedException();

        public Task<BiliApiResponse> ReceiveV2(VipBigPointTaskAppRequest request, string ck)
        {
            ReceiveV2Called = true;
            return Task.FromResult(new BiliApiResponse { Code = 0, Message = "0" });
        }

        public Task<BiliApiResponse> CompleteAsync(
            ReceiveOrCompleteTaskRequest request,
            string ck
        ) => throw new NotImplementedException();

        public Task<BiliApiResponse> CompleteV2(VipBigPointTaskAppRequest request, string ck) =>
            throw new NotImplementedException();

        public Task<BiliApiResponse> ViewComplete(ViewRequest request, string ck) =>
            throw new NotImplementedException();

        public Task<BiliApiResponse<VouchersInfoResponse>> GetVouchersInfoAsync(string ck) =>
            throw new NotImplementedException();

        public Task<BiliApiResponse> ObtainVipExperienceAsync(
            VipExperienceRequest request,
            string ck
        ) => throw new NotImplementedException();

        public Task<BiliApiResponse<StartOgvWatchResponse>> StartOgvWatchAsync(
            StartOgvWatchRequest request,
            string ck,
            string buvid,
            string mid
        )
        {
            StartCalled = true;
            LastStartRequest = request;
            return Task.FromResult(
                StartResponse
                    ?? throw new InvalidOperationException("StartResponse was not configured.")
            );
        }

        public Task<BiliApiResponse> CompleteOgvWatchAsync(
            CompleteOgvWatchRequest request,
            string ck,
            string buvid,
            string mid
        )
        {
            CompleteCalled = true;
            LastCompleteRequest = request;
            return Task.FromResult(
                CompleteResponse
                    ?? throw new InvalidOperationException("CompleteResponse was not configured.")
            );
        }
    }

    private sealed class FakeMallApi : IMallApi
    {
        public Task<BiliApiResponse<Sign2Response>> Sign2Async(
            Sign2RequestPath requestPath,
            Sign2Request request,
            string ck
        ) => throw new NotImplementedException();

        public Task<BiliApiResponse<VipBigPointCombine>> GetCombineAsync(
            GetCombineRequest request,
            string ck
        ) => throw new NotImplementedException();
    }

    private sealed class FakeVipMallApi : IVipMallApi
    {
        public Task<BiliApiResponse> ViewVipMallAsync(ViewVipMallRequest request, string ck) =>
            throw new NotImplementedException();
    }

    private sealed class FakeVideoApiWithEmptyBangumi : IVideoApi
    {
        public Task<BiliApiResponse> ShareVideo(ShareVideoRequest request, string ck) =>
            throw new NotImplementedException();

        public Task<BiliApiResponse> UploadVideoHeartbeat(
            UploadVideoHeartbeatRequest request,
            string ck
        ) => throw new NotImplementedException();

        public Task<BiliApiResponse> AddCoinForVideo(
            AddCoinRequest request,
            string ck,
            string refer =
                "https://www.bilibili.com/video/BV123456/?spm_id_from=333.1007.tianma.1-1-1.click&vd_source=80c1601a7003934e7a90709c18dfcffd"
        ) => throw new NotImplementedException();

        public Task<BiliApiResponse<DonatedCoinsForVideo>> GetDonatedCoinsForVideo(
            GetAlreadyDonatedCoinsRequest request,
            string ck
        ) => throw new NotImplementedException();

        public Task<BiliApiResponse<SearchUpVideosResponse>> SearchVideosByUpId(
            SearchVideosByUpIdDto request,
            string ck
        ) => throw new NotImplementedException();

        public Task<GetBangumiBySsidResponse> GetBangumiBySsid(long ssid, string ck)
        {
            return Task.FromResult(
                new GetBangumiBySsidResponse
                {
                    Code = 0,
                    Result = new Result { episodes = [] },
                }
            );
        }
    }

    private sealed class FakeVideoApiWithSingleBangumi : IVideoApi
    {
        public Task<BiliApiResponse> ShareVideo(ShareVideoRequest request, string ck) =>
            throw new NotImplementedException();

        public Task<BiliApiResponse> UploadVideoHeartbeat(
            UploadVideoHeartbeatRequest request,
            string ck
        ) => throw new NotImplementedException();

        public Task<BiliApiResponse> AddCoinForVideo(
            AddCoinRequest request,
            string ck,
            string refer =
                "https://www.bilibili.com/video/BV123456/?spm_id_from=333.1007.tianma.1-1-1.click&vd_source=80c1601a7003934e7a90709c18dfcffd"
        ) => throw new NotImplementedException();

        public Task<BiliApiResponse<DonatedCoinsForVideo>> GetDonatedCoinsForVideo(
            GetAlreadyDonatedCoinsRequest request,
            string ck
        ) => throw new NotImplementedException();

        public Task<BiliApiResponse<SearchUpVideosResponse>> SearchVideosByUpId(
            SearchVideosByUpIdDto request,
            string ck
        ) => throw new NotImplementedException();

        public Task<GetBangumiBySsidResponse> GetBangumiBySsid(long ssid, string ck)
        {
            return Task.FromResult(
                new GetBangumiBySsidResponse
                {
                    Code = 0,
                    Result = new Result
                    {
                        episodes =
                        [
                            new Episode
                            {
                                aid = 7104446040,
                                bvid = "BV1PQ4y1N7V8",
                                cid = 26835486927,
                                duration = 1493033,
                                ep_id = 321808,
                                id = 321808,
                                long_title = "云霄飞车杀人事件",
                                share_copy = "《名侦探柯南》第1话 云霄飞车杀人事件",
                                status = 2,
                            },
                        ],
                    },
                }
            );
        }
    }

    private sealed class FakeAccountDomainService : IAccountDomainService
    {
        public Task<UserInfo> LoginByCookie(BiliCookie cookie) =>
            throw new NotImplementedException();

        public Task<DailyTaskInfo> GetDailyTaskStatus(BiliCookie ck) =>
            throw new NotImplementedException();

        public Task UnfollowBatched(BiliCookie ck) => throw new NotImplementedException();

        public int CalculateUpgradeTime(UserInfo useInfo) => throw new NotImplementedException();
    }

    private sealed class FakeVideoDomainService : IVideoDomainService
    {
        public Task<VideoDetail> GetVideoDetail(string aid) => throw new NotImplementedException();

        public Task<RankingInfo> GetRandomVideoOfRanking() => throw new NotImplementedException();

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

        public Task WatchAndShareVideo(DailyTaskInfo dailyTaskStatus, BiliCookie ck) =>
            throw new NotImplementedException();

        public Task WatchVideo(VideoInfoDto videoInfo, BiliCookie ck) =>
            throw new NotImplementedException();

        public Task ShareVideo(VideoInfoDto videoInfo, BiliCookie ck) =>
            throw new NotImplementedException();
    }

    private sealed class TestOptionsMonitor<T>(T value) : IOptionsMonitor<T>
    {
        public T CurrentValue => value;

        public T Get(string? name) => value;

        public IDisposable? OnChange(Action<T, string?> listener) => null;
    }

    private sealed class TestableVipBigPointDomainService(
        ILogger<VipBigPointDomainService> logger,
        IOptionsMonitor<VipBigPointOptions> vipBigPointOptions,
        IVipBigPointApi vipApi,
        IMallApi mallApi,
        IVipMallApi vipMallApi,
        IVideoApi videoApi,
        IAccountDomainService accountDomainService,
        IVideoDomainService videoDomainService,
        VipBigPointAccessKeyStore vipBigPointAccessKeyStore
    )
        : VipBigPointDomainService(
            logger,
            vipBigPointOptions,
            vipApi,
            mallApi,
            vipMallApi,
            videoApi,
            accountDomainService,
            videoDomainService,
            vipBigPointAccessKeyStore
        )
    {
        public TimeSpan? RecordedDelay { get; private set; }

        protected override Task DelayAsync(TimeSpan delay)
        {
            RecordedDelay = delay;
            return Task.CompletedTask;
        }
    }
}
