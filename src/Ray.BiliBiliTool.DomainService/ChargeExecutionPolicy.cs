namespace Ray.BiliBiliTool.DomainService;

public sealed class ChargeExecutionPolicy(TimeProvider timeProvider)
{
    private readonly TimeProvider _timeProvider = timeProvider;

    public bool IsMonthEnd(string timeZoneId)
    {
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        var today = TimeZoneInfo.ConvertTime(_timeProvider.GetUtcNow(), timeZone).Date;
        return today.Day == DateTime.DaysInMonth(today.Year, today.Month);
    }
}
