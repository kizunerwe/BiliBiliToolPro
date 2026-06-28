using System.Text.Json;
using Ray.BiliBiliTool.Agent.BiliBiliAgent.Dtos.Video;

namespace DomainServiceTest;

public class GetBangumiBySsidResponseTest
{
    [Fact]
    public void Deserialize_ShouldSupportLargeAidAndCidValues()
    {
        const string json = """
            {
              "code": 0,
              "message": "success",
              "result": {
                "episodes": [
                  {
                    "aid": 7104446040,
                    "bvid": "BV1PQ4y1N7V8",
                    "cid": 26835486927,
                    "duration": 1493033,
                    "ep_id": 321808,
                    "id": 321808,
                    "long_title": "云霄飞车杀人事件",
                    "share_copy": "《名侦探柯南》第1话 云霄飞车杀人事件",
                    "status": 2
                  }
                ]
              }
            }
            """;

        var response = JsonSerializer.Deserialize<GetBangumiBySsidResponse>(
            json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
        );

        Assert.NotNull(response);
        Assert.NotNull(response!.Result);
        Assert.Single(response.Result.episodes);
        Assert.Equal(7104446040, response.Result.episodes[0].aid);
        Assert.Equal(26835486927, response.Result.episodes[0].cid);
    }
}
