using Ray.BiliBiliTool.Agent;
using Ray.BiliBiliTool.Agent.BiliBiliAgent.Dtos.Passport;

namespace Ray.BiliBiliTool.DomainService.Dtos;

public sealed class PassportTvLoginResult(BiliCookie cookie, string accessKey)
{
    public BiliCookie Cookie { get; } = cookie;

    public string AccessKey { get; } = accessKey;

    public static PassportTvLoginResult Create(PassportTvPollData data)
    {
        string accessKey = data.access_token ?? data.token_info?.access_token ?? string.Empty;
        if (string.IsNullOrWhiteSpace(accessKey))
        {
            throw new InvalidOperationException("TV 登录结果中缺少 access_token");
        }

        if (data.cookie_info?.cookies == null || data.cookie_info.cookies.Count == 0)
        {
            throw new InvalidOperationException("TV 登录结果中缺少 cookie_info");
        }

        var cookieDictionary = data
            .cookie_info.cookies.Where(x => !string.IsNullOrWhiteSpace(x.name))
            .GroupBy(x => x.name)
            .ToDictionary(x => x.Key, x => x.Last().value ?? string.Empty);

        var cookie = new BiliCookie(cookieDictionary);
        cookie.Check();

        return new PassportTvLoginResult(cookie, accessKey);
    }
}
