using System.Text.Json;
using Ray.BiliBiliTool.Agent.BiliBiliAgent.Dtos;
using Ray.BiliBiliTool.Agent.BiliBiliAgent.Dtos.VipTask;

namespace DomainServiceTest;

public class StartOgvWatchResponseTest
{
    [Fact]
    public void Deserialize_ShouldReadTaskIdAndTokenFromWatchCountDownConfig()
    {
        const string json = """
            {
              "code": 0,
              "message": "success",
              "data": {
                "closeType": "close_win",
                "showTime": "",
                "watch_count_down_cfg": {
                  "milliseconds": 600000,
                  "task_id": "4320003",
                  "token": "67ba5888e7"
                }
              }
            }
            """;

        var response = JsonSerializer.Deserialize<BiliApiResponse<StartOgvWatchResponse>>(
            json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
        );

        Assert.NotNull(response);
        Assert.NotNull(response!.Data);
        Assert.Equal(4320003, response.Data.task_id);
        Assert.Equal("67ba5888e7", response.Data.token);
        Assert.Equal(600000, response.Data.CountdownMilliseconds);
        Assert.True(response.Data.HasValidTaskContext);
    }

    [Fact]
    public void HasValidTaskContext_ShouldBeFalse_WhenTaskIdIsInvalid()
    {
        var response = new StartOgvWatchResponse
        {
            watch_count_down_cfg = new WatchCountDownConfig
            {
                task_id = "oops",
                token = "67ba5888e7",
            },
        };

        Assert.Equal(0, response.task_id);
        Assert.False(response.HasValidTaskContext);
    }

    [Fact]
    public void HasValidTaskContext_ShouldBeFalse_WhenTokenIsMissing()
    {
        var response = new StartOgvWatchResponse
        {
            watch_count_down_cfg = new WatchCountDownConfig { task_id = "4320003", token = "" },
        };

        Assert.Equal(4320003, response.task_id);
        Assert.False(response.HasValidTaskContext);
    }
}
