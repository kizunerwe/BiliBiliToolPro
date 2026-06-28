using Ray.BiliBiliTool.DomainService.Dtos;

namespace DomainServiceTest;

public class PassportTvPollStatusTest
{
    [Theory]
    [InlineData(PassportTvPollStatus.WaitingConfirm)]
    [InlineData(PassportTvPollStatus.ScannedUnconfirmed)]
    [InlineData(PassportTvPollStatus.Unscanned)]
    public void ShouldKeepWaiting_ShouldReturnTrue_ForPendingCodes(int code)
    {
        Assert.True(PassportTvPollStatus.ShouldKeepWaiting(code));
    }

    [Theory]
    [InlineData(PassportTvPollStatus.Success)]
    [InlineData(PassportTvPollStatus.Expired)]
    [InlineData(-1)]
    public void ShouldKeepWaiting_ShouldReturnFalse_ForNonPendingCodes(int code)
    {
        Assert.False(PassportTvPollStatus.ShouldKeepWaiting(code));
    }
}
