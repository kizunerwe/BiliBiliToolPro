namespace Ray.BiliBiliTool.Agent.BiliBiliAgent.Dtos.Favorite;

public sealed class GetFavoriteFoldersRequest(long aid, string userId)
{
    public int type { get; set; } = 2;
    public long rid { get; set; } = aid;
    public string up_mid { get; set; } = userId;
    public string web_location { get; set; } = "333.788";
}
