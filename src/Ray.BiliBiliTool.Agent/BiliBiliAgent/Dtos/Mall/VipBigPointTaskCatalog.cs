namespace Ray.BiliBiliTool.Agent.BiliBiliAgent.Dtos.Mall;

public static class VipBigPointTaskCatalog
{
    private static readonly HashSet<string> UnsupportedAutomatedTaskCodes =
    [
        "vipmallbuy",
        "tvodbuy",
        "dressbuyamount",
    ];

    public static bool IsAutomationUnsupported(CommonTaskItem task)
    {
        return IsAutomationUnsupported(task.task_code, task.title);
    }

    public static bool IsAutomationUnsupported(string taskCode, string? title = null)
    {
        return UnsupportedAutomatedTaskCodes.Contains(taskCode)
            || (
                !string.IsNullOrWhiteSpace(title)
                && title.Contains("购买", StringComparison.Ordinal)
            );
    }
}
