namespace Ray.BiliBiliTool.Agent.BiliBiliAgent.Dtos.Live;

public class GetRoomListRequest
{
    public string platform { get; set; } = "web";

    public long parent_area_id { get; set; }

    public long area_id { get; set; }

    public int page { get; set; }
}
