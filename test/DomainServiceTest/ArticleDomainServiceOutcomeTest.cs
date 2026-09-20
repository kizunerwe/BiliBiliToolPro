using Microsoft.Extensions.Logging.Abstractions;
using Ray.BiliBiliTool.Agent.BiliBiliAgent.Dtos;
using Ray.BiliBiliTool.Agent.BiliBiliAgent.Dtos.Article;
using Ray.BiliBiliTool.Agent.BiliBiliAgent.Interfaces;
using Ray.BiliBiliTool.Config.Options;
using Ray.BiliBiliTool.DomainService;
using Ray.BiliBiliTool.DomainService.Dtos;
using Ray.BiliBiliTool.DomainService.Interfaces;

namespace DomainServiceTest;

public sealed class ArticleDomainServiceOutcomeTest
{
    [Fact]
    public async Task ArticleCoinRejection_ShouldBeFailed()
    {
        var articleApi = CreateArticleApi(-1);
        var service = new ArticleDomainService(
            articleApi,
            NullLogger<ArticleDomainService>.Instance,
            new TestDoubles.OptionsMonitor<DailyTaskOptions>(
                new() { NumberOfCoins = 1, SupportUpIds = "456" }
            ),
            CreateCoinService(),
            CreateAccountApi()
        );

        var result = await service.AddCoinForArticles(TestDoubles.Cookie());

        Assert.Equal(TaskStepStatus.Failed, result.Status);
    }

    [Fact]
    public async Task ArticleCoinSuccess_ShouldBeSucceeded()
    {
        var articleApi = CreateArticleApi(0);
        var service = new ArticleDomainService(
            articleApi,
            NullLogger<ArticleDomainService>.Instance,
            new TestDoubles.OptionsMonitor<DailyTaskOptions>(
                new() { NumberOfCoins = 1, SupportUpIds = "456" }
            ),
            CreateCoinService(),
            CreateAccountApi()
        );

        var result = await service.AddCoinForArticles(TestDoubles.Cookie());

        Assert.Equal(TaskStepStatus.Succeeded, result.Status);
    }

    [Fact]
    public async Task ArticleStatusQueryFailure_ShouldBeFailed()
    {
        var articleApi = CreateArticleApi(0, articleInfoCode: 1001);
        var service = new ArticleDomainService(
            articleApi,
            NullLogger<ArticleDomainService>.Instance,
            new TestDoubles.OptionsMonitor<DailyTaskOptions>(
                new() { NumberOfCoins = 1, SupportUpIds = "456" }
            ),
            CreateCoinService(),
            CreateAccountApi()
        );

        var result = await service.AddCoinForArticles(TestDoubles.Cookie());

        Assert.Equal(TaskStepStatus.Failed, result.Status);
    }

    private static IArticleApi CreateArticleApi(int coinCode, int articleInfoCode = 0) =>
        TestDoubles.Api<IArticleApi>(
            (method, _) =>
                method.Name switch
                {
                    nameof(IArticleApi.SearchUpArticlesByUpIdAsync) => Task.FromResult(
                        new BiliApiResponse<SearchUpArticlesResponse>
                        {
                            Code = 0,
                            Data = new SearchUpArticlesResponse
                            {
                                Count = 1,
                                Articles = [new ArticleInfo { Id = 789, Title = "test" }],
                            },
                        }
                    ),
                    nameof(IArticleApi.SearchArticleInfoAsync) => Task.FromResult(
                        new BiliApiResponse<SearchArticleInfoResponse>
                        {
                            Code = articleInfoCode,
                            Data =
                                articleInfoCode == 0
                                    ? new SearchArticleInfoResponse { Coin = 0 }
                                    : null,
                        }
                    ),
                    nameof(IArticleApi.AddCoinForArticleAsync) => Task.FromResult(
                        new BiliApiResponse { Code = coinCode, Message = "拒绝" }
                    ),
                    _ => throw new InvalidOperationException($"Unexpected API: {method.Name}"),
                }
        );

    private static ICoinDomainService CreateCoinService() =>
        TestDoubles.Api<ICoinDomainService>(
            (method, _) =>
                method.Name switch
                {
                    nameof(ICoinDomainService.GetDonatedCoins) => Task.FromResult(0),
                    nameof(ICoinDomainService.GetCoinBalance) => Task.FromResult(5m),
                    _ => throw new InvalidOperationException($"Unexpected API: {method.Name}"),
                }
        );

    private static IAccountApi CreateAccountApi() =>
        TestDoubles.Api<IAccountApi>(
            (method, _) =>
                method.Name == nameof(IAccountApi.GetCoinBalanceAsync)
                    ? Task.FromResult(
                        new BiliApiResponse<CoinBalance>
                        {
                            Code = 0,
                            Data = new CoinBalance { Money = 5 },
                        }
                    )
                    : throw new InvalidOperationException($"Unexpected API: {method.Name}")
        );
}
