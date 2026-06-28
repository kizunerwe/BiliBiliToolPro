using Ray.BiliBiliTool.Agent.BiliBiliAgent.Utils;

namespace Ray.BiliBiliTool.Agent.BiliBiliAgent.Dtos.Passport;

public class PassportTvQrCodePollRequest(string localId, string authCode)
{
    public string appkey { get; } = PassportTvLoginRequestSigner.LoginAppKey;
    public string auth_code { get; } = authCode;
    public string build { get; } = PassportTvLoginRequestSigner.Build;
    public string c_locale { get; } = PassportTvLoginRequestSigner.CLocale;
    public string channel { get; } = PassportTvLoginRequestSigner.Channel;
    public string local_id { get; } = localId;
    public string mobi_app { get; } = PassportTvLoginRequestSigner.MobiApp;
    public string platform { get; } = PassportTvLoginRequestSigner.Platform;
    public string s_locale { get; } = PassportTvLoginRequestSigner.SLocale;
    public long ts { get; set; }
    public string? sign { get; set; }
}
