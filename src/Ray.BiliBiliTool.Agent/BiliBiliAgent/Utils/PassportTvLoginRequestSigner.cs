using System.Security.Cryptography;
using System.Text;

namespace Ray.BiliBiliTool.Agent.BiliBiliAgent.Utils;

public static class PassportTvLoginRequestSigner
{
    public const string LoginAppKey = "783bbb7264451d82";
    public const string Build = "6720300";
    public const string Channel = "website";
    public const string MobiApp = "android";
    public const string Platform = "android";
    public const string CLocale = "zh-Hans_CN";
    public const string SLocale = "zh-Hans_CN";
    public const string UserAgent =
        "Mozilla/5.0 BiliDroid/6.72.0 (bbcallen@gmail.com) os/android model/XQ-CT72 mobi_app/android build/6720300 channel/bilih5 innerVer/6720310 osVer/12 network/2";

    private const string LoginSecretKey = "2653583c8873dea268ab9386918b1d65";

    public static string CreateLocalId()
    {
        string hash = ToMd5(Guid.NewGuid().ToString()).ToUpperInvariant();
        return $"XX{hash[2]}{hash[12]}{hash[22]}{hash}";
    }

    public static string BuildSignedQuery(string localId, long ts, string? prefix = null)
    {
        string loginQuery =
            $"appkey={LoginAppKey}&build={Build}&c_locale={CLocale}&channel={Channel}&local_id={localId}&mobi_app={MobiApp}&platform={Platform}&s_locale={SLocale}";
        string baseQuery = string.IsNullOrWhiteSpace(prefix)
            ? loginQuery
            : $"{prefix}&{loginQuery}";
        string sorted = string.Join("&", $"{baseQuery}&ts={ts}".Split('&').OrderBy(x => x));
        return $"{sorted}&sign={ToMd5(sorted + LoginSecretKey)}";
    }

    private static string ToMd5(string text)
    {
        using var md5 = MD5.Create();
        byte[] bytes = md5.ComputeHash(Encoding.UTF8.GetBytes(text));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
