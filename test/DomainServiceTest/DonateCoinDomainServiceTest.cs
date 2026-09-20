namespace DomainServiceTest;

public class DonateCoinDomainServiceTest
{
    public DonateCoinDomainServiceTest()
    {
        Program.CreateHost(new[] { "--ENVIRONMENT=Development" });
    }

    [Fact]
    public void BuildSelectionPlan_ShouldDescribeSourceOrder()
    {
        var message = DonateCoinLogFormatter.BuildSelectionPlan();

        Assert.Equal("【选视频】按顺序尝试：配置UP -> 特别关注 -> 普通关注 -> 排行榜", message);
    }

    [Fact]
    public void BuildSourceFallback_ShouldDescribeNextSource()
    {
        var message = DonateCoinLogFormatter.BuildSourceFallback(
            DonateCoinVideoSource.SpecialFollowings,
            DonateCoinVideoSource.Followings
        );

        Assert.Equal("【选源】特别关注未找到可投视频，继续尝试普通关注", message);
    }

    [Fact]
    public void BuildSourceSkippedWithFallback_ShouldDescribeReasonAndNextSource()
    {
        var message = DonateCoinLogFormatter.BuildSourceSkippedWithFallback(
            DonateCoinVideoSource.ConfigUp,
            "已看完",
            DonateCoinVideoSource.SpecialFollowings
        );

        Assert.Equal("【选源】配置UP：已看完，继续尝试特别关注", message);
    }

    [Fact]
    public void BuildSourceSelected_ShouldUseReadableSourceName()
    {
        var message = DonateCoinLogFormatter.BuildSourceSelected(DonateCoinVideoSource.Ranking);

        Assert.Equal("【视频来源】排行榜", message);
    }

    [Fact]
    public void BuildRankingRiskWarning_ShouldMentionCaptchaRisk()
    {
        var message = DonateCoinLogFormatter.BuildRankingRiskWarning("B站返回 -352");

        Assert.Equal(
            "【选源】排行榜：获取失败，可能触发风控或验证码，已跳过。B站返回 -352",
            message
        );
    }

    [Fact]
    public void ConfigUpStatusMessages_ShouldDescribeTheScanState()
    {
        Assert.Equal("已确认无可投视频", DonateCoinLogFormatter.BuildConfigUpConfirmedExhausted());
        Assert.Equal(
            "扫描中断，保留当前页待重试",
            DonateCoinLogFormatter.BuildConfigUpRetryableFailure()
        );
        Assert.Equal("扫描进行中", DonateCoinLogFormatter.BuildConfigUpInProgress());
    }

    [Fact]
    public void ConfigUpStatusMessages_ShouldNotUseCompletedWordingForRetryableStates()
    {
        Assert.DoesNotContain("已看完", DonateCoinLogFormatter.BuildConfigUpRetryableFailure());
        Assert.DoesNotContain("已看完", DonateCoinLogFormatter.BuildConfigUpInProgress());
    }

    [Fact]
    public void BuildConfigUpProgress_ShouldSeparateHistoricalAndCurrentRunProgress()
    {
        var message = DonateCoinLogFormatter.BuildConfigUpProgress(
            487417170,
            historicalTerminalCount: 125,
            videoCount: 128,
            statusChecksThisRun: 3,
            pageNumber: 5,
            nextVideoIndex: 7,
            DonateCoinLogFormatter.BuildConfigUpInProgress()
        );

        Assert.Equal(
            "【配置UP】487417170：历史终态 125 / 当前视频 128，本轮接口检查 3，位置 第5页第8条，扫描进行中",
            message
        );
        Assert.DoesNotContain("已记录", message);
    }
}
