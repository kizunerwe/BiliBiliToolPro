using System.Net;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Ray.BiliBiliTool.Agent;
using Ray.BiliBiliTool.Agent.BiliBiliAgent;
using Ray.BiliBiliTool.Agent.BiliBiliAgent.Dtos;
using Ray.BiliBiliTool.Agent.BiliBiliAgent.Interfaces;
using Ray.BiliBiliTool.Agent.QingLong;
using Ray.BiliBiliTool.Config.Options;
using Ray.BiliBiliTool.DomainService;
using Ray.BiliBiliTool.Infrastructure.Cookie;

namespace DomainServiceTest;

public sealed class LoginDomainServiceCookieRefreshTest
{
    [Fact]
    public async Task SetCookieAsync_ShouldGenerateAndReportDeviceFingerprint_WhenBuvid3Missing()
    {
        var cookie = TestDoubles.Cookie();
        cookie.CookieItemDictionary.Remove("buvid3");
        var userInfoApi = TestDoubles.Api<IUserInfoApi>(
            (method, _) =>
                method.Name == "GetDeviceFingerprint"
                    ? Task.FromResult<BiliApiResponse<DeviceFingerprintResponse>>(
                        new BiliApiResponse<DeviceFingerprintResponse>
                        {
                            Code = 0,
                            Data = new DeviceFingerprintResponse
                            {
                                B_3 = "fresh-buvid3",
                                B_4 = "fresh-buvid4",
                            },
                        }
                    )
                    : throw new InvalidOperationException(method.Name)
        );
        string? reportedPayload = null;
        string? reportedCookieHeader = null;
        var gaiaApi = TestDoubles.Api<IGaiaApi>(
            (method, args) =>
                method.Name == "ReportDeviceFingerprint"
                    ? Task.FromResult(
                        CaptureGaiaReport(args, out reportedPayload, out reportedCookieHeader)
                    )
                    : throw new InvalidOperationException(method.Name)
        );
        var service = CreateService(userInfoApi, CreateSuccessfulHomeApi(), gaiaApi: gaiaApi);

        await service.SetCookieAsync(cookie, CancellationToken.None);

        Assert.Equal("fresh-buvid3", cookie.CookieItemDictionary["buvid3"]);
        Assert.Equal("fresh-buvid4", cookie.CookieItemDictionary["buvid4"]);
        var deviceUuid = cookie.CookieItemDictionary.GetValueOrDefault("_uuid");
        Assert.False(string.IsNullOrWhiteSpace(deviceUuid));

        // 上报内容校验：动态字段、指纹扰动、Cookie 头
        Assert.NotNull(reportedPayload);
        Assert.NotNull(reportedCookieHeader);
        var payload = Newtonsoft.Json.Linq.JObject.Parse(reportedPayload);
        Assert.Equal(deviceUuid, (string?)payload["df35"]);
        Assert.Equal("https%3A%2F%2Fwww.bilibili.com%2F", (string?)payload["03bf"]);
        var fingerprint = (Newtonsoft.Json.Linq.JObject?)payload["3c43"];
        Assert.NotNull(fingerprint);
        var screen = (Newtonsoft.Json.Linq.JArray?)fingerprint["748e"];
        Assert.NotNull(screen);
        Assert.Equal(2, screen.Count);
        // canvas 指纹应与模板不同（已扰动）
        var template = Newtonsoft.Json.Linq.JObject.Parse(
            GaiaDeviceFingerprintTemplate.PayloadJson
        );
        var templateFingerprint = (Newtonsoft.Json.Linq.JObject?)template["3c43"];
        Assert.NotNull(templateFingerprint);
        Assert.NotEqual(
            (string?)templateFingerprint["13ab"],
            (string?)fingerprint["13ab"]
        );
        // 上报 Cookie 头含设备字段与 _uuid
        Assert.Contains($"buvid3=fresh-buvid3", reportedCookieHeader);
        Assert.Contains($"_uuid={deviceUuid}", reportedCookieHeader);
    }

    private static BiliApiResponse<object> CaptureGaiaReport(
        object?[] args,
        out string payload,
        out string cookieHeader
    )
    {
        cookieHeader = (string)args[0]!;
        payload = ((GaiaReportRequest)args[1]!).Payload;
        return new BiliApiResponse<object> { Code = 0 };
    }

    [Fact]
    public async Task SetCookieAsync_ShouldKeepExistingDeviceFingerprints_WhenRefreshFails()
    {
        var cookie = TestDoubles.Cookie();
        cookie.CookieItemDictionary.Remove("buvid3");
        cookie.CookieItemDictionary["buvid4"] = "existing-buvid4";
        var userInfoApi = TestDoubles.Api<IUserInfoApi>(
            (method, _) =>
                method.Name == "GetDeviceFingerprint"
                    ? Task.FromResult<BiliApiResponse<DeviceFingerprintResponse>>(
                        new BiliApiResponse<DeviceFingerprintResponse>
                        {
                            Code = -403,
                            Message = "denied",
                        }
                    )
                    : throw new InvalidOperationException(method.Name)
        );
        // gaia 未被调用（否则抛异常）：生成失败时应跳过上报
        var service = CreateService(userInfoApi, CreateSuccessfulHomeApi());

        await service.SetCookieAsync(cookie, CancellationToken.None);

        Assert.True(string.IsNullOrWhiteSpace(cookie.CookieItemDictionary.GetValueOrDefault("buvid3")));
        Assert.Equal("existing-buvid4", cookie.CookieItemDictionary["buvid4"]);
        Assert.True(string.IsNullOrWhiteSpace(cookie.CookieItemDictionary.GetValueOrDefault("_uuid")));
    }

    [Fact]
    public async Task SetCookieAsync_ShouldReportGaiaOnly_WhenUuidMissingButBuvid3Present()
    {
        var cookie = TestDoubles.Cookie();
        cookie.CookieItemDictionary["buvid4"] = "stable-buvid4";
        // finger/spi 未被调用（否则抛异常）：buvid3 存在时不应重新生成
        var userInfoApi = TestDoubles.Api<IUserInfoApi>(
            (method, _) => throw new InvalidOperationException(method.Name)
        );
        var gaiaApi = TestDoubles.Api<IGaiaApi>(
            (method, _) =>
                method.Name == "ReportDeviceFingerprint"
                    ? Task.FromResult(new BiliApiResponse<object> { Code = 0 })
                    : throw new InvalidOperationException(method.Name)
        );
        var service = CreateService(userInfoApi, CreateSuccessfulHomeApi(), gaiaApi: gaiaApi);

        await service.SetCookieAsync(cookie, CancellationToken.None);

        Assert.Equal("buvid", cookie.CookieItemDictionary["buvid3"]);
        Assert.False(
            string.IsNullOrWhiteSpace(cookie.CookieItemDictionary.GetValueOrDefault("_uuid"))
        );
    }

    [Fact]
    public async Task SetCookieAsync_ShouldNotRefreshOrReport_WhenDeviceEnrolled()
    {
        var cookie = TestDoubles.Cookie();
        cookie.CookieItemDictionary["buvid4"] = "stable-buvid4";
        cookie.CookieItemDictionary["b_nut"] = "stable-bnut";
        cookie.CookieItemDictionary["_uuid"] = "stable-uuid";
        var userInfoApi = TestDoubles.Api<IUserInfoApi>(
            (method, _) => throw new InvalidOperationException(method.Name)
        );
        var service = CreateService(userInfoApi, CreateSuccessfulHomeApi());

        await service.SetCookieAsync(cookie, CancellationToken.None);

        Assert.Equal("buvid", cookie.CookieItemDictionary["buvid3"]);
        Assert.Equal("stable-buvid4", cookie.CookieItemDictionary["buvid4"]);
        Assert.Equal("stable-bnut", cookie.CookieItemDictionary["b_nut"]);
        Assert.Equal("stable-uuid", cookie.CookieItemDictionary["_uuid"]);
    }

    [Fact]
    public async Task SetCookieAsync_ShouldApplyPinnedDeviceCookies_WithoutRefreshOrReport()
    {
        var cookie = TestDoubles.Cookie();
        cookie.CookieItemDictionary["buvid4"] = "old-buvid4";
        var userInfoApi = TestDoubles.Api<IUserInfoApi>(
            (method, _) => throw new InvalidOperationException(method.Name)
        );
        var service = CreateService(
            userInfoApi,
            CreateSuccessfulHomeApi(),
            new DeviceCookieOptions
            {
                Buvid3 = "pinned-buvid3",
                Buvid4 = "pinned-buvid4",
                BNut = "pinned-bnut",
            }
        );

        await service.SetCookieAsync(cookie, CancellationToken.None);

        Assert.Equal("pinned-buvid3", cookie.CookieItemDictionary["buvid3"]);
        Assert.Equal("pinned-buvid4", cookie.CookieItemDictionary["buvid4"]);
        Assert.Equal("pinned-bnut", cookie.CookieItemDictionary["b_nut"]);
        // 固定配置模式下不写 _uuid、不上报 gaia
        Assert.True(string.IsNullOrWhiteSpace(cookie.CookieItemDictionary.GetValueOrDefault("_uuid")));
    }

    private static LoginDomainService CreateService(
        IUserInfoApi userInfoApi,
        IHomeApi homeApi,
        DeviceCookieOptions? deviceCookieOptions = null,
        IGaiaApi? gaiaApi = null
    ) =>
        new(
            NullLogger<LoginDomainService>.Instance,
            TestDoubles.Api<IPassportApi>(
                (method, _) => throw new InvalidOperationException(method.Name)
            ),
            userInfoApi,
            new TestHostEnvironment(),
            TestDoubles.Api<IQingLongApi>(
                (method, _) => throw new InvalidOperationException(method.Name)
            ),
            homeApi,
            new ConfigurationBuilder().Build(),
            Options.Create(new QingLongOptions()),
            Options.Create(deviceCookieOptions ?? new DeviceCookieOptions()),
            gaiaApi
                ?? TestDoubles.Api<IGaiaApi>(
                    (method, _) => throw new InvalidOperationException(method.Name)
                ),
            new VipBigPointAccessKeyStore()
        );

    private static IHomeApi CreateSuccessfulHomeApi() =>
        TestDoubles.Api<IHomeApi>(
            (method, _) =>
                method.Name == "GetHomePageAsync"
                    ? Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK))
                    : throw new InvalidOperationException(method.Name)
        );

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = nameof(LoginDomainServiceCookieRefreshTest);
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
