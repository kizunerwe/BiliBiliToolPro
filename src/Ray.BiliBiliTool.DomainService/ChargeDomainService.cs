using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Ray.BiliBiliTool.Agent;
using Ray.BiliBiliTool.Agent.BiliBiliAgent.Dtos;
using Ray.BiliBiliTool.Agent.BiliBiliAgent.Interfaces;
using Ray.BiliBiliTool.Config.Options;
using Ray.BiliBiliTool.DomainService.Dtos;
using Ray.BiliBiliTool.DomainService.Interfaces;

namespace Ray.BiliBiliTool.DomainService;

/// <summary>
/// 充电
/// </summary>
public class ChargeDomainService(
    ILogger<ChargeDomainService> logger,
    IOptionsMonitor<ChargeTaskOptions> chargeTaskOptions,
    IChargeApi chargeApi,
    ChargeExecutionPolicy chargeExecutionPolicy
) : IChargeDomainService
{
    private readonly ChargeTaskOptions _chargeTaskOptions = chargeTaskOptions.CurrentValue;
    private readonly ChargeExecutionPolicy _chargeExecutionPolicy = chargeExecutionPolicy;

    public async Task<TaskStepResult> Charge(UserInfo userInfo, BiliCookie ck)
    {
        if (!TryGetTargetUpId(userInfo, ck, out var targetUpId, out var targetError))
        {
            logger.LogError("充电目标配置无效：{reason}", targetError);
            return TaskStepResult.Fail(targetError);
        }

        try
        {
            if (!_chargeExecutionPolicy.IsMonthEnd(_chargeTaskOptions.BusinessTimeZoneId))
            {
                logger.LogInformation("今天不是业务时区的月末，跳过充电");
                return TaskStepResult.Skip("今天不是业务时区月末");
            }
        }
        catch (ArgumentNullException exception)
        {
            return TaskStepResult.Fail($"业务时区无效：{exception.Message}");
        }
        catch (TimeZoneNotFoundException exception)
        {
            return TaskStepResult.Fail($"业务时区无效：{exception.Message}");
        }
        catch (InvalidTimeZoneException exception)
        {
            return TaskStepResult.Fail($"业务时区无效：{exception.Message}");
        }

        if (userInfo.GetVipType() != VipType.Annual)
        {
            logger.LogInformation("不是年度大会员，跳过");
            return TaskStepResult.Skip("不是年度大会员");
        }

        decimal couponBalance = userInfo.Wallet?.Coupon_balance ?? 0;
        logger.LogInformation("【B币券】{couponBalance}", couponBalance);
        if (couponBalance < 2)
        {
            logger.LogInformation("余额小于2，无法充电");
            return TaskStepResult.Skip("B币券余额小于2");
        }

        logger.LogDebug("【目标Up】{up}", targetUpId);
        var request = new ChargeRequest(couponBalance, targetUpId, ck.BiliJct);
        BiliApiResponse<ChargeV2Response> response;
        try
        {
            response = await chargeApi.ChargeV2Async(request, ck.ToString());
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "充电请求异常");
            return TaskStepResult.Fail($"充电请求异常：{exception.Message}");
        }

        if (response.Code != 0)
        {
            logger.LogError("【充电结果】失败，原因：{reason}", response.Message);
            return TaskStepResult.Fail(
                $"充电业务拒绝：{response.Message ?? response.Code.ToString()}"
            );
        }

        if (response.Data == null)
        {
            return TaskStepResult.Fail("充电成功响应缺少 data");
        }

        if (response.Data.Status != 4)
        {
            logger.LogError("【充电结果】失败，状态：{status}", response.Data.Status);
            return TaskStepResult.Fail($"充电成功响应状态异常：{response.Data.Status}");
        }

        logger.LogInformation("【充电结果】成功");
        logger.LogInformation("【充值个数】 {num}个B币", couponBalance);
        logger.LogInformation("经验+{exp} √", couponBalance);
        logger.LogInformation("在过期前使用成功，赠送的B币券没有浪费哦~");

        try
        {
            await ChargeComments(response.Data.Order_no, ck);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "充电成功，但留言失败");
        }

        return TaskStepResult.Success("充电成功");
    }

    public async Task ChargeComments(string orderNum, BiliCookie ck)
    {
        var comment = _chargeTaskOptions.ChargeComment;
        var request = new ChargeCommentRequest(orderNum, comment, ck.BiliJct);
        var response = await chargeApi.ChargeCommentAsync(request, ck.ToString());
        if (response.Code != 0)
        {
            throw new InvalidOperationException(
                $"留言业务拒绝：{response.Message ?? response.Code.ToString()}"
            );
        }

        logger.LogInformation("【留言】{comment}", comment);
    }

    private bool TryGetTargetUpId(
        UserInfo userInfo,
        BiliCookie ck,
        out long targetUpId,
        out string reason
    )
    {
        targetUpId = 0;
        reason = "未配置明确的充电目标 UID";

        var configuredTarget = _chargeTaskOptions.AutoChargeUpId?.Trim();
        if (string.IsNullOrWhiteSpace(configuredTarget))
        {
            return false;
        }

        if (!long.TryParse(configuredTarget, out targetUpId) || targetUpId <= 0)
        {
            reason = "充电目标 UID 必须是正整数";
            targetUpId = 0;
            return false;
        }

        if (
            targetUpId == userInfo.Mid
            || (long.TryParse(ck.UserId, out var cookieUserId) && targetUpId == cookieUserId)
        )
        {
            reason = "充电目标 UID 不能是当前账号";
            targetUpId = 0;
            return false;
        }

        return true;
    }
}
