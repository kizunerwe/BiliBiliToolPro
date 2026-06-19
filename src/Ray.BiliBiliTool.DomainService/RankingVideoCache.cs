using Ray.BiliBiliTool.Agent.BiliBiliAgent.Dtos;

namespace Ray.BiliBiliTool.DomainService;

public sealed class RankingVideoCache
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private List<RankingInfo>? _cachedVideos;
    private string? _errorMessage;

    public async Task<RankingVideoCacheResult> GetOrLoadAsync(Func<Task<List<RankingInfo>>> loader)
    {
        if (_cachedVideos is { Count: > 0 })
        {
            return RankingVideoCacheResult.Success(_cachedVideos);
        }

        if (!string.IsNullOrWhiteSpace(_errorMessage))
        {
            return RankingVideoCacheResult.Failure(_errorMessage);
        }

        await _gate.WaitAsync();
        try
        {
            if (_cachedVideos is { Count: > 0 })
            {
                return RankingVideoCacheResult.Success(_cachedVideos);
            }

            if (!string.IsNullOrWhiteSpace(_errorMessage))
            {
                return RankingVideoCacheResult.Failure(_errorMessage);
            }

            List<RankingInfo> videos = await loader();
            if (videos.Count == 0)
            {
                _errorMessage = "排行榜为空";
                return RankingVideoCacheResult.Failure(_errorMessage);
            }

            _cachedVideos = videos;
            return RankingVideoCacheResult.Success(_cachedVideos);
        }
        catch (Exception ex)
        {
            _errorMessage = ex.Message;
            return RankingVideoCacheResult.Failure(_errorMessage);
        }
        finally
        {
            _gate.Release();
        }
    }
}

public sealed record RankingVideoCacheResult(
    bool IsAvailable,
    IReadOnlyList<RankingInfo>? Videos,
    string? ErrorMessage
)
{
    public static RankingVideoCacheResult Success(IReadOnlyList<RankingInfo> videos)
    {
        return new RankingVideoCacheResult(true, videos, null);
    }

    public static RankingVideoCacheResult Failure(string errorMessage)
    {
        return new RankingVideoCacheResult(false, null, errorMessage);
    }
}
