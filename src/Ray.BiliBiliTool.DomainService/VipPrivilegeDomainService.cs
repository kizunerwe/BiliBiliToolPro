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
/// 会员权益
/// </summary>
public class VipPrivilegeDomainService(
    ILogger<VipPrivilegeDomainService> logger,
    IDailyTaskApi dailyTaskApi,
    IOptionsMonitor<VipPrivilegeOptions> receiveVipPrivilegeOptions
) : IVipPrivilegeDomainService
{
    private readonly VipPrivilegeOptions _vipPrivilegeOptions =
        receiveVipPrivilegeOptions.CurrentValue;

    /// <summary>
    /// 每月领取大会员福利（B币券、大会员权益）
    /// </summary>
    /// <param name="userInfo"></param>
    /// <param name="ck"></param>
    public async Task<TaskStepResult> ReceiveVipPrivilege(UserInfo userInfo, BiliCookie ck)
    {
        if (!_vipPrivilegeOptions.IsEnable)
        {
            logger.LogInformation("已配置为关闭，跳过");
            return TaskStepResult.Skip("大会员福利已关闭");
        }

        //大会员类型
        VipType vipType = userInfo.GetVipType();
        if (vipType != VipType.Annual)
        {
            logger.LogInformation("普通会员和月度大会员每月不赠送B币券，不需要领取权益喽");
            return TaskStepResult.Skip("当前账号不是年度大会员");
        }

        /*
        int targetDay = _dailyTaskOptions.DayOfReceiveVipPrivilege == -1
            ? 1
            : _dailyTaskOptions.DayOfReceiveVipPrivilege;

        _logger.LogInformation("【目标领取日期】{targetDay}号", targetDay);
        _logger.LogInformation("【今天】{day}号", DateTime.Today.Day);

        if (DateTime.Today.Day != targetDay
            && DateTime.Today.Day != DateTime.Today.LastDayOfMonth().Day)
        {
            _logger.LogInformation("跳过");
            return false;
        }
        */

        try
        {
            var coupon = await ReceiveVipPrivilege(VipPrivilegeType.BCoinCoupon, ck);
            var benefits = await ReceiveVipPrivilege(VipPrivilegeType.MembershipBenefits, ck);

            if (coupon.Status == TaskStepStatus.Failed || benefits.Status == TaskStepStatus.Failed)
            {
                return TaskStepResult.Fail(
                    coupon.Status == TaskStepStatus.Failed ? coupon.Reason! : benefits.Reason!
                );
            }

            if (
                coupon.Status == TaskStepStatus.Skipped
                && benefits.Status == TaskStepStatus.Skipped
            )
                return TaskStepResult.Skip("本月大会员福利已领取");

            return TaskStepResult.Success();
        }
        catch (Exception exception)
        {
            return TaskStepResult.Fail($"领取大会员福利异常：{exception.Message}");
        }
    }

    #region private

    /// <summary>
    /// 领取大会员每月赠送福利
    /// </summary>
    /// <param name="type">1.大会员B币券；2.大会员福利</param>
    /// <param name="ck"></param>
    private async Task<TaskStepResult> ReceiveVipPrivilege(VipPrivilegeType type, BiliCookie ck)
    {
        var response = await dailyTaskApi.ReceiveVipPrivilegeAsync(
            (int)type,
            ck.BiliJct,
            ck.ToString()
        );

        var name = GetPrivilegeName(type);
        logger.LogInformation("【领取】{name}", name);

        if (response.Code == 0)
        {
            logger.LogInformation("【结果】成功");
            return TaskStepResult.Success();
        }
        if (response.Code == 69801)
        {
            logger.LogInformation("【结果】本月已领取");
            return TaskStepResult.Skip("本月已领取");
        }

        logger.LogInformation("【结果】失败");
        logger.LogInformation("【原因】 {msg}", response.Message);
        return TaskStepResult.Fail($"{GetPrivilegeName(type)}领取被拒绝：{response.Message}");
    }

    /// <summary>
    /// 获取权益名称
    /// </summary>
    /// <param name="type"></param>
    /// <returns></returns>
    private string GetPrivilegeName(VipPrivilegeType type)
    {
        switch (type)
        {
            case VipPrivilegeType.BCoinCoupon:
                return "年度大会员每月赠送的B币券";

            case VipPrivilegeType.MembershipBenefits:
                return "大会员福利/权益";
        }

        return "";
    }

    #endregion private
}
