using Microsoft.Extensions.Logging;
using Ray.BiliBiliTool.Agent;
using Ray.BiliBiliTool.Agent.BiliBiliAgent.Dtos;
using Ray.BiliBiliTool.Agent.BiliBiliAgent.Interfaces;
using Ray.BiliBiliTool.Config.Options;
using Ray.BiliBiliTool.DomainService;

namespace DomainServiceTest;

public sealed class MangaApiErrorContractTest
{
    [Fact]
    public async Task ReceiveMangaVipReward_ShouldNotReportSuccess_WhenErrorResponseHasData()
    {
        var logger = new ListLogger<MangaDomainService>();
        var mangaApi = TestDoubles.Api<IMangaApi>(
            (method, _) =>
                method.Name == nameof(IMangaApi.ReceiveMangaVipReward)
                    ? Task.FromResult(
                        new BiliApiResponse<MangaVipRewardResponse>
                        {
                            Code = 1001,
                            Message = "领取失败",
                            Data = new MangaVipRewardResponse { Amount = 10 },
                        }
                    )
                    : throw new InvalidOperationException($"Unexpected API: {method.Name}")
        );
        var service = new MangaDomainService(
            logger,
            mangaApi,
            new TestDoubles.OptionsMonitor<MangaTaskOptions>(new()),
            new TestDoubles.OptionsMonitor<DailyTaskOptions>(new()),
            new TestDoubles.OptionsMonitor<VipPrivilegeOptions>(new())
        );

        await service.ReceiveMangaVipReward(
            1,
            TestDoubles.User(VipType.Annual),
            TestDoubles.Cookie()
        );

        Assert.Contains(logger.Messages, message => message.Contains("领取结果】失败"));
        Assert.DoesNotContain(logger.Messages, message => message.Contains("领取结果】成功"));
    }

    private sealed class ListLogger<T> : ILogger<T>
    {
        public List<string> Messages { get; } = [];

        public IDisposable BeginScope<TState>(TState state)
            where TState : notnull => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter
        ) => Messages.Add(formatter(state, exception));

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();

            public void Dispose() { }
        }
    }
}
