using System.Reflection;
using Microsoft.Extensions.Options;
using Ray.BiliBiliTool.Agent;
using Ray.BiliBiliTool.Agent.BiliBiliAgent.Dtos;

namespace DomainServiceTest;

internal static class TestDoubles
{
    public static T Api<T>(Func<MethodInfo, object?[], object?> handler)
        where T : class
    {
        var proxy = DispatchProxy.Create<T, ApiDispatchProxy<T>>();
        ((ApiDispatchProxy<T>)(object)proxy).Handler = handler;
        return proxy;
    }

    public static BiliCookie Cookie() =>
        new(
            new Dictionary<string, string>
            {
                ["DedeUserID"] = "123",
                ["SESSDATA"] = "sess",
                ["bili_jct"] = "csrf",
                ["buvid3"] = "buvid",
                ["LIVE_BUVID"] = "live-buvid",
            }
        );

    public static UserInfo User(VipType vipType = VipType.None) =>
        new()
        {
            Mid = 123,
            IsLogin = true,
            VipStatus = vipType == VipType.None ? VipStatus.Disable : VipStatus.Enable,
            VipType = vipType,
            Wbi_img = new WbiImg
            {
                img_url = "https://example.com/wbi/img.png",
                sub_url = "https://example.com/wbi/sub.png",
            },
        };

    public sealed class OptionsMonitor<T>(T value) : IOptionsMonitor<T>
    {
        public T CurrentValue => value;

        public T Get(string? name) => value;

        public IDisposable? OnChange(Action<T, string?> listener) => null;
    }

    public class ApiDispatchProxy<T> : DispatchProxy
        where T : class
    {
        public Func<MethodInfo, object?[], object?> Handler { get; set; } = null!;

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) =>
            Handler(targetMethod ?? throw new InvalidOperationException(), args ?? []);
    }
}
