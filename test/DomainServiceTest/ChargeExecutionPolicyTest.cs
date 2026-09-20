namespace DomainServiceTest;

public sealed class ChargeExecutionPolicyTest
{
    [Theory]
    [InlineData("2026-02-27T12:00:00+08:00", false)]
    [InlineData("2026-02-28T12:00:00+08:00", true)]
    [InlineData("2028-02-28T12:00:00+08:00", false)]
    [InlineData("2028-02-29T12:00:00+08:00", true)]
    [InlineData("2026-04-30T12:00:00+08:00", true)]
    [InlineData("2026-07-31T12:00:00+08:00", true)]
    public void IsMonthEnd_ShouldUseBusinessTimeZone(string now, bool expected)
    {
        var current = DateTimeOffset.Parse(now);
        var policy = new ChargeExecutionPolicy(new FixedTimeProvider(current.ToUniversalTime()));

        Assert.Equal(expected, policy.IsMonthEnd("Asia/Shanghai"));
    }
}

internal sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => utcNow;
}
