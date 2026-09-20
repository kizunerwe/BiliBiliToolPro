using Microsoft.Extensions.Logging.Abstractions;
using Ray.BiliBiliTool.Agent.BiliBiliAgent.Dtos;
using Ray.BiliBiliTool.Agent.BiliBiliAgent.Interfaces;
using Ray.BiliBiliTool.Config.Options;
using Ray.BiliBiliTool.DomainService;
using Ray.BiliBiliTool.DomainService.Dtos;

namespace DomainServiceTest;

public sealed class VipPrivilegeDomainServiceOutcomeTest
{
    [Fact]
    public async Task AlreadyClaimedPrivileges_ShouldBeSkipped()
    {
        var service = CreateService(69801);

        var result = await service.ReceiveVipPrivilege(
            TestDoubles.User(VipType.Annual),
            TestDoubles.Cookie()
        );

        Assert.Equal(TaskStepStatus.Skipped, result.Status);
    }

    [Fact]
    public async Task OneSuccessfulPrivilege_ShouldBeSucceeded()
    {
        var service = CreateService(0, 69801);

        var result = await service.ReceiveVipPrivilege(
            TestDoubles.User(VipType.Annual),
            TestDoubles.Cookie()
        );

        Assert.Equal(TaskStepStatus.Succeeded, result.Status);
    }

    [Fact]
    public async Task PartialPrivilegeFailure_ShouldRemainFailed()
    {
        var service = CreateService(0, 1001);

        var result = await service.ReceiveVipPrivilege(
            TestDoubles.User(VipType.Annual),
            TestDoubles.Cookie()
        );

        Assert.Equal(TaskStepStatus.Failed, result.Status);
        Assert.Contains("结果", result.Reason);
    }

    [Fact]
    public async Task NonAnnualMember_ShouldBeSkippedWithoutCallingApi()
    {
        var calls = 0;
        var service = CreateService(0, 0, () => calls++);

        var result = await service.ReceiveVipPrivilege(
            TestDoubles.User(VipType.Mensual),
            TestDoubles.Cookie()
        );

        Assert.Equal(TaskStepStatus.Skipped, result.Status);
        Assert.Equal(0, calls);
    }

    private static VipPrivilegeDomainService CreateService(params int[] codes) =>
        CreateService(codes, null);

    private static VipPrivilegeDomainService CreateService(
        int firstCode,
        int secondCode,
        Action? onCall = null
    ) => CreateService([firstCode, secondCode], onCall);

    private static VipPrivilegeDomainService CreateService(int[] codes, Action? onCall)
    {
        var index = 0;
        var api = TestDoubles.Api<IDailyTaskApi>(
            (method, _) =>
            {
                if (method.Name != nameof(IDailyTaskApi.ReceiveVipPrivilegeAsync))
                    throw new InvalidOperationException($"Unexpected API: {method.Name}");
                onCall?.Invoke();
                var code = codes[Math.Min(index++, codes.Length - 1)];
                return Task.FromResult(new BiliApiResponse { Code = code, Message = "结果" });
            }
        );

        return new VipPrivilegeDomainService(
            NullLogger<VipPrivilegeDomainService>.Instance,
            api,
            new TestDoubles.OptionsMonitor<VipPrivilegeOptions>(new() { IsEnable = true })
        );
    }
}
