using Ray.BiliBiliTool.Agent.BiliBiliAgent.Utils;

namespace DomainServiceTest;

public class PassportTvLoginRequestSignerTest
{
    [Fact]
    public void CreateBuvid_ShouldUseExpectedPrefixAndLength()
    {
        string buvid = PassportTvLoginRequestSigner.CreateLocalId();

        Assert.StartsWith("XX", buvid);
        Assert.Equal(37, buvid.Length);
    }

    [Fact]
    public void BuildSignedAuthQuery_ShouldMatchStableReferenceShape()
    {
        string query = PassportTvLoginRequestSigner.BuildSignedQuery(
            "XXTESTLOCALID12345678901234567890123",
            1735744760
        );

        Assert.Equal(
            "appkey=783bbb7264451d82&build=6720300&c_locale=zh-Hans_CN&channel=website&local_id=XXTESTLOCALID12345678901234567890123&mobi_app=android&platform=android&s_locale=zh-Hans_CN&ts=1735744760&sign=0a0dfc9b4afd61043c9f719f9cb5ba82",
            query
        );
    }

    [Fact]
    public void BuildSignedPollQuery_ShouldPrependAuthCode()
    {
        string query = PassportTvLoginRequestSigner.BuildSignedQuery(
            "XXTESTLOCALID12345678901234567890123",
            1735744760,
            "auth_code=abcdef"
        );

        Assert.StartsWith("appkey=783bbb7264451d82", query);
        Assert.Contains("auth_code=abcdef", query);
        Assert.Contains("local_id=XXTESTLOCALID12345678901234567890123", query);
        Assert.EndsWith("sign=e9025f54bdbb0d5e21f9c62ee4ed1ad1", query);
    }
}
