using Ray.BiliBiliTool.DomainService.Interfaces;

namespace Ray.BiliBiliTool.DomainService;

public sealed class TaskDelay : ITaskDelay
{
    public Task Delay(TimeSpan delay, CancellationToken cancellationToken) =>
        Task.Delay(delay, cancellationToken);
}
