using Microsoft.Extensions.Logging;
using Ray.BiliBiliTool.Agent;
using Ray.BiliBiliTool.Agent.BiliBiliAgent.Dtos;
using Ray.BiliBiliTool.Agent.BiliBiliAgent.Dtos.Favorite;
using Ray.BiliBiliTool.Agent.BiliBiliAgent.Interfaces;
using Ray.BiliBiliTool.DomainService;

namespace DomainServiceTest;

public sealed class FavoriteDomainServiceTest
{
    [Fact]
    public async Task GetOrCreateFolderAsync_ShouldUseSingleMatchingFolder()
    {
        var api = new FakeFavoriteApi();
        api.FolderLists.Enqueue(Folders((12, "专用收藏夹")));
        var service = new FavoriteDomainService(new ListLogger<FavoriteDomainService>(), api);
        var folderId = await service.GetOrCreateFolderAsync("专用收藏夹", CreateCookie("10001"));
        Assert.Equal(12, folderId);
        Assert.Empty(api.CreateRequests);
        Assert.Equal("10001", api.ListRequests.Single().up_mid);
    }

    [Fact]
    public async Task GetOrCreateFolderAsync_ShouldCreateAndReloadWhenMissing()
    {
        var api = new FakeFavoriteApi();
        api.FolderLists.Enqueue(Folders());
        api.FolderLists.Enqueue(Folders((34, "专用收藏夹")));
        var service = new FavoriteDomainService(new ListLogger<FavoriteDomainService>(), api);
        var folderId = await service.GetOrCreateFolderAsync("专用收藏夹", CreateCookie("10002"));
        Assert.Equal(34, folderId);
        Assert.Equal("专用收藏夹", api.CreateRequests.Single().title);
        Assert.Equal(0, api.CreateRequests.Single().privacy);
        Assert.Equal("csrf", api.CreateRequests.Single().csrf);
        Assert.Equal(2, api.ListRequests.Count);
    }

    [Fact]
    public async Task GetOrCreateFolderAsync_ShouldFailWhenDuplicateNamesExist()
    {
        var logger = new ListLogger<FavoriteDomainService>();
        var api = new FakeFavoriteApi();
        api.FolderLists.Enqueue(Folders((1, "专用收藏夹"), (2, "专用收藏夹")));
        var service = new FavoriteDomainService(logger, api);
        var folderId = await service.GetOrCreateFolderAsync("专用收藏夹", CreateCookie("10003"));
        Assert.Null(folderId);
        Assert.Contains(logger.Messages, message => message.Contains("同名收藏夹"));
    }

    [Fact]
    public async Task AddVideoAsync_ShouldSendConfirmedFieldsOnly()
    {
        var api = new FakeFavoriteApi();
        var service = new FavoriteDomainService(new ListLogger<FavoriteDomainService>(), api);
        var result = await service.AddVideoAsync(
            9988,
            66,
            "333.1007",
            "333.788",
            "statistics",
            CreateCookie("10004")
        );
        Assert.True(result);
        var request = api.DealRequests.Single();
        Assert.Equal(9988, request.rid);
        Assert.Equal(2, request.type);
        Assert.Equal("66", request.add_media_ids);
        Assert.Equal("", request.del_media_ids);
        Assert.Equal("web", request.platform);
        Assert.Equal("333.1007", request.from_spmid);
        Assert.Equal("333.788", request.spmid);
        Assert.Equal("statistics", request.statistics);
        Assert.Equal("csrf", request.csrf);
    }

    [Fact]
    public async Task ApiBusinessFailure_ShouldReturnFailureWithoutThrowing()
    {
        var api = new FakeFavoriteApi
        {
            DealResponse = new BiliApiResponse { Code = -1, Message = "failed" },
        };
        var service = new FavoriteDomainService(new ListLogger<FavoriteDomainService>(), api);
        var result = await service.AddVideoAsync(
            1,
            2,
            "from",
            "spmid",
            "stats",
            CreateCookie("10005")
        );
        Assert.False(result);
    }

    private static FavoriteFolderListResponse Folders(params (long Id, string Title)[] folders) =>
        new()
        {
            List = folders
                .Select(x => new FavoriteFolderDto { Id = x.Id, Title = x.Title })
                .ToList(),
        };

    private static BiliCookie CreateCookie(string userId) =>
        new(
            new Dictionary<string, string>
            {
                ["DedeUserID"] = userId,
                ["SESSDATA"] = "sess",
                ["bili_jct"] = "csrf",
            }
        );

    private sealed class FakeFavoriteApi : IFavoriteApi
    {
        public Queue<FavoriteFolderListResponse> FolderLists { get; } = new();
        public List<GetFavoriteFoldersRequest> ListRequests { get; } = [];
        public List<CreateFavoriteFolderRequest> CreateRequests { get; } = [];
        public List<DealFavoriteResourceRequest> DealRequests { get; } = [];
        public BiliApiResponse DealResponse { get; set; } = new() { Code = 0 };

        public Task<BiliApiResponse<FavoriteFolderListResponse>> GetCreatedFolders(
            GetFavoriteFoldersRequest request,
            string ck
        )
        {
            ListRequests.Add(request);
            return Task.FromResult(
                new BiliApiResponse<FavoriteFolderListResponse>
                {
                    Code = 0,
                    Data = FolderLists.Dequeue(),
                }
            );
        }

        public Task<BiliApiResponse> CreateFolder(CreateFavoriteFolderRequest request, string ck)
        {
            CreateRequests.Add(request);
            return Task.FromResult(new BiliApiResponse { Code = 0 });
        }

        public Task<BiliApiResponse> DealResource(DealFavoriteResourceRequest request, string ck)
        {
            DealRequests.Add(request);
            return Task.FromResult(DealResponse);
        }
    }

    private sealed class ListLogger<T> : ILogger<T>
    {
        public List<string> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter
        ) => Messages.Add(formatter(state, exception));
    }
}
