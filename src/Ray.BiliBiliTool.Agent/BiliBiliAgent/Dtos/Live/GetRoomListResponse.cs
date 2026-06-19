namespace Ray.BiliBiliTool.Agent.BiliBiliAgent.Dtos.Live;

public class GetRoomListResponse
{
    public int Count { get; set; }

    public List<ListItemDto> List { get; set; } = [];
}
