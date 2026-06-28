namespace Ray.BiliBiliTool.DomainService.Dtos;

public static class PassportTvPollStatus
{
    public const int Success = 0;
    public const int Expired = 86038;
    public const int WaitingConfirm = 86039;
    public const int ScannedUnconfirmed = 86090;
    public const int Unscanned = 86101;

    public static bool ShouldKeepWaiting(int code)
    {
        return code is WaitingConfirm or ScannedUnconfirmed or Unscanned;
    }
}
