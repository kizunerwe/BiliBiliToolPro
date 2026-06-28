using System.Reflection;
using Ray.BiliBiliTool.Agent.BiliBiliAgent.Dtos.VipTask;
using Ray.BiliBiliTool.Agent.BiliBiliAgent.Interfaces;
using WebApiClientCore.Attributes;

namespace DomainServiceTest;

public class VipBigPointOgvContractTest
{
    [Fact]
    public void StartOgvWatchAsync_ShouldDeclareMaterialReceiveRoute()
    {
        MethodInfo? method = typeof(IVipBigPointApi).GetMethod(
            nameof(IVipBigPointApi.StartOgvWatchAsync)
        );
        Assert.NotNull(method);

        var attribute = method!.GetCustomAttribute<HttpPostAttribute>();

        Assert.NotNull(attribute);
        Assert.Equal("/pgc/activity/deliver/material/receive", attribute!.Path.ToString());
    }

    [Fact]
    public void CompleteOgvWatchAsync_ShouldDeclareDeliverCompleteRoute()
    {
        MethodInfo? method = typeof(IVipBigPointApi).GetMethod(
            nameof(IVipBigPointApi.CompleteOgvWatchAsync)
        );
        Assert.NotNull(method);

        var attribute = method!.GetCustomAttribute<HttpPostAttribute>();

        Assert.NotNull(attribute);
        Assert.Equal("/pgc/activity/deliver/task/complete", attribute!.Path.ToString());
    }

    [Fact]
    public void StartOgvWatchRequest_ShouldAcceptDynamicEpisodeAndSeasonIds()
    {
        ConstructorInfo? ctor = typeof(StartOgvWatchRequest).GetConstructor([
            typeof(long),
            typeof(long),
            typeof(string),
        ]);

        Assert.NotNull(ctor);
    }

    [Theory]
    [InlineData(nameof(IVipBigPointApi.StartOgvWatchAsync))]
    [InlineData(nameof(IVipBigPointApi.CompleteOgvWatchAsync))]
    public void OgvMethods_ShouldDeclareReferenceAppHeaders(string methodName)
    {
        MethodInfo? method = typeof(IVipBigPointApi).GetMethod(methodName);
        Assert.NotNull(method);

        var headers = typeof(IVipBigPointApi)
            .GetCustomAttributes<HeaderAttribute>()
            .Concat(method!.GetCustomAttributes<HeaderAttribute>())
            .ToDictionary(
                x => GetHeaderField(x, "name")!.ToLowerInvariant(),
                x => GetHeaderField(x, "value")
            );

        Assert.Equal("android64", headers["app-key"]);
        Assert.Equal("prod", headers["env"]);
        Assert.Equal("https://big.bilibili.com/mobile/bigPoint", headers["referer"]);
    }

    private static string? GetHeaderField(HeaderAttribute attribute, string fieldName)
    {
        return attribute
            .GetType()
            .GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public
            )
            ?.GetValue(attribute)
            ?.ToString();
    }
}
