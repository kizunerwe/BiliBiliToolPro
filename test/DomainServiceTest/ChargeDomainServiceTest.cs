using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Ray.BiliBiliTool.Agent;
using Ray.BiliBiliTool.Agent.BiliBiliAgent.Dtos;
using Ray.BiliBiliTool.Agent.BiliBiliAgent.Interfaces;
using Ray.BiliBiliTool.Config.Options;
using Ray.BiliBiliTool.DomainService.Dtos;

namespace DomainServiceTest;

public sealed class ChargeDomainServiceTest
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("-1")]
    [InlineData("0")]
    [InlineData("not-a-number")]
    [InlineData("10001")]
    public async Task Charge_ShouldFailWithoutCallingApi_WhenTargetIsInvalid(string? target)
    {
        var chargeApi = new FakeChargeApi();
        var service = CreateService(chargeApi, "2026-04-30T12:00:00+08:00", target);

        var result = await service.Charge(CreateUser(VipType.Annual, 2), CreateCookie("10001"));

        Assert.Equal(TaskStepStatus.Failed, result.Status);
        Assert.Equal(0, chargeApi.ChargeCallCount);
    }

    [Fact]
    public async Task Charge_ShouldSendOnlyTheExplicitTarget()
    {
        var chargeApi = new FakeChargeApi();
        var service = CreateService(chargeApi, "2026-04-30T12:00:00+08:00", "20002");

        var result = await service.Charge(CreateUser(VipType.Annual, 2), CreateCookie("10001"));

        Assert.Equal(TaskStepStatus.Succeeded, result.Status);
        Assert.Equal(1, chargeApi.ChargeCallCount);
        Assert.Equal(20002, chargeApi.LastChargeRequest!.Up_mid);
    }

    [Fact]
    public async Task Charge_ShouldFail_WhenBusinessTimeZoneIsInvalid()
    {
        var chargeApi = new FakeChargeApi();
        var service = CreateService(
            chargeApi,
            "2026-04-30T12:00:00+08:00",
            "20002",
            "Invalid/BusinessTimeZone"
        );

        var result = await service.Charge(CreateUser(VipType.Annual, 2), CreateCookie("10001"));

        Assert.Equal(TaskStepStatus.Failed, result.Status);
        Assert.Equal(0, chargeApi.ChargeCallCount);
    }

    [Fact]
    public async Task Charge_ShouldFail_WhenBusinessTimeZoneIsMissing()
    {
        var chargeApi = new FakeChargeApi();
        var service = CreateService(chargeApi, "2026-04-30T12:00:00+08:00", "20002", null);

        var result = await service.Charge(CreateUser(VipType.Annual, 2), CreateCookie("10001"));

        Assert.Equal(TaskStepStatus.Failed, result.Status);
        Assert.Contains("业务时区无效", result.Reason);
        Assert.Equal(0, chargeApi.ChargeCallCount);
    }

    [Fact]
    public async Task Charge_ShouldSkip_WhenAccountIsNotAnnualVip()
    {
        var chargeApi = new FakeChargeApi();
        var service = CreateService(chargeApi, "2026-04-30T12:00:00+08:00", "20002");

        var result = await service.Charge(CreateUser(VipType.Mensual, 2), CreateCookie("10001"));

        Assert.Equal(TaskStepStatus.Skipped, result.Status);
        Assert.Equal(0, chargeApi.ChargeCallCount);
    }

    [Fact]
    public async Task Charge_ShouldSkip_WhenTodayIsNotMonthEnd()
    {
        var chargeApi = new FakeChargeApi();
        var service = CreateService(chargeApi, "2026-04-29T12:00:00+08:00", "20002");

        var result = await service.Charge(CreateUser(VipType.Annual, 2), CreateCookie("10001"));

        Assert.Equal(TaskStepStatus.Skipped, result.Status);
        Assert.Equal(0, chargeApi.ChargeCallCount);
    }

    [Fact]
    public async Task Charge_ShouldSkip_WhenCouponBalanceIsBelowTwo()
    {
        var chargeApi = new FakeChargeApi();
        var service = CreateService(chargeApi, "2026-04-30T12:00:00+08:00", "20002");

        var result = await service.Charge(CreateUser(VipType.Annual, 1.9m), CreateCookie("10001"));

        Assert.Equal(TaskStepStatus.Skipped, result.Status);
        Assert.Equal(0, chargeApi.ChargeCallCount);
    }

    [Fact]
    public async Task Charge_ShouldFail_WhenApiRejectsRequest()
    {
        var chargeApi = new FakeChargeApi
        {
            ChargeResponse = new BiliApiResponse<ChargeV2Response>
            {
                Code = 1001,
                Message = "拒绝充电",
                Data = new ChargeV2Response
                {
                    Bp_num = "2",
                    Order_no = "error-order",
                    Status = 4,
                },
            },
        };
        var service = CreateService(chargeApi, "2026-04-30T12:00:00+08:00", "20002");

        var result = await service.Charge(CreateUser(VipType.Annual, 2), CreateCookie("10001"));

        Assert.Equal(TaskStepStatus.Failed, result.Status);
        Assert.Equal(1, chargeApi.ChargeCallCount);
    }

    [Fact]
    public async Task Charge_ShouldFail_WhenSuccessResponseHasUnexpectedStatus()
    {
        var chargeApi = new FakeChargeApi
        {
            ChargeResponse = new BiliApiResponse<ChargeV2Response>
            {
                Code = 0,
                Data = new ChargeV2Response
                {
                    Bp_num = "2",
                    Order_no = "order-1",
                    Status = 3,
                },
            },
        };
        var service = CreateService(chargeApi, "2026-04-30T12:00:00+08:00", "20002");

        var result = await service.Charge(CreateUser(VipType.Annual, 2), CreateCookie("10001"));

        Assert.Equal(TaskStepStatus.Failed, result.Status);
        Assert.Equal(0, chargeApi.CommentCallCount);
    }

    [Fact]
    public async Task Charge_ShouldSucceed_WhenCommentFailsAfterSuccessfulCharge()
    {
        var chargeApi = new FakeChargeApi { ThrowOnComment = true };
        var service = CreateService(chargeApi, "2026-04-30T12:00:00+08:00", "20002");

        var result = await service.Charge(CreateUser(VipType.Annual, 2), CreateCookie("10001"));

        Assert.Equal(TaskStepStatus.Succeeded, result.Status);
        Assert.Equal(1, chargeApi.ChargeCallCount);
        Assert.Equal(1, chargeApi.CommentCallCount);
    }

    private static ChargeDomainService CreateService(
        FakeChargeApi chargeApi,
        string now,
        string? target,
        string? timeZoneId = "Asia/Shanghai"
    )
    {
        var chargeOptions = new TestOptionsMonitor<ChargeTaskOptions>(
            new ChargeTaskOptions
            {
                AutoChargeUpId = target,
                BusinessTimeZoneId = timeZoneId!,
                ChargeComment = "测试留言",
            }
        );
        var current = DateTimeOffset.Parse(now);
        return new ChargeDomainService(
            NullLogger<ChargeDomainService>.Instance,
            chargeOptions,
            chargeApi,
            new ChargeExecutionPolicy(new FixedTimeProvider(current.ToUniversalTime()))
        );
    }

    private static UserInfo CreateUser(VipType vipType, decimal couponBalance)
    {
        return new UserInfo
        {
            Mid = 10001,
            VipStatus = VipStatus.Enable,
            VipType = vipType,
            Wallet = new Wallet { Coupon_balance = couponBalance },
            Wbi_img = new WbiImg
            {
                img_url = "https://example.com/wbi/img.png",
                sub_url = "https://example.com/wbi/sub.png",
            },
        };
    }

    private static BiliCookie CreateCookie(string userId)
    {
        return new BiliCookie(
            new Dictionary<string, string>
            {
                ["DedeUserID"] = userId,
                ["SESSDATA"] = "sess",
                ["bili_jct"] = "csrf",
                ["buvid3"] = "buvid",
            }
        );
    }

    private sealed class FakeChargeApi : IChargeApi
    {
        public BiliApiResponse<ChargeV2Response> ChargeResponse { get; set; } =
            new()
            {
                Code = 0,
                Data = new ChargeV2Response
                {
                    Bp_num = "2",
                    Order_no = "order-1",
                    Status = 4,
                },
            };

        public bool ThrowOnComment { get; set; }
        public int ChargeCallCount { get; private set; }
        public int CommentCallCount { get; private set; }
        public ChargeRequest? LastChargeRequest { get; private set; }

        public Task<BiliApiResponse<ChargeResponse>> Charge(
            int elec_num,
            string up_mid,
            string oid,
            string csrf,
            string ck
        ) => throw new NotImplementedException();

        public Task<BiliApiResponse<ChargeV2Response>> ChargeV2Async(
            ChargeRequest request,
            string ck
        )
        {
            ChargeCallCount++;
            LastChargeRequest = request;
            return Task.FromResult(ChargeResponse);
        }

        public Task<BiliApiResponse<ChargeResponse>> ChargeCommentAsync(
            ChargeCommentRequest request,
            string ck
        )
        {
            CommentCallCount++;
            if (ThrowOnComment)
            {
                throw new InvalidOperationException("留言失败");
            }

            return Task.FromResult(
                new BiliApiResponse<ChargeResponse>
                {
                    Code = 0,
                    Data = new ChargeResponse { Order_no = request.Order_id, Status = 4 },
                }
            );
        }
    }

    private sealed class FakeDailyTaskApi : IDailyTaskApi
    {
        public Task<BiliApiResponse<DailyTaskInfo>> GetDailyTaskRewardInfoAsync(string ck) =>
            throw new NotImplementedException();

        public Task<BiliApiResponse<int?>> GetDonateCoinExpAsync(string ck) =>
            throw new NotImplementedException();

        public Task<BiliApiResponse> ReceiveVipPrivilegeAsync(int type, string csrf, string ck) =>
            throw new NotImplementedException();
    }

    private sealed class TestOptionsMonitor<T>(T value) : IOptionsMonitor<T>
    {
        public T CurrentValue => value;

        public T Get(string? name) => value;

        public IDisposable? OnChange(Action<T, string?> listener) => null;
    }
}
