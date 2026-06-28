using Ray.BiliBiliTool.Agent.BiliBiliAgent.Utils;

namespace Ray.BiliBiliTool.Agent.BiliBiliAgent.Dtos.VipTask;

public class StartOgvWatchRequest(long epId, long seasonId, string csrf)
{
    public string activity_code { get; } = "";
    public string? access_key { get; set; }
    public string? actionKey { get; set; }
    public string? appkey { get; set; }
    public string build { get; } = VipBigPointAppRequestSigner.Build;
    public string c_locale { get; } = "zh_CN";
    public string channel { get; } = Constants.Channel;
    public string? csrf { get; set; } = csrf;
    public string device { get; } = "android";
    public int disable_rcmd { get; } = 0;
    public long ep_id { get; } = epId;
    public string from_spmid { get; } = "search.search-result.0.0";
    public string mobi_app { get; } = "android";
    public string platform { get; } = "android";
    public string s_locale { get; } = "zh_CN";
    public long season_id { get; } = seasonId;
    public string spmid { get; } = "united.player-video-detail.0.0";
    public string? sign { get; set; }
    public long ts { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
}
