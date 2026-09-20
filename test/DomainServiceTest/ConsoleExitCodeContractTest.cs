namespace DomainServiceTest;

using Microsoft.Extensions.Hosting;

[CollectionDefinition("ConsoleMain", DisableParallelization = true)]
public sealed class ConsoleMainCollection { }

[Collection("ConsoleMain")]
public sealed class ConsoleExitCodeContractTest
{
    [Fact]
    public void HostedService_ShouldUseCancelableBackgroundServiceLifecycle()
    {
        Assert.True(
            typeof(BackgroundService).IsAssignableFrom(
                typeof(Ray.BiliBiliTool.Console.BiliBiliToolHostedService)
            )
        );
    }

    [Fact]
    public async Task Main_ShouldReturnZero_WhenSecurityGateSkipsExecution()
    {
        var exitCode = await Ray.BiliBiliTool.Console.Program.Main([
            "--Security:IsSkipDailyTask=true",
            "--Security:RandomSleepMaxMin=0",
        ]);

        Assert.Equal(0, exitCode);
    }

    [Fact]
    public async Task Main_ShouldReturnOne_WhenRequestedTaskCodeIsInvalid()
    {
        var exitCode = await Ray.BiliBiliTool.Console.Program.Main([
            "--RunTasks=NoSuchTask",
            "--Security:RandomSleepMaxMin=0",
        ]);

        Assert.Equal(1, exitCode);
    }
}
