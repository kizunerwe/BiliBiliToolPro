using Ray.BiliBiliTool.Agent.BiliBiliAgent.Dtos.Passport;
using Ray.BiliBiliTool.DomainService.Dtos;

namespace DomainServiceTest;

public class PassportTvLoginResultTest
{
    [Fact]
    public void Create_ShouldBuildBiliCookieAndAccessKey()
    {
        var data = new PassportTvPollData
        {
            access_token = "access-key-demo",
            cookie_info = new PassportTvCookieInfo
            {
                cookies =
                [
                    new PassportTvCookieItem { name = "DedeUserID", value = "565140580" },
                    new PassportTvCookieItem { name = "SESSDATA", value = "sess" },
                    new PassportTvCookieItem { name = "bili_jct", value = "csrf" },
                    new PassportTvCookieItem { name = "buvid3", value = "buvid" },
                ],
            },
        };

        PassportTvLoginResult result = PassportTvLoginResult.Create(data);

        Assert.Equal("access-key-demo", result.AccessKey);
        Assert.Equal("565140580", result.Cookie.UserId);
        Assert.Equal("sess", result.Cookie.SessData);
        Assert.Equal("csrf", result.Cookie.BiliJct);
    }

    [Fact]
    public void Create_ShouldFallbackToTokenInfoAccessToken()
    {
        var data = new PassportTvPollData
        {
            token_info = new PassportTvTokenInfo
            {
                access_token = "token-info-demo",
                refresh_token = "refresh",
            },
            cookie_info = new PassportTvCookieInfo
            {
                cookies =
                [
                    new PassportTvCookieItem { name = "DedeUserID", value = "565140580" },
                    new PassportTvCookieItem { name = "SESSDATA", value = "sess" },
                    new PassportTvCookieItem { name = "bili_jct", value = "csrf" },
                ],
            },
        };

        PassportTvLoginResult result = PassportTvLoginResult.Create(data);

        Assert.Equal("token-info-demo", result.AccessKey);
    }
}
