using System.Text.Json.Serialization;

namespace Ray.BiliBiliTool.Agent.BiliBiliAgent.Dtos.VipTask;

public class StartOgvWatchResponse
{
    public string? closeType { get; set; }

    public string? showTime { get; set; }

    public WatchCountDownConfig? watch_count_down_cfg { get; set; }

    [JsonIgnore]
    public long task_id => long.TryParse(watch_count_down_cfg?.task_id, out var value) ? value : 0;

    [JsonIgnore]
    public string? token => watch_count_down_cfg?.token;

    [JsonIgnore]
    public bool HasValidTaskContext => task_id > 0 && !string.IsNullOrWhiteSpace(token);

    [JsonIgnore]
    public int CountdownMilliseconds => Math.Max(watch_count_down_cfg?.milliseconds ?? 0, 0);
}

public class WatchCountDownConfig
{
    public int milliseconds { get; set; }

    public string? task_id { get; set; }

    public string? token { get; set; }
}
