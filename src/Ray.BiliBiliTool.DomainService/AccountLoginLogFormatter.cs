using Ray.BiliBiliTool.Agent.BiliBiliAgent.Dtos;

namespace Ray.BiliBiliTool.DomainService;

public static class AccountLoginLogFormatter
{
    public static string BuildAccountSummary(UserInfo userInfo)
    {
        var uname = userInfo.Uname ?? "";
        return $"{uname} | UID {userInfo.Mid} | {BuildVipSummary(userInfo)}";
    }

    public static string BuildProgressSummary(UserInfo userInfo, int? estimatedDays = null)
    {
        var parts = new List<string>();
        var level = userInfo.Level_info?.Current_level ?? 0;

        parts.Add($"Lv{level}");

        if (userInfo.Level_info is { } levelInfo)
        {
            if (level < 6)
            {
                parts.Add($"{levelInfo.Current_exp}/{levelInfo.GetNext_expLong()}");
            }
            else
            {
                parts.Add($"经验 {levelInfo.Current_exp}");
            }
        }

        parts.Add($"硬币 {userInfo.Money ?? 0}");

        if (estimatedDays.HasValue && level is > 0 and < 6)
        {
            parts.Add($"Lv{level + 1} 预估 {estimatedDays.Value} 天");
        }

        return string.Join(" | ", parts);
    }

    private static string BuildVipSummary(UserInfo userInfo)
    {
        if (userInfo.VipStatus == VipStatus.Enable)
        {
            return $"{GetVipTypeText(userInfo.GetVipType())} / 正常";
        }

        if (userInfo.VipType == VipType.None)
        {
            return "无会员";
        }

        return $"{GetVipTypeText(userInfo.VipType)} / 已过期";
    }

    private static string GetVipTypeText(VipType vipType)
    {
        return vipType switch
        {
            VipType.Mensual => "月度大会员",
            VipType.Annual => "年度大会员",
            _ => "无会员",
        };
    }
}
