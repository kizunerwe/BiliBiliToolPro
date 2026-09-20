using Ray.BiliBiliTool.DomainService.Dtos;

namespace DomainServiceTest;

public sealed class TaskStepResultTest
{
    [Fact]
    public void Factories_ShouldCreateTheExpectedStatuses()
    {
        Assert.Equal(TaskStepStatus.Succeeded, TaskStepResult.Success().Status);
        Assert.Equal(TaskStepStatus.Skipped, TaskStepResult.Skip("已完成").Status);
        Assert.Equal(TaskStepStatus.Failed, TaskStepResult.Fail("业务拒绝").Status);
    }

    [Fact]
    public void FailedResult_ShouldRequireAReason()
    {
        Assert.Throws<ArgumentException>(() => TaskStepResult.Fail(" "));
    }
}
