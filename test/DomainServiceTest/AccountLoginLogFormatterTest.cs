using Ray.BiliBiliTool.Agent.BiliBiliAgent.Dtos;
using Ray.BiliBiliTool.DomainService;

namespace DomainServiceTest;

public class AccountLoginLogFormatterTest
{
    [Fact]
    public void Should_build_compact_login_summary_for_non_lv6_user()
    {
        var userInfo = new UserInfo
        {
            Mid = 123456,
            Uname = "kizunerwe",
            Money = 497.5m,
            VipType = VipType.Mensual,
            VipStatus = VipStatus.Disable,
            Level_info = new LevelInfo
            {
                Current_level = 5,
                Current_exp = 12345,
                Next_exp = 28800,
            },
            Wallet = new(),
            Wbi_img = new() { img_url = "", sub_url = "" },
        };

        var accountSummary = AccountLoginLogFormatter.BuildAccountSummary(userInfo);
        var progressSummary = AccountLoginLogFormatter.BuildProgressSummary(userInfo, 56);

        Assert.Equal("kizunerwe | UID 123456 | 月度大会员 / 已过期", accountSummary);
        Assert.Equal("Lv5 | 12345/28800 | 硬币 497.5 | Lv6 预估 56 天", progressSummary);
    }

    [Fact]
    public void Should_omit_estimate_for_lv6_user()
    {
        var userInfo = new UserInfo
        {
            Mid = 10001,
            Uname = "lv6user",
            Money = 88m,
            VipType = VipType.Annual,
            VipStatus = VipStatus.Enable,
            Level_info = new LevelInfo
            {
                Current_level = 6,
                Current_exp = 99999,
                Next_exp = "--",
            },
            Wallet = new(),
            Wbi_img = new() { img_url = "", sub_url = "" },
        };

        var progressSummary = AccountLoginLogFormatter.BuildProgressSummary(userInfo);

        Assert.Equal("Lv6 | 经验 99999 | 硬币 88", progressSummary);
    }
}
