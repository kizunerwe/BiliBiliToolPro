using Ray.BiliBiliTool.Agent.BiliBiliAgent.Dtos;
using Ray.BiliBiliTool.Agent.BiliBiliAgent.Interfaces;
using Ray.BiliBiliTool.DomainService;

namespace DomainServiceTest;

public sealed class CoinDomainServiceTest
{
    [Fact]
    public async Task MissingDonateCoinExpData_ShouldFailInsteadOfBecomingZero()
    {
        var dailyTaskApi = TestDoubles.Api<IDailyTaskApi>(
            (method, _) =>
                method.Name == nameof(IDailyTaskApi.GetDonateCoinExpAsync)
                    ? Task.FromResult(new BiliApiResponse<int?> { Code = 0, Data = null })
                    : throw new InvalidOperationException($"Unexpected API: {method.Name}")
        );
        var service = new CoinDomainService(null!, dailyTaskApi);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.GetDonatedCoins(TestDoubles.Cookie())
        );
    }
}
