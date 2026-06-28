namespace Ray.BiliBiliTool.Agent.BiliBiliAgent.Dtos.Passport;

public class PassportTvPollData
{
    public bool is_new { get; set; }
    public long mid { get; set; }
    public string? access_token { get; set; }
    public string? refresh_token { get; set; }
    public int expires_in { get; set; }
    public PassportTvCookieInfo? cookie_info { get; set; }
    public List<string> sso { get; set; } = [];
    public PassportTvTokenInfo? token_info { get; set; }
}

public class PassportTvTokenInfo
{
    public long mid { get; set; }
    public string? access_token { get; set; }
    public string? refresh_token { get; set; }
    public int expires_in { get; set; }
}

public class PassportTvCookieInfo
{
    public List<PassportTvCookieItem> cookies { get; set; } = [];
    public List<string> domains { get; set; } = [];
}

public class PassportTvCookieItem
{
    public required string name { get; set; }
    public string? value { get; set; }
    public int http_only { get; set; }
    public long expires { get; set; }
}
