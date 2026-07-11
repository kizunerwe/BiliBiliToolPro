using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Ray.BiliBiliTool.Agent;
using Ray.BiliBiliTool.Application;
using Ray.BiliBiliTool.Application.Contracts;
using Ray.BiliBiliTool.Infrastructure.Cookie;

namespace DomainServiceTest;

public class BaseMultiAccountsAppServiceTest
{
    [Fact]
    public async Task DoTaskAsync_ShouldContinueAccountsAndThrowSummary_WhenAnyAccountFails()
    {
        var cookieFactory = new CookieStrFactory<BiliCookie>(
            new ConfigurationBuilder()
                .AddInMemoryCollection(
                    new Dictionary<string, string?>
                    {
                        ["BiliBiliCookies:0"] = "DedeUserID=1;SESSDATA=a;bili_jct=a;buvid3=a",
                        ["BiliBiliCookies:1"] = "DedeUserID=2;SESSDATA=b;bili_jct=b;buvid3=b",
                    }
                )
                .Build()
        );
        var service = new TestMultiAccountsAppService(cookieFactory);

        var exception = await Assert.ThrowsAsync<TaskExecutionException>(() =>
            service.DoTaskAsync()
        );

        Assert.Equal(["1", "2"], service.ProcessedUserIds);
        Assert.Contains("1", exception.Message);
    }

    private sealed class TestMultiAccountsAppService(CookieStrFactory<BiliCookie> cookieFactory)
        : BaseMultiAccountsAppService(NullLogger.Instance, cookieFactory)
    {
        public List<string> ProcessedUserIds { get; } = [];

        protected override Task DoTaskAccountAsync(
            BiliCookie ck,
            CancellationToken cancellationToken = default
        )
        {
            ProcessedUserIds.Add(ck.UserId);
            return ck.UserId == "1"
                ? Task.FromException(new InvalidOperationException("failed"))
                : Task.CompletedTask;
        }
    }
}
