using Ray.BiliBiliTool.DomainService.Dtos;
using Ray.BiliBiliTool.DomainService.Interfaces;

namespace DomainServiceTest;

public sealed class TaskOutcomeContractTest
{
    [Theory]
    [InlineData(typeof(IVideoDomainService), "WatchAndShareVideo")]
    [InlineData(typeof(IDonateCoinDomainService), "AddCoinsForVideos")]
    [InlineData(typeof(IArticleDomainService), "AddCoinForArticles")]
    [InlineData(typeof(IVipPrivilegeDomainService), "ReceiveVipPrivilege")]
    [InlineData(typeof(ILiveDomainService), "SendDanmakuToFansMedalLive")]
    [InlineData(typeof(ILiveDomainService), "LikeFansMedalLive")]
    [InlineData(typeof(ILiveDomainService), "SendHeartBeatToFansMedalLive")]
    public void BusinessStepContracts_ShouldReturnTaskStepResult(
        Type serviceType,
        string methodName
    )
    {
        var method = serviceType.GetMethod(methodName);

        Assert.NotNull(method);
        Assert.Equal(typeof(Task<TaskStepResult>), method!.ReturnType);
    }
}
