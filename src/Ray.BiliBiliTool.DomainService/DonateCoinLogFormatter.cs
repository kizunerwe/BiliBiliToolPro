namespace Ray.BiliBiliTool.DomainService;

public static class DonateCoinLogFormatter
{
    public static string BuildSelectionPlan()
    {
        return "【选视频】按顺序尝试：配置UP -> 特别关注 -> 普通关注 -> 排行榜";
    }

    public static string BuildSourceSelected(DonateCoinVideoSource source)
    {
        return $"【视频来源】{GetSourceName(source)}";
    }

    public static string BuildSourceSkipped(DonateCoinVideoSource source, string reason)
    {
        return $"【选源】{GetSourceName(source)}：{reason}";
    }

    public static string BuildSourceSkippedWithFallback(
        DonateCoinVideoSource source,
        string reason,
        DonateCoinVideoSource nextSource
    )
    {
        return $"【选源】{GetSourceName(source)}：{reason}，继续尝试{GetSourceName(nextSource)}";
    }

    public static string BuildSourceFallback(
        DonateCoinVideoSource source,
        DonateCoinVideoSource? nextSource = null
    )
    {
        var current = GetSourceName(source);

        if (!nextSource.HasValue)
        {
            return $"【选源】{current}未找到可投视频";
        }

        return $"【选源】{current}未找到可投视频，继续尝试{GetSourceName(nextSource.Value)}";
    }

    public static string BuildRankingRiskWarning(string detail)
    {
        return $"【选源】排行榜：获取失败，可能触发风控或验证码，已跳过。{detail}";
    }

    private static string GetSourceName(DonateCoinVideoSource source)
    {
        return source switch
        {
            DonateCoinVideoSource.ConfigUp => "配置UP",
            DonateCoinVideoSource.SpecialFollowings => "特别关注",
            DonateCoinVideoSource.Followings => "普通关注",
            DonateCoinVideoSource.Ranking => "排行榜",
            _ => "未知来源",
        };
    }
}

public enum DonateCoinVideoSource
{
    ConfigUp,
    SpecialFollowings,
    Followings,
    Ranking,
}
