using Ray.BiliBiliTool.Agent.BiliBiliAgent.Dtos;
using Ray.BiliBiliTool.Agent.BiliBiliAgent.Dtos.Favorite;
using WebApiClientCore.Attributes;

namespace Ray.BiliBiliTool.Agent.BiliBiliAgent.Interfaces;

public interface IFavoriteApi : IBiliBiliApi
{
    [HttpGet("/x/v3/fav/folder/created/list-all")]
    Task<BiliApiResponse<FavoriteFolderListResponse>> GetCreatedFolders(
        [PathQuery] GetFavoriteFoldersRequest request,
        [Header("Cookie")] string ck
    );

    [Header("Content-Type", "application/x-www-form-urlencoded")]
    [HttpPost("/x/v3/fav/folder/add")]
    Task<BiliApiResponse> CreateFolder(
        [FormContent] CreateFavoriteFolderRequest request,
        [Header("Cookie")] string ck
    );

    [Header("Content-Type", "application/x-www-form-urlencoded")]
    [HttpPost("/x/v3/fav/resource/deal")]
    Task<BiliApiResponse> DealResource(
        [FormContent] DealFavoriteResourceRequest request,
        [Header("Cookie")] string ck
    );
}
