using Microsoft.Extensions.Logging;
using Ray.BiliBiliTool.Agent;
using Ray.BiliBiliTool.Agent.BiliBiliAgent.Dtos.Favorite;
using Ray.BiliBiliTool.Agent.BiliBiliAgent.Interfaces;
using Ray.BiliBiliTool.DomainService.Interfaces;

namespace Ray.BiliBiliTool.DomainService;

public sealed class FavoriteDomainService(
    ILogger<FavoriteDomainService> logger,
    IFavoriteApi favoriteApi
) : IFavoriteDomainService
{
    public async Task<long?> GetOrCreateFolderAsync(
        string folderName,
        BiliCookie cookie,
        long aid = 0
    )
    {
        try
        {
            var matches = await FindFoldersAsync(folderName, aid, cookie);
            if (matches == null)
                return null;

            if (matches.Count == 1)
                return matches[0].Id;

            if (matches.Count > 1)
            {
                logger.LogError("找到多个同名收藏夹“{FolderName}”，为避免误操作已跳过", folderName);
                return null;
            }

            var createResponse = await favoriteApi.CreateFolder(
                new CreateFavoriteFolderRequest(folderName, cookie.BiliJct),
                cookie.ToString()
            );
            if (createResponse.Code != 0)
            {
                logger.LogError("创建收藏夹失败：{Message}", createResponse.Message);
                return null;
            }

            matches = await FindFoldersAsync(folderName, aid, cookie);
            if (matches == null)
                return null;

            if (matches.Count == 1)
                return matches[0].Id;

            logger.LogError(
                matches.Count == 0
                    ? "创建收藏夹后未能在列表中找到“{FolderName}”"
                    : "创建收藏夹后找到多个同名收藏夹“{FolderName}”，为避免误操作已跳过",
                folderName
            );
            return null;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "获取或创建收藏夹失败");
            return null;
        }
    }

    public async Task<bool> AddVideoAsync(
        long aid,
        long folderId,
        string fromSpmid,
        string spmid,
        string statistics,
        BiliCookie cookie
    )
    {
        try
        {
            var response = await favoriteApi.DealResource(
                new DealFavoriteResourceRequest(
                    aid,
                    folderId,
                    fromSpmid,
                    spmid,
                    statistics,
                    cookie.BiliJct
                ),
                cookie.ToString()
            );
            if (response.Code == 0)
                return true;

            logger.LogError("加入收藏夹失败：{Message}", response.Message);
            return false;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "加入收藏夹失败");
            return false;
        }
    }

    private async Task<List<FavoriteFolderDto>?> FindFoldersAsync(
        string folderName,
        long aid,
        BiliCookie cookie
    )
    {
        var response = await favoriteApi.GetCreatedFolders(
            new GetFavoriteFoldersRequest(aid, cookie.UserId),
            cookie.ToString()
        );
        if (response.Code != 0)
        {
            logger.LogError("获取收藏夹列表失败：{Message}", response.Message);
            return null;
        }

        if (response.Data is null)
        {
            logger.LogError("获取收藏夹列表失败：响应缺少 data（业务码：{Code}）", response.Code);
            return null;
        }

        return (response.Data.List ?? []).Where(x => x.Title == folderName).ToList();
    }
}
