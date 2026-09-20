using System.Text.Json;
using Ray.BiliBiliTool.Agent.BiliBiliAgent.Dtos;
using Ray.BiliBiliTool.Agent.BiliBiliAgent.Dtos.Live;

namespace DomainServiceTest;

public sealed class BiliApiResponseDataTest
{
    [Fact]
    public void ErrorEnvelope_ShouldAllowMissingData_AndPreserveBusinessDetails()
    {
        const string json = "{\"code\":-352,\"message\":\"风控校验失败\"}";
        var response = JsonSerializer.Deserialize<BiliApiResponse<Dictionary<string, string>>>(
            json,
            JsonSerializerOptionsBuilder.DefaultOptions
        );

        Assert.NotNull(response);
        Assert.Null(response.Data);
        Assert.Equal(-352, response.Code);
        Assert.Equal("风控校验失败", response.Message);
    }

    [Fact]
    public void ErrorEnvelope_ShouldAllowMissingData_ForBusinessDtos()
    {
        AssertMissingData<ChargeV2Response>();
        AssertMissingData<Silver2CoinResponse>();
        AssertMissingData<MangaVipRewardResponse>();
        AssertMissingData<Ranking>();
    }

    private static void AssertMissingData<T>()
    {
        var response = JsonSerializer.Deserialize<BiliApiResponse<T>>(
            "{\"code\":1001,\"message\":\"业务失败\"}",
            JsonSerializerOptionsBuilder.DefaultOptions
        );

        Assert.NotNull(response);
        Assert.Equal(1001, response.Code);
        Assert.Equal("业务失败", response.Message);
        Assert.Null(response.Data);
    }
}
