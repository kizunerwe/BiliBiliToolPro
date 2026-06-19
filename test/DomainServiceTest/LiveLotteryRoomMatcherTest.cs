using System.Reflection;
using System.Text.Json;
using Ray.BiliBiliTool.Agent.BiliBiliAgent.Dtos;
using Ray.BiliBiliTool.Agent.BiliBiliAgent.Dtos.Live;

namespace DomainServiceTest;

public class LiveLotteryRoomMatcherTest
{
    private static bool InvokeIsPotentialLotteryRoom(ListItemDto item)
    {
        var matcherType = Type.GetType(
            "Ray.BiliBiliTool.DomainService.LiveLotteryRoomMatcher, Ray.BiliBiliTool.DomainService"
        );
        Assert.NotNull(matcherType);

        MethodInfo? method = matcherType!.GetMethod(
            "IsPotentialLotteryRoom",
            BindingFlags.Public | BindingFlags.Static
        );
        Assert.NotNull(method);

        return Assert.IsType<bool>(method!.Invoke(null, new object[] { item }));
    }

    [Fact]
    public void IsPotentialLotteryRoom_ShouldMatchNewTianXuanPendant()
    {
        var item = new ListItemDto
        {
            Roomid = 1771624336,
            Uid = 3546958450919751,
            Title = "下雨的时候特别想你",
            Uname = "测试主播",
            Parent_name = "娱乐",
            Pendant_info = new Dictionary<string, PendantInfo>
            {
                ["2"] = new PendantInfo { Pendent_id = 1432, Content = "天选之旅" },
            },
        };

        bool result = InvokeIsPotentialLotteryRoom(item);

        Assert.True(result);
    }

    [Fact]
    public void IsPotentialLotteryRoom_ShouldKeepSupportingLegacyTianXuanPendantId()
    {
        var item = new ListItemDto
        {
            Roomid = 1000,
            Uid = 2000,
            Title = "老版徽章",
            Uname = "测试主播",
            Parent_name = "娱乐",
            Pendant_info = new Dictionary<string, PendantInfo>
            {
                ["2"] = new PendantInfo { Pendent_id = 504, Content = "任意内容" },
            },
        };

        bool result = InvokeIsPotentialLotteryRoom(item);

        Assert.True(result);
    }

    [Fact]
    public void IsPotentialLotteryRoom_ShouldRejectNonLotteryPendant()
    {
        var item = new ListItemDto
        {
            Roomid = 1001,
            Uid = 2001,
            Title = "普通直播间",
            Uname = "测试主播",
            Parent_name = "娱乐",
            Pendant_info = new Dictionary<string, PendantInfo>
            {
                ["2"] = new PendantInfo { Pendent_id = 1818, Content = "SSS主播" },
            },
        };

        bool result = InvokeIsPotentialLotteryRoom(item);

        Assert.False(result);
    }

    [Fact]
    public void DeserializeGetRoomListResponse_ShouldAllowEmptyPendantInfoArray()
    {
        const string json = """
            {
              "code": 0,
              "message": "success",
              "data": {
                "count": 1,
                "list": [
                  {
                    "roomid": 27107871,
                    "uid": 277715923,
                    "title": "合约",
                    "uname": "什么事在睡觉",
                    "parent_id": 3,
                    "parent_name": "手游",
                    "area_id": 255,
                    "area_name": "明日方舟",
                    "pendant_info": []
                  }
                ]
              }
            }
            """;

        BiliApiResponse<GetRoomListResponse>? response = JsonSerializer.Deserialize<
            BiliApiResponse<GetRoomListResponse>
        >(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.NotNull(response);
        Assert.NotNull(response.Data);
        Assert.Single(response.Data.List);
        Assert.Empty(response.Data.List[0].Pendant_info ?? []);
    }
}
