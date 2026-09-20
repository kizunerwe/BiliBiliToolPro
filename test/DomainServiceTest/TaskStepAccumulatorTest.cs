using Ray.BiliBiliTool.Application;
using Ray.BiliBiliTool.Application.Contracts;
using Ray.BiliBiliTool.DomainService.Dtos;

namespace DomainServiceTest;

public sealed class TaskStepAccumulatorTest
{
    [Fact]
    public void SkippedSteps_ShouldNotFailTheTask()
    {
        var steps = new TaskStepAccumulator();

        steps.Add("已完成", TaskStepResult.Skip("今天已完成"));

        steps.ThrowIfFailed("每日任务");
    }

    [Fact]
    public void MultipleFailures_ShouldBeReportedWithStepNamesAndReasons()
    {
        var steps = new TaskStepAccumulator();
        steps.Add("文章投币", TaskStepResult.Fail("接口拒绝"));
        steps.Add("视频投币", TaskStepResult.Fail("请求超时"));

        var exception = CaptureFailure(steps, "每日任务");

        Assert.Contains("文章投币", exception.Message);
        Assert.Contains("接口拒绝", exception.Message);
        Assert.Contains("视频投币", exception.Message);
        Assert.Contains("请求超时", exception.Message);
    }

    [Fact]
    public async Task RunAsync_ShouldConvertExceptionsAndContinueSubsequentSteps()
    {
        var steps = new TaskStepAccumulator();
        var continued = false;

        await steps.RunAsync("第一步", () => throw new InvalidOperationException("请求失败"));
        await steps.RunAsync(
            "第二步",
            () =>
            {
                continued = true;
                return Task.FromResult(TaskStepResult.Skip("无需执行"));
            }
        );

        Assert.True(continued);
        var exception = CaptureFailure(steps, "测试任务");
        Assert.Contains("第一步", exception.Message);
        Assert.Contains("请求失败", exception.Message);
    }

    [Fact]
    public async Task RunValueAsync_ShouldReturnEmptyValueOnFailureAndContinue()
    {
        var steps = new TaskStepAccumulator();
        var continued = false;

        var failed = await steps.RunValueAsync<string>(
            "状态查询",
            () => throw new InvalidOperationException("状态接口不可用")
        );
        var succeeded = await steps.RunValueAsync(
            "后续步骤",
            () =>
            {
                continued = true;
                return Task.FromResult("继续执行");
            }
        );

        Assert.False(failed.Succeeded);
        Assert.Null(failed.Value);
        Assert.True(succeeded.Succeeded);
        Assert.Equal("继续执行", succeeded.Value);
        Assert.True(continued);
        _ = CaptureFailure(steps, "测试任务");
    }

    private static TaskExecutionException CaptureFailure(TaskStepAccumulator steps, string taskName)
    {
        try
        {
            steps.ThrowIfFailed(taskName);
        }
        catch (TaskExecutionException exception)
        {
            return exception;
        }

        throw new Xunit.Sdk.XunitException("Expected TaskExecutionException");
    }
}
