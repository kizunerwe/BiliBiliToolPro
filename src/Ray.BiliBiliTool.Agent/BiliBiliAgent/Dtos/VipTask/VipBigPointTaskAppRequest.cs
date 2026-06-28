using Ray.BiliBiliTool.Agent.BiliBiliAgent.Utils;

namespace Ray.BiliBiliTool.Agent.BiliBiliAgent.Dtos.VipTask;

public class VipBigPointTaskAppRequest(string taskCode, string csrf)
{
    public string? access_key { get; set; }
    public string? actionKey { get; set; }
    public string? appkey { get; set; }
    public string build { get; } = VipBigPointAppRequestSigner.Build;
    public string c_locale { get; } = "zh_CN";
    public string channel { get; } = Constants.Channel;
    public string? csrf { get; set; } = csrf;
    public string device { get; } = "android";
    public int disable_rcmd { get; } = 0;
    public string mobi_app { get; } = "android";
    public string platform { get; } = "android";
    public string s_locale { get; } = "zh_CN";
    public string? sign { get; set; }
    public string taskCode { get; set; } = taskCode;
    public long ts { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
}
