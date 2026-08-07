using Ray.BiliBiliTool.Application.Contracts;
using Ray.BiliBiliTool.DomainService.Dtos;

namespace Ray.BiliBiliTool.Application;

public sealed class TaskStepAccumulator
{
    private readonly List<(string Name, string Reason)> _failures = [];

    public async Task RunAsync(string name, Func<Task<TaskStepResult>> action)
    {
        try
        {
            Add(name, await action());
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Add(name, TaskStepResult.Fail(ex.Message));
        }
    }

    public async Task<TaskStepResult> RunResultAsync(string name, Func<Task<TaskStepResult>> action)
    {
        try
        {
            var result = await action();
            Add(name, result);
            return result;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            var result = TaskStepResult.Fail(ex.Message);
            Add(name, result);
            return result;
        }
    }

    public async Task<(bool Succeeded, T? Value)> RunValueAsync<T>(
        string name,
        Func<Task<T>> action
    )
    {
        try
        {
            var value = await action();
            Add(name, TaskStepResult.Success());
            return (true, value);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Add(name, TaskStepResult.Fail(ex.Message));
            return (false, default);
        }
    }

    public void Add(string name, TaskStepResult result)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (result.Status == TaskStepStatus.Failed)
        {
            _failures.Add(
                (name, string.IsNullOrWhiteSpace(result.Reason) ? "未提供失败原因" : result.Reason)
            );
        }
    }

    public void ThrowIfFailed(string taskName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(taskName);

        if (_failures.Count == 0)
            return;

        var details = string.Join(
            "；",
            _failures.Select(x => $"{x.Name}：{SanitizeReason(x.Reason)}")
        );
        throw new TaskExecutionException($"{taskName}失败：{details}");
    }

    private static string SanitizeReason(string reason)
    {
        var sanitized = reason.Replace("\r", " ").Replace("\n", " ").Trim();
        return sanitized.Length > 300 ? sanitized[..300] : sanitized;
    }
}
