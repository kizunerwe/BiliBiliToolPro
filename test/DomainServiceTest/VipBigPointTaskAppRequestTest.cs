using System.Reflection;
using Ray.BiliBiliTool.Agent.BiliBiliAgent.Interfaces;

namespace DomainServiceTest;

public class VipBigPointTaskAppRequestTest
{
    [Fact]
    public void ReceiveV2_ShouldUseAppTaskRequestDto()
    {
        MethodInfo? method = typeof(IVipBigPointApi).GetMethod(nameof(IVipBigPointApi.ReceiveV2));
        Assert.NotNull(method);

        var requestParameter = method!.GetParameters().FirstOrDefault();
        Assert.NotNull(requestParameter);
        Assert.Equal("VipBigPointTaskAppRequest", requestParameter!.ParameterType.Name);
    }

    [Fact]
    public void CompleteV2_ShouldUseAppTaskRequestDto()
    {
        MethodInfo? method = typeof(IVipBigPointApi).GetMethod(nameof(IVipBigPointApi.CompleteV2));
        Assert.NotNull(method);

        var requestParameter = method!.GetParameters().FirstOrDefault();
        Assert.NotNull(requestParameter);
        Assert.Equal("VipBigPointTaskAppRequest", requestParameter!.ParameterType.Name);
    }

    [Fact]
    public void AppTaskRequest_ShouldExposeReferenceFields()
    {
        var requestType = Type.GetType(
            "Ray.BiliBiliTool.Agent.BiliBiliAgent.Dtos.VipTask.VipBigPointTaskAppRequest, Ray.BiliBiliTool.Agent"
        );
        Assert.NotNull(requestType);

        ConstructorInfo? ctor = requestType!.GetConstructor([typeof(string), typeof(string)]);
        Assert.NotNull(ctor);

        var request = ctor!.Invoke(["ogvwatchnew", "csrf-token"]);

        Assert.Equal("ogvwatchnew", GetPropertyValue(request, "taskCode"));
        Assert.Equal("csrf-token", GetPropertyValue(request, "csrf"));
        Assert.Equal("android", GetPropertyValue(request, "device"));
        Assert.Equal("7720200", GetPropertyValue(request, "build"));
        Assert.Null(GetPropertyValue(request, "access_key"));
        Assert.Null(GetPropertyValue(request, "actionKey"));
        Assert.Null(GetPropertyValue(request, "appkey"));
        Assert.Null(GetPropertyValue(request, "sign"));
    }

    private static object? GetPropertyValue(object instance, string propertyName)
    {
        return instance.GetType().GetProperty(propertyName)?.GetValue(instance);
    }
}
