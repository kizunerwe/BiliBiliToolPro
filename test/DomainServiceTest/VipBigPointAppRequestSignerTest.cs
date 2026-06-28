using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Ray.BiliBiliTool.Agent.BiliBiliAgent.Dtos.VipTask;

namespace DomainServiceTest;

public class VipBigPointAppRequestSignerTest
{
    [Fact]
    public void PopulateOgvTaskSigns_ShouldUseReferenceTaskSignAlgorithm()
    {
        MethodInfo? method = GetSignerMethod(
            "PopulateOgvTaskSigns",
            typeof(CompleteOgvWatchRequest)
        );
        Assert.NotNull(method);

        ConstructorInfo? ctor = typeof(CompleteOgvWatchRequest).GetConstructor([
            typeof(long),
            typeof(string),
            typeof(string),
        ]);
        Assert.NotNull(ctor);

        var request = (CompleteOgvWatchRequest)ctor!.Invoke([4320003L, "67ba5888e7", "csrf-token"]);

        typeof(CompleteOgvWatchRequest).GetProperty("timestamp")!.SetValue(request, 1748884714621L);

        method!.Invoke(null, [request]);

        Assert.Equal("c609da93036da61485925143a582e09b", request.task_sign);
        Assert.Null(request.sign);
    }

    [Fact]
    public void StartAndCompleteOgvRequests_ShouldMatchUnsignedReferencePayloadShape()
    {
        ConstructorInfo? startCtor = typeof(StartOgvWatchRequest).GetConstructor([
            typeof(long),
            typeof(long),
            typeof(string),
        ]);
        ConstructorInfo? completeCtor = typeof(CompleteOgvWatchRequest).GetConstructor([
            typeof(long),
            typeof(string),
            typeof(string),
        ]);

        Assert.NotNull(startCtor);
        Assert.NotNull(completeCtor);

        var startRequest = (StartOgvWatchRequest)startCtor!.Invoke([328482L, 12548L, "csrf-token"]);
        var completeRequest = (CompleteOgvWatchRequest)
            completeCtor!.Invoke([4320003L, "67ba5888e7", "csrf-token"]);

        Assert.Equal("csrf-token", GetPropertyValue(startRequest, "csrf"));
        Assert.Equal("android", GetPropertyValue(startRequest, "device"));
        Assert.Equal("7720200", GetPropertyValue(startRequest, "build"));
        Assert.Equal("search.search-result.0.0", GetPropertyValue(startRequest, "from_spmid"));
        Assert.Null(GetPropertyValue(startRequest, "appkey"));
        Assert.Null(GetPropertyValue(startRequest, "access_key"));
        Assert.Null(GetPropertyValue(startRequest, "actionKey"));
        Assert.Null(GetPropertyValue(startRequest, "sign"));

        Assert.Equal("csrf-token", GetPropertyValue(completeRequest, "csrf"));
        Assert.Equal("android", GetPropertyValue(completeRequest, "device"));
        Assert.Equal("7720200", GetPropertyValue(completeRequest, "build"));
        Assert.Null(GetPropertyValue(completeRequest, "appkey"));
        Assert.Null(GetPropertyValue(completeRequest, "access_key"));
        Assert.Null(GetPropertyValue(completeRequest, "actionKey"));
        Assert.Null(GetPropertyValue(completeRequest, "sign"));
    }

    [Fact]
    public void PopulateAppSign_ShouldUpgradeStartRequest_WhenAccessKeyExists()
    {
        MethodInfo? method = GetSignerMethod(
            "PopulateAppSign",
            typeof(StartOgvWatchRequest),
            typeof(string)
        );
        Assert.NotNull(method);

        var request = new StartOgvWatchRequest(328482, 12548, "csrf-token") { ts = 1735744760 };

        method!.Invoke(null, [request, "access-key-demo"]);

        Assert.Null(request.csrf);
        Assert.Equal("access-key-demo", GetPropertyValue(request, "access_key"));
        Assert.Equal("appkey", GetPropertyValue(request, "actionKey"));
        Assert.Equal("1d8b6e7d45233436", GetPropertyValue(request, "appkey"));
        Assert.Equal(
            BuildExpectedAppSign(
                new Dictionary<string, string>
                {
                    ["access_key"] = "access-key-demo",
                    ["actionKey"] = "appkey",
                    ["activity_code"] = "",
                    ["appkey"] = "1d8b6e7d45233436",
                    ["build"] = "7720200",
                    ["c_locale"] = "zh_CN",
                    ["channel"] = "bili",
                    ["device"] = "android",
                    ["disable_rcmd"] = "0",
                    ["ep_id"] = "328482",
                    ["from_spmid"] = "search.search-result.0.0",
                    ["mobi_app"] = "android",
                    ["platform"] = "android",
                    ["s_locale"] = "zh_CN",
                    ["season_id"] = "12548",
                    ["spmid"] = "united.player-video-detail.0.0",
                    ["ts"] = "1735744760",
                }
            ),
            request.sign
        );
    }

    [Fact]
    public void PopulateAppSign_ShouldUpgradeCompleteRequest_WhenAccessKeyExists()
    {
        MethodInfo? taskSignMethod = GetSignerMethod(
            "PopulateOgvTaskSigns",
            typeof(CompleteOgvWatchRequest)
        );
        MethodInfo? signMethod = GetSignerMethod(
            "PopulateAppSign",
            typeof(CompleteOgvWatchRequest),
            typeof(string)
        );
        Assert.NotNull(taskSignMethod);
        Assert.NotNull(signMethod);

        var request = new CompleteOgvWatchRequest(4320003, "67ba5888e7", "csrf-token")
        {
            ts = 1735744760,
            timestamp = 1748884714621,
        };

        taskSignMethod!.Invoke(null, [request]);
        signMethod!.Invoke(null, [request, "access-key-demo"]);

        Assert.Null(request.csrf);
        Assert.Equal("access-key-demo", GetPropertyValue(request, "access_key"));
        Assert.Equal("appkey", GetPropertyValue(request, "actionKey"));
        Assert.Equal("1d8b6e7d45233436", GetPropertyValue(request, "appkey"));
        Assert.Equal(
            BuildExpectedAppSign(
                new Dictionary<string, string>
                {
                    ["access_key"] = "access-key-demo",
                    ["actionKey"] = "appkey",
                    ["appkey"] = "1d8b6e7d45233436",
                    ["build"] = "7720200",
                    ["c_locale"] = "zh_CN",
                    ["channel"] = "bili",
                    ["device"] = "android",
                    ["disable_rcmd"] = "0",
                    ["mobi_app"] = "android",
                    ["platform"] = "android",
                    ["s_locale"] = "zh_CN",
                    ["task_id"] = "4320003",
                    ["task_sign"] = "c609da93036da61485925143a582e09b",
                    ["timestamp"] = "1748884714621",
                    ["token"] = "67ba5888e7",
                    ["ts"] = "1735744760",
                }
            ),
            request.sign
        );
    }

    private static MethodInfo? GetSignerMethod(string name, params Type[] parameters)
    {
        var signerType = Type.GetType(
            "Ray.BiliBiliTool.Agent.BiliBiliAgent.Utils.VipBigPointAppRequestSigner, Ray.BiliBiliTool.Agent"
        );
        Assert.NotNull(signerType);

        return signerType!.GetMethod(name, BindingFlags.Public | BindingFlags.Static, parameters);
    }

    private static object? GetPropertyValue(object instance, string propertyName)
    {
        return instance.GetType().GetProperty(propertyName)?.GetValue(instance);
    }

    private static string BuildExpectedAppSign(Dictionary<string, string> parameters)
    {
        var query = string.Join(
            "&",
            parameters
                .OrderBy(x => x.Key, StringComparer.Ordinal)
                .Select(x => $"{Uri.EscapeDataString(x.Key)}={Uri.EscapeDataString(x.Value)}")
        );

        using var md5 = MD5.Create();
        var hash = md5.ComputeHash(
            Encoding.UTF8.GetBytes(query + "560c52ccd288fed045859ed18bffd973")
        );
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
