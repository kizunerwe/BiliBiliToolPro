namespace Ray.BiliBiliTool.DomainService.Interfaces;

public interface ITaskDelay
{
    Task Delay(TimeSpan delay, CancellationToken cancellationToken);
}
