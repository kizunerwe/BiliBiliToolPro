using Ray.BiliBiliTool.Agent.BiliBiliAgent.Dtos;

namespace DomainServiceTest;

public class RankingVideoCacheTest
{
    [Fact]
    public async Task GetOrLoadAsync_ShouldReuseLoadedVideos()
    {
        var cache = new RankingVideoCache();
        var loaderCallCount = 0;

        Task<List<RankingInfo>> Loader()
        {
            loaderCallCount++;
            return Task.FromResult(
                new List<RankingInfo>
                {
                    new()
                    {
                        Aid = 1,
                        Bvid = "BV1",
                        Title = "video-1",
                    },
                }
            );
        }

        var first = await cache.GetOrLoadAsync(Loader);
        var second = await cache.GetOrLoadAsync(Loader);

        Assert.True(first.IsAvailable);
        Assert.True(second.IsAvailable);
        Assert.Equal(1, loaderCallCount);
        Assert.Single(first.Videos!);
        Assert.Single(second.Videos!);
    }

    [Fact]
    public async Task GetOrLoadAsync_ShouldCacheFailureForCurrentRun()
    {
        var cache = new RankingVideoCache();
        var loaderCallCount = 0;

        Task<List<RankingInfo>> Loader()
        {
            loaderCallCount++;
            throw new InvalidOperationException("risk control");
        }

        var first = await cache.GetOrLoadAsync(Loader);
        var second = await cache.GetOrLoadAsync(Loader);

        Assert.False(first.IsAvailable);
        Assert.False(second.IsAvailable);
        Assert.Equal(1, loaderCallCount);
        Assert.Contains("risk control", first.ErrorMessage);
        Assert.Contains("risk control", second.ErrorMessage);
    }
}
