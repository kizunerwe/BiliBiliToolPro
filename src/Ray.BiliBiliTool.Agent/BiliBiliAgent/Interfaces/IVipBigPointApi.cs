using Ray.BiliBiliTool.Agent.Attributes;
using Ray.BiliBiliTool.Agent.BiliBiliAgent.Dtos;
using Ray.BiliBiliTool.Agent.BiliBiliAgent.Dtos.Mall;
using Ray.BiliBiliTool.Agent.BiliBiliAgent.Dtos.VipTask;
using Ray.BiliBiliTool.Agent.BiliBiliAgent.Dtos.VipTask.ThreeDaysSign;
using WebApiClientCore.Attributes;

namespace Ray.BiliBiliTool.Agent.BiliBiliAgent.Interfaces;

/// <summary>
/// 大会员大积分
/// </summary>
[Header("Host", "api.bilibili.com")]
[Header("Referer", "https://big.bilibili.com/mobile/bigPoint")]
[LogFilter]
public interface IVipBigPointApi
{
    [HttpGet("/x/vip/vip_center/sign_in/three_days_sign")]
    Task<BiliApiResponse<ThreeDaySignResponse>> GetThreeDaySignAsync(
        [PathQuery] ThreeDaySignRequest request,
        [Header("Cookie")] string ck
    );

    [Obsolete("Using IMallApi.GetCombineAsync instead.")]
    [HttpGet("/x/vip_point/task/combine")]
    Task<BiliApiResponse<VipBigPointCombine>> GetCombineAsync([Header("Cookie")] string ck);

    [Obsolete("Using IMallApi.Sign2Async instead.")]
    [Header("Referer", "https://big.bilibili.com/mobile/bigPoint/task")]
    [HttpPost("/pgc/activity/score/task/sign")]
    Task<BiliApiResponse> SignAsync(
        [FormContent] SignRequest request,
        [Header("Cookie")] string ck
    );

    [Obsolete]
    [Header("Referer", "https://big.bilibili.com/mobile/bigPoint/task")]
    [HttpPost("/pgc/activity/score/task/receive")]
    Task<BiliApiResponse> Receive(
        [JsonContent] ReceiveOrCompleteTaskRequest request,
        [Header("Cookie")] string ck
    );

    [Header("app-key", "android64")]
    [Header("env", "prod")]
    [Header("navtive_api_from", "h5")]
    [Header("Referer", "https://big.bilibili.com/mobile/bigPoint/task")]
    [HttpPost("/pgc/activity/score/task/receive/v2")]
    Task<BiliApiResponse> ReceiveV2(
        [FormContent] VipBigPointTaskAppRequest request,
        [Header("Cookie")] string ck
    );

    [Header("Referer", "https://big.bilibili.com/mobile/bigPoint/task")]
    [HttpPost("/pgc/activity/score/task/complete")]
    Task<BiliApiResponse> CompleteAsync(
        [JsonContent] ReceiveOrCompleteTaskRequest request,
        [Header("Cookie")] string ck
    );

    [Header("app-key", "android64")]
    [Header("env", "prod")]
    [Header("Referer", "https://big.bilibili.com/mobile/bigPoint/task")]
    [HttpPost("/pgc/activity/score/task/complete/v2")]
    Task<BiliApiResponse> CompleteV2(
        [FormContent] VipBigPointTaskAppRequest request,
        [Header("Cookie")] string ck
    );

    [HttpPost("/pgc/activity/deliver/task/complete")]
    Task<BiliApiResponse> ViewComplete(
        [FormContent] ViewRequest request,
        [Header("Cookie")] string ck
    );

    [HttpGet("/x/vip/privilege/my")]
    Task<BiliApiResponse<VouchersInfoResponse>> GetVouchersInfoAsync([Header("Cookie")] string ck);

    [HttpPost("/x/vip/experience/add")]
    Task<BiliApiResponse> ObtainVipExperienceAsync(
        [FormContent] VipExperienceRequest request,
        [Header("Cookie")] string ck
    );

    [Header("app-key", "android64")]
    [Header("env", "prod")]
    [HttpPost("/pgc/activity/deliver/material/receive")]
    Task<BiliApiResponse<StartOgvWatchResponse>> StartOgvWatchAsync(
        [FormContent] StartOgvWatchRequest request,
        [Header("Cookie")] string ck,
        [Header("buvid")] string buvid,
        [Header("x-bili-mid")] string mid
    );

    [Header("app-key", "android64")]
    [Header("env", "prod")]
    [HttpPost("/pgc/activity/deliver/task/complete")]
    Task<BiliApiResponse> CompleteOgvWatchAsync(
        [FormContent] CompleteOgvWatchRequest request,
        [Header("Cookie")] string ck,
        [Header("buvid")] string buvid,
        [Header("x-bili-mid")] string mid
    );
}
