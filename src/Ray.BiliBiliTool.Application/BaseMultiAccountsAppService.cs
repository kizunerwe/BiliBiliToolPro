using Microsoft.Extensions.Logging;
using Ray.BiliBiliTool.Agent;
using Ray.BiliBiliTool.Application.Contracts;
using Ray.BiliBiliTool.Infrastructure.Cookie;

namespace Ray.BiliBiliTool.Application;

public abstract class BaseMultiAccountsAppService(
    ILogger logger,
    CookieStrFactory<BiliCookie> cookieStrFactory
) : AppService
{
    public override async Task DoTaskAsync(CancellationToken cancellationToken = default)
    {
        logger.LogInformation(
            "【账号个数】{count}个" + Environment.NewLine,
            cookieStrFactory.Count
        );
        var failedAccounts = new List<(int Index, Exception Exception)>();
        for (int i = 0; i < cookieStrFactory.Count; i++)
        {
            logger.LogInformation("######### 账号 {num} #########" + Environment.NewLine, i);
            try
            {
                var ck = cookieStrFactory.GetCookie(i);
                await DoTaskAccountAsync(ck, cancellationToken);
            }
            catch (Exception e)
            {
                failedAccounts.Add((i, e));
                logger.LogWarning(e, "账号 {num} 执行失败", i);
            }
        }

        if (failedAccounts.Count > 0)
        {
            throw new TaskExecutionException(
                $"{failedAccounts.Count} 个账号执行失败：{string.Join("；", failedAccounts.Select(x => $"账号{x.Index}：{x.Exception.Message}"))}",
                new AggregateException(failedAccounts.Select(x => x.Exception))
            );
        }
    }

    protected abstract Task DoTaskAccountAsync(
        BiliCookie ck,
        CancellationToken cancellationToken = default
    );
}
