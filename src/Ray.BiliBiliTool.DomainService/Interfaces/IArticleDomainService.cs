using Ray.BiliBiliTool.Agent;
using Ray.BiliBiliTool.DomainService.Dtos;

namespace Ray.BiliBiliTool.DomainService.Interfaces;

public interface IArticleDomainService : IDomainService
{
    Task<bool> AddCoinForArticle(long cvid, long mid, BiliCookie ck);

    Task<TaskStepResult> AddCoinForArticles(BiliCookie ck);

    Task LikeArticle(long cvid, BiliCookie ck);
}
