namespace Ray.BiliBiliTool.Agent.BiliBiliAgent.Dtos.Favorite;

public sealed class DealFavoriteResourceRequest(
    long aid,
    long folderId,
    string fromSpmid,
    string spmidValue,
    string statisticsValue,
    string csrfToken
)
{
    public long rid { get; set; } = aid;
    public int type { get; set; } = 2;
    public string add_media_ids { get; set; } = folderId.ToString();
    public string del_media_ids { get; set; } = "";
    public string platform { get; set; } = "web";
    public string from_spmid { get; set; } = fromSpmid;
    public string spmid { get; set; } = spmidValue;
    public string statistics { get; set; } = statisticsValue;
    public string csrf { get; set; } = csrfToken;
}
