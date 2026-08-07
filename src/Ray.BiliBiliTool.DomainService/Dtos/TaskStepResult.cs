namespace Ray.BiliBiliTool.DomainService.Dtos;

public enum TaskStepStatus
{
    Succeeded,
    Skipped,
    Failed,
}

public sealed record TaskStepResult(TaskStepStatus Status, string? Reason = null)
{
    public static TaskStepResult Success(string? reason = null) =>
        new(TaskStepStatus.Succeeded, reason);

    public static TaskStepResult Skip(string reason) =>
        new(TaskStepStatus.Skipped, RequireReason(reason));

    public static TaskStepResult Fail(string reason) =>
        new(TaskStepStatus.Failed, RequireReason(reason));

    private static string RequireReason(string reason) =>
        string.IsNullOrWhiteSpace(reason)
            ? throw new ArgumentException("原因不能为空", nameof(reason))
            : reason;
}
