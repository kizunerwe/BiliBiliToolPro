using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Ray.BiliBiliTool.Agent;
using Ray.BiliBiliTool.Agent.BiliBiliAgent.Dtos;
using Ray.BiliBiliTool.Application;
using Ray.BiliBiliTool.Application.Contracts;
using Ray.BiliBiliTool.Config.Options;
using Ray.BiliBiliTool.DomainService.Dtos;
using Ray.BiliBiliTool.DomainService.Interfaces;
using Ray.BiliBiliTool.Infrastructure.Cookie;

namespace DomainServiceTest;

[Collection("ConsoleMain")]
public sealed class VipPrivilegeTaskAppServiceOutcomeTest
{
    public VipPrivilegeTaskAppServiceOutcomeTest()
    {
        Program.CreateHost(["--ENVIRONMENT=Development"]);
    }

    [Fact]
    public async Task FailedResult_ShouldBecomeTaskFailureWithoutRefreshingAccount()
    {
        var refreshCount = 0;
        var service = CreateService(TaskStepResult.Fail("业务拒绝"), () => refreshCount++);

        await Assert.ThrowsAsync<TaskExecutionException>(() => service.DoTaskAsync());

        Assert.Equal(1, refreshCount);
    }

    [Fact]
    public async Task SkippedResult_ShouldFinishWithoutRefreshingAccount()
    {
        var refreshCount = 0;
        var service = CreateService(TaskStepResult.Skip("已领取"), () => refreshCount++);

        await service.DoTaskAsync();

        Assert.Equal(1, refreshCount);
    }

    private static VipPrivilegeTaskAppService CreateService(TaskStepResult result, Action refresh)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["BiliBiliCookies:0"] =
                        "DedeUserID=123;SESSDATA=sess;bili_jct=csrf;buvid3=buvid;LIVE_BUVID=live",
                }
            )
            .Build();
        var account = TestDoubles.Api<IAccountDomainService>(
            (method, _) =>
            {
                if (method.Name != nameof(IAccountDomainService.LoginByCookie))
                    throw new InvalidOperationException($"Unexpected API: {method.Name}");
                refresh();
                return Task.FromResult(TestDoubles.User());
            }
        );
        var vip = TestDoubles.Api<IVipPrivilegeDomainService>(
            (method, _) =>
                method.Name == nameof(IVipPrivilegeDomainService.ReceiveVipPrivilege)
                    ? Task.FromResult(result)
                    : throw new InvalidOperationException($"Unexpected API: {method.Name}")
        );

        return new VipPrivilegeTaskAppService(
            NullLogger<VipPrivilegeTaskAppService>.Instance,
            new TestDoubles.OptionsMonitor<VipPrivilegeOptions>(new() { IsEnable = true }),
            account,
            vip,
            TestDoubles.Api<ILoginDomainService>(
                (method, _) => throw new InvalidOperationException($"Unexpected API: {method.Name}")
            ),
            configuration,
            new CookieStrFactory<BiliCookie>(configuration)
        );
    }
}
