using Ray.BiliBiliTool.Agent;

namespace Ray.BiliBiliTool.DomainService.Interfaces;

public interface IFavoriteDomainService : IDomainService
{
    Task<long?> GetOrCreateFolderAsync(string folderName, BiliCookie cookie, long aid = 0);

    Task<bool> AddVideoAsync(
        long aid,
        long folderId,
        string fromSpmid,
        string spmid,
        string statistics,
        BiliCookie cookie
    );
}
