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

    public static string BuildConfigUpConfirmedExhausted() => "已确认无可投视频";

    public static string BuildConfigUpRetryableFailure() => "扫描中断，保留当前页待重试";

    public static string BuildConfigUpInProgress() => "扫描进行中";

    public static string BuildConfigUpProgress(
        long upId,
        int historicalTerminalCount,
        int videoCount,
        int statusChecksThisRun,
        int pageNumber,
        int nextVideoIndex,
        string status
    )
    {
        var position =
            pageNumber <= 0 ? "扫描位置已到末尾" : $"位置 第{pageNumber}页第{nextVideoIndex + 1}条";
        return $"【配置UP】{upId}：历史终态 {historicalTerminalCount} / 当前视频 {videoCount}，本轮接口检查 {statusChecksThisRun}，{position}，{status}";
    }

    public static string BuildConfigUpPageSummary(
        long upId,
        int pageNumber,
        int checkedThisSegment,
        int historicalTerminalSkipped,
        int alreadyDonated,
        int nextVideoIndex,
        int pageVideoCount
    )
    {
        var remaining = Math.Max(0, pageVideoCount - nextVideoIndex);
        var position =
            remaining == 0
                ? "本页已完成"
                : $"下一位置 第{pageNumber}页第{nextVideoIndex + 1}条，本页剩余 {remaining} 个";
        return $"【配置UP】{upId} 第{pageNumber}页汇总：本段接口检查 {checkedThisSegment} 个，历史跳过 {historicalTerminalSkipped} 个，新确认已投币 {alreadyDonated} 个，{position}";
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
