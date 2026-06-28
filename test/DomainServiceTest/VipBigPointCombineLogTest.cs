using Microsoft.Extensions.Logging;
using Ray.BiliBiliTool.Agent.BiliBiliAgent.Dtos.Mall;

namespace DomainServiceTest;

public class VipBigPointCombineLogTest
{
    [Fact]
    public void LogFullInfo_ShouldHideUnsupportedPurchaseTasks()
    {
        var logger = new ListLogger();
        var combine = new VipBigPointCombine
        {
            point_info = new PointInfo(8470, 0, 0, 0),
            Task_info = new TaskInfo
            {
                Sing_task_item = new SingTaskItem(),
                Modules =
                [
                    new ModuleItem
                    {
                        module_title = "日常任务",
                        common_task_item =
                        [
                            new CommonTaskItem
                            {
                                title = "浏览装扮商城主页",
                                task_code = "dress-view",
                                state = 3,
                            },
                            new CommonTaskItem
                            {
                                title = "购买指定会员购商品",
                                task_code = "vipmallbuy",
                                state = 0,
                            },
                            new CommonTaskItem
                            {
                                title = "购买单点付费影片",
                                task_code = "tvodbuy",
                                state = 0,
                            },
                            new CommonTaskItem
                            {
                                title = "购买指定装扮商品",
                                task_code = "dressbuyamount",
                                state = 0,
                            },
                        ],
                    },
                ],
            },
        };

        combine.LogFullInfo(logger);

        Assert.Contains(logger.Messages, x => x.Contains("浏览装扮商城主页"));
        Assert.DoesNotContain(logger.Messages, x => x.Contains("购买指定会员购商品"));
        Assert.DoesNotContain(logger.Messages, x => x.Contains("购买单点付费影片"));
        Assert.DoesNotContain(logger.Messages, x => x.Contains("购买指定装扮商品"));
    }

    private sealed class ListLogger : ILogger
    {
        public List<string> Messages { get; } = [];

        public IDisposable BeginScope<TState>(TState state)
            where TState : notnull => NullDisposable.Instance;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter
        )
        {
            Messages.Add(formatter(state, exception));
        }

        private sealed class NullDisposable : IDisposable
        {
            public static readonly NullDisposable Instance = new();

            public void Dispose() { }
        }
    }
}
