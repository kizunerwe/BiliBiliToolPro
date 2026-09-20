using Ray.BiliBiliTool.Agent.BiliBiliAgent.Dtos.Live;

namespace Ray.BiliBiliTool.DomainService.Dtos;

public class HeartBeatIterationInfoDto(
    long roomId,
    GetLiveRoomInfoResponse roomInfo,
    HeartBeatResponse heartBeatInfo,
    int heartBeatCount,
    long lastBeatTime
)
{
    public long RoomId { get; set; } = roomId;

    public GetLiveRoomInfoResponse RoomInfo { get; set; } = roomInfo;

    public HeartBeatResponse HeartBeatInfo { get; set; } = heartBeatInfo;

    // 成功发送的心跳包个数
    public int HeartBeatCount { get; set; } = heartBeatCount;

    public long LastBeatTime { get; set; } = lastBeatTime;

    // 连续失败的次数
    public int FailedTimes { get; set; }

    // 当前服务端心跳链使用的序号，重新进入直播间后从 0 开始。
    public int ChainSequence { get; set; }

    public int HeartBeatIntervalSeconds { get; set; } = 60;

    public bool NeedsReenter { get; set; }

    public int RebuildCount { get; set; }
}
