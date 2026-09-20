using System.Security.Cryptography;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using QRCoder;
using Ray.BiliBiliTool.Agent;
using Ray.BiliBiliTool.Agent.BiliBiliAgent;
using Ray.BiliBiliTool.Agent.BiliBiliAgent.Dtos;
using Ray.BiliBiliTool.Agent.BiliBiliAgent.Dtos.Passport;
using Ray.BiliBiliTool.Agent.BiliBiliAgent.Interfaces;
using Ray.BiliBiliTool.Agent.BiliBiliAgent.Utils;
using Ray.BiliBiliTool.Agent.QingLong;
using Ray.BiliBiliTool.Agent.QingLong.Dtos;
using Ray.BiliBiliTool.Config.Options;
using Ray.BiliBiliTool.DomainService.Dtos;
using Ray.BiliBiliTool.DomainService.Interfaces;
using Ray.BiliBiliTool.Infrastructure.Cookie;
using Ray.BiliBiliTool.Infrastructure.IO;

namespace Ray.BiliBiliTool.DomainService;

/// <summary>
/// 账户
/// </summary>
public class LoginDomainService(
    ILogger<LoginDomainService> logger,
    IPassportApi passportApi,
    IUserInfoApi userInfoApi,
    IHostEnvironment hostingEnvironment,
    IQingLongApi qingLongApi,
    IHomeApi homeApi,
    IConfiguration configuration,
    IOptions<QingLongOptions> qingLongOptions,
    IOptions<DeviceCookieOptions> deviceCookieOptions,
    IGaiaApi gaiaApi,
    VipBigPointAccessKeyStore vipBigPointAccessKeyStore
) : ILoginDomainService
{
    public async Task<BiliCookie> LoginByQrCodeAsync(CancellationToken cancellationToken)
    {
        BiliCookie? cookieInfo = null;

        var re = await passportApi.GenerateQrCode();
        if (re.Code != 0)
        {
            throw new Exception($"获取二维码失败：{re.ToJsonStr()}");
        }

        if (re.Data is null)
            throw new InvalidOperationException($"获取二维码失败：{re.Message}");
        var url = re.Data.Url;
        GenerateQrCode(url);

        var online = GetOnlinePic(url);
        logger.LogInformation(Environment.NewLine + Environment.NewLine);
        logger.LogInformation(
            "如果上方二维码显示异常，或扫描失败，请使用浏览器访问如下链接，查看高清二维码："
        );
        logger.LogInformation(online + Environment.NewLine + Environment.NewLine);

        var waitTimes = 10;
        logger.LogInformation("我数到{num}，动作快点", waitTimes);
        for (int i = 0; i < waitTimes; i++)
        {
            logger.LogInformation("[{num}]等待扫描...", i + 1);

            await Task.Delay(5 * 1000, cancellationToken);

            var check = await passportApi.CheckQrCodeHasScaned(re.Data.Qrcode_key);
            if (!check.IsSuccessStatusCode)
            {
                logger.LogWarning("调用检测接口异常");
                continue;
            }

            var contentStr = await check.Content.ReadAsStringAsync(cancellationToken);
            var content = JsonConvert.DeserializeObject<BiliApiResponse<TokenDto>>(contentStr);
            if (content?.Code != 0)
            {
                logger.LogWarning("调用检测接口异常：{msg}", check.ToJsonStr());
                break;
            }

            if (content.Data is null)
            {
                logger.LogWarning("调用检测接口缺少数据：{msg}", content.Message);
                break;
            }

            if (content.Data.Code == 86038) //已失效
            {
                logger.LogInformation(content.Data.Message);
                break;
            }

            if (content.Data.Code == 0)
            {
                logger.LogInformation("扫描成功！");
                IEnumerable<string> cookies = check
                    .Headers.SingleOrDefault(header => header.Key == "Set-Cookie")
                    .Value;

                var cookieStr = CookieInfo.ConvertSetCkHeadersToCkStr(cookies);

                cookieInfo = CookieStrFactory<BiliCookie>.CreateNew(cookieStr);
                cookieInfo.Check();

                break;
            }

            logger.LogInformation("{msg}", content.Data.Message + Environment.NewLine);
        }

        if (cookieInfo == null)
        {
            throw new Exception("登录超时");
        }

        return cookieInfo;
    }

    public async Task<BiliCookie> SetCookieAsync(
        BiliCookie biliCookie,
        CancellationToken cancellationToken
    )
    {
        try
        {
            var homePage = await homeApi.GetHomePageAsync(biliCookie.ToString());
            if (homePage.IsSuccessStatusCode)
            {
                logger.LogInformation("访问主站成功");
                IEnumerable<string> setCookieHeaders = homePage
                    .Headers.SingleOrDefault(header => header.Key == "Set-Cookie")
                    .Value;
                if (setCookieHeaders != null)
                {
                    biliCookie.MergeCurrentCookieBySetCookieHeaders(setCookieHeaders);
                    logger.LogInformation("SetCookie成功");
                }
                else
                {
                    logger.LogInformation("无需set");
                }
            }
            else
            {
                logger.LogError("访问主站失败：{msg}", homePage.ToJsonStr());
            }

            if (ApplyPinnedDeviceCookie(biliCookie))
            {
                // 已应用固定设备指纹配置，跳过设备指纹建档
            }
            else
            {
                await EnsureDeviceCookieAsync(biliCookie, cancellationToken);
            }
        }
        catch (Exception e)
        {
            //buvid只影响分享和投币，可以吞掉异常
            logger.LogError(e.ToJsonStr());
        }

        return biliCookie;
    }

    private bool ApplyPinnedDeviceCookie(BiliCookie biliCookie)
    {
        var options = deviceCookieOptions.Value;
        if (!options.HasPinnedValues)
            return false;

        var applied = 0;
        if (!string.IsNullOrWhiteSpace(options.Buvid3))
        {
            biliCookie.CookieItemDictionary["buvid3"] = options.Buvid3;
            applied++;
        }

        if (!string.IsNullOrWhiteSpace(options.Buvid4))
        {
            biliCookie.CookieItemDictionary["buvid4"] = options.Buvid4;
            applied++;
        }

        if (!string.IsNullOrWhiteSpace(options.BNut))
        {
            biliCookie.CookieItemDictionary["b_nut"] = options.BNut;
            applied++;
        }

        logger.LogInformation(
            "已应用固定设备指纹配置，覆盖{count}项设备Cookie，本次不再刷新服务端指纹",
            applied
        );
        return true;
    }

    /// <summary>
    /// 确保当前设备指纹已在服务端建立档案。
    /// 程序生成（finger/spi）的 buvid3 是无档案的新设备，直接使用会被
    /// 分享等接口以 -403"账号异常"拒绝；需向 gaia 网关上报一次设备指纹
    /// （ExClimbWuzhi）激活（2026-09-15 生产容器实验验证）。
    /// 以 Cookie 中存在 _uuid 作为"已建档"标记，避免重复上报；
    /// 已建档的设备保持稳定使用，绝不每日刷新（刷新会丢失档案并触发风控）。
    /// </summary>
    private async Task EnsureDeviceCookieAsync(
        BiliCookie biliCookie,
        CancellationToken cancellationToken
    )
    {
        var items = biliCookie.CookieItemDictionary;
        var hasBuvid3 =
            items.TryGetValue("buvid3", out var buvid3)
            && !string.IsNullOrWhiteSpace(buvid3);
        var hasUuid =
            items.TryGetValue("_uuid", out var uuid) && !string.IsNullOrWhiteSpace(uuid);

        if (hasBuvid3 && hasUuid)
        {
            logger.LogInformation("设备指纹已激活（buvid3 与 _uuid 齐全），保持稳定不刷新");
            return;
        }

        if (!hasBuvid3)
        {
            logger.LogInformation("设备Cookie缺少buvid3，通过finger/spi生成");
            await RefreshDeviceFingerprintAsync(biliCookie);
            hasBuvid3 =
                items.TryGetValue("buvid3", out buvid3)
                && !string.IsNullOrWhiteSpace(buvid3);
            if (!hasBuvid3)
            {
                logger.LogWarning("设备指纹生成失败，跳过gaia上报");
                return;
            }
        }

        var deviceUuid = hasUuid ? uuid! : GenerateDeviceUuid();
        try
        {
            var payload = BuildGaiaPayload(deviceUuid);
            var response = await gaiaApi.ReportDeviceFingerprint(
                BuildDeviceCookieHeader(biliCookie, deviceUuid),
                new GaiaReportRequest { Payload = payload }
            );
            if (response?.Code == 0)
            {
                items["_uuid"] = deviceUuid;
                logger.LogInformation("设备指纹上报成功，服务端设备档案已建立");
            }
            else
            {
                logger.LogWarning(
                    "设备指纹上报失败，返回码：{code}，消息：{message}",
                    response?.Code,
                    response?.Message
                );
            }
        }
        catch (Exception e)
        {
            logger.LogWarning("设备指纹上报异常：{msg}", e.Message);
        }
    }

    /// <summary>
    /// 常见真实屏幕（宽, 高, 可用工作区高），用于指纹扰动
    /// </summary>
    private static readonly (int Width, int Height, int AvailHeight)[] ScreenProfiles =
    [
        (1920, 1080, 1048),
        (1600, 900, 868),
        (1536, 864, 832),
        (1440, 900, 868),
        (2560, 1440, 1408),
    ];

    /// <summary>
    /// 基于真实浏览器设备特征模板构造 ExClimbWuzhi payload，
    /// 替换动态字段：时间戳(5062)、来源页(03bf)、spm(39c8)、设备 _uuid(df35)。
    /// 并做指纹扰动（屏幕分辨率取真实常见组合、canvas 指纹尾部随机化），
    /// 降低多设备共享同一模板的服务端聚类特征
    /// （2026-09-15 生产容器实验验证：扰动后上报激活依然有效）。
    /// </summary>
    private static string BuildGaiaPayload(string deviceUuid)
    {
        var payload = JObject.Parse(GaiaDeviceFingerprintTemplate.PayloadJson);
        payload["5062"] = DateTimeOffset.Now.ToUnixTimeMilliseconds().ToString();
        payload["03bf"] = "https%3A%2F%2Fwww.bilibili.com%2F";
        payload["39c8"] = "333.1007.fp.risk";
        payload["df35"] = deviceUuid;

        var fingerprint = (JObject)payload["3c43"]!;
        var (width, height, availHeight) = ScreenProfiles[Random.Shared.Next(ScreenProfiles.Length)];
        fingerprint["748e"] = new JArray(width, height);
        fingerprint["d61f"] = new JArray(width, availHeight);
        fingerprint["13ab"] = PerturbCanvasTail((string)fingerprint["13ab"]!);
        fingerprint["bfe9"] = PerturbCanvasTail((string)fingerprint["bfe9"]!);

        return payload.ToString(Formatting.None);
    }

    /// <summary>
    /// canvas 指纹尾部（base64）随机化，保持格式合法
    /// </summary>
    private static string PerturbCanvasTail(string value)
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/";
        var chars = value.ToCharArray();
        var start = Math.Max(0, chars.Length - 16);
        for (var i = start; i < chars.Length; i++)
        {
            if (chars[i] != '=')
            {
                chars[i] = alphabet[Random.Shared.Next(alphabet.Length)];
            }
        }

        return new string(chars);
    }

    /// <summary>
    /// 仿浏览器 _uuid 格式：9-4-5-4-12 位大写十六进制 + 6 位数字 + "infoc" 后缀
    /// </summary>
    private static string GenerateDeviceUuid()
    {
        var hex = Convert.ToHexString(RandomNumberGenerator.GetBytes(18));
        return $"{hex[..9]}-{hex[9..13]}-{hex[13..18]}-{hex[18..22]}-{hex[22..34]}"
            + $"{Random.Shared.Next(100000, 999999)}infoc";
    }

    /// <summary>
    /// 构造 gaia 上报所需的设备 Cookie 头（buvid3/buvid4/b_nut/_uuid）
    /// </summary>
    private static string BuildDeviceCookieHeader(BiliCookie biliCookie, string deviceUuid)
    {
        var items = biliCookie.CookieItemDictionary;
        var parts = new List<string>();
        foreach (var key in new[] { "buvid3", "buvid4", "b_nut" })
        {
            if (items.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
            {
                parts.Add($"{key}={value}");
            }
        }

        parts.Add($"_uuid={deviceUuid}");
        return string.Join("; ", parts);
    }

    private async Task RefreshDeviceFingerprintAsync(BiliCookie biliCookie)
    {
        var response = await userInfoApi.GetDeviceFingerprint(biliCookie.ToString());
        if (response?.Code != 0 || response.Data is null)
        {
            logger.LogWarning(
                "刷新设备指纹失败，保留现有Cookie，返回码：{code}，消息：{message}",
                response?.Code,
                response?.Message
            );
            return;
        }

        var updated = 0;
        if (!string.IsNullOrWhiteSpace(response.Data.B_3))
        {
            biliCookie.CookieItemDictionary["buvid3"] = response.Data.B_3;
            updated++;
        }

        if (!string.IsNullOrWhiteSpace(response.Data.B_4))
        {
            biliCookie.CookieItemDictionary["buvid4"] = response.Data.B_4;
            updated++;
        }

        logger.LogInformation("设备指纹刷新成功，更新{count}项设备Cookie", updated);
    }

    public async Task<PassportTvLoginResult> LoginByTvQrCodeAsync(
        CancellationToken cancellationToken
    )
    {
        string localId = PassportTvLoginRequestSigner.CreateLocalId();
        var request = new PassportTvQrCodeAuthRequest(localId)
        {
            ts = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
        };
        request.sign = PassportTvLoginRequestSigner
            .BuildSignedQuery(localId, request.ts)
            .Split("&sign=")
            .Last();

        var authResponse = await passportApi.GenerateTvQrCodeAsync(
            request,
            PassportTvLoginRequestSigner.UserAgent,
            localId
        );
        if (authResponse.Code != 0 || authResponse.Data == null)
        {
            throw new Exception($"获取 App 登录二维码失败：{authResponse.ToJsonStr()}");
        }

        GenerateQrCode(authResponse.Data.url);
        logger.LogInformation(Environment.NewLine + Environment.NewLine);
        logger.LogInformation("请使用 B 站 App 扫描上方二维码完成登录");
        logger.LogInformation("二维码链接：{url}" + Environment.NewLine, authResponse.Data.url);

        const int waitTimes = 24;
        for (int i = 0; i < waitTimes; i++)
        {
            logger.LogInformation("[{num}]等待 App 扫码确认...", i + 1);
            await Task.Delay(5 * 1000, cancellationToken);

            var pollRequest = new PassportTvQrCodePollRequest(localId, authResponse.Data.auth_code)
            {
                ts = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            };
            pollRequest.sign = PassportTvLoginRequestSigner
                .BuildSignedQuery(
                    localId,
                    pollRequest.ts,
                    $"auth_code={authResponse.Data.auth_code}"
                )
                .Split("&sign=")
                .Last();

            var pollResponse = await passportApi.PollTvQrCodeAsync(
                pollRequest,
                PassportTvLoginRequestSigner.UserAgent,
                localId
            );
            if (pollResponse.Code == 0 && pollResponse.Data != null)
            {
                logger.LogInformation("App 二维码登录成功");
                return PassportTvLoginResult.Create(pollResponse.Data);
            }

            if (pollResponse.Code == PassportTvPollStatus.Expired)
            {
                throw new Exception($"App 二维码已失效：{pollResponse.Message}");
            }

            if (PassportTvPollStatus.ShouldKeepWaiting(pollResponse.Code))
            {
                logger.LogInformation("App 登录状态：{msg}", pollResponse.Message);
                continue;
            }

            throw new Exception($"App 二维码登录失败：{pollResponse.Message}");
        }

        throw new Exception("等待 App 扫码超时");
    }

    public async Task<string?> TryGetAccessKeyByTvQrCodeAsync(CancellationToken cancellationToken)
    {
        try
        {
            return (await LoginByTvQrCodeAsync(cancellationToken)).AccessKey;
        }
        catch (Exception ex)
        {
            logger.LogWarning("补全 access_key 失败：{msg}", ex.Message);
            return null;
        }
    }

    public async Task SaveCookieToJsonFileAsync(
        BiliCookie ckInfo,
        CancellationToken cancellationToken
    )
    {
        //读取json
        var path = hostingEnvironment.ContentRootPath;
        var indexOfBin = path.LastIndexOf("bin");
        if (indexOfBin != -1)
        {
            path = path[..indexOfBin];
        }
        if (string.Equals(configuration["PlatformType"], "Web", StringComparison.OrdinalIgnoreCase))
        {
            path = Path.Combine(path, "config");
        }
        var fileProvider = new PhysicalFileProvider(path);
        IFileInfo fileInfo = fileProvider.GetFileInfo("cookies.json");
        logger.LogInformation("目标json地址：{path}", fileInfo.PhysicalPath);

        if (!fileInfo.Exists)
        {
            await using var stream = File.Create(fileInfo.PhysicalPath!);
            await using var sw = new StreamWriter(stream);
            await sw.WriteAsync($"{{{Environment.NewLine}}}");
        }

        string json;
        await using (var stream = new FileStream(fileInfo.PhysicalPath!, FileMode.Open))
        {
            using var reader = new StreamReader(stream);
            json = await reader.ReadToEndAsync();
        }
        var lines = json.Split(Environment.NewLine).ToList();

        var indexOfCkConfigKey = lines.FindIndex(x =>
            x.TrimStart().StartsWith("\"BiliBiliCookies\"")
        );
        if (indexOfCkConfigKey == -1)
        {
            logger.LogInformation("未配置过cookie，初始化并新增");

            var indexOfInsert = lines.FindIndex(x => x.TrimStart().StartsWith("{"));
            lines.InsertRange(
                indexOfInsert + 1,
                new List<string>()
                {
                    "  \"BiliBiliCookies\":[",
                    $@"    ""{ckInfo.CookieStr}"",",
                    "  ],",
                }
            );

            await SaveJson(lines, fileInfo);
            logger.LogInformation("新增成功！");
            return;
        }

        ckInfo.CookieItemDictionary.TryGetValue("DedeUserID", out var userId);
        userId ??= ckInfo.CookieStr;
        var indexOfCkConfigEnd = lines.FindIndex(
            indexOfCkConfigKey,
            x => x.TrimStart().StartsWith("]")
        );
        var indexOfTargetCk = lines.FindIndex(
            indexOfCkConfigKey,
            indexOfCkConfigEnd - indexOfCkConfigKey,
            x => x.Contains(userId) && !x.TrimStart().StartsWith("//")
        );

        if (indexOfTargetCk == -1)
        {
            logger.LogInformation("不存在该用户，新增cookie");
            lines.Insert(indexOfCkConfigEnd, $@"    ""{ckInfo.CookieStr}"",");
            await SaveJson(lines, fileInfo);
            logger.LogInformation("新增成功！");
            return;
        }

        logger.LogInformation("已存在该用户，更新cookie");
        lines[indexOfTargetCk] = $@"    ""{ckInfo.CookieStr}"",";
        await SaveJson(lines, fileInfo);
        logger.LogInformation("更新成功！");
    }

    public async Task SaveAccessKeyToJsonFileAsync(
        string userId,
        string accessKey,
        CancellationToken cancellationToken
    )
    {
        vipBigPointAccessKeyStore.Set(userId, accessKey);
        var fileInfo = await EnsureLocalConfigFileAsync();
        string json = await File.ReadAllTextAsync(fileInfo.PhysicalPath!, cancellationToken);
        JObject root = string.IsNullOrWhiteSpace(json) ? new JObject() : JObject.Parse(json);

        JObject vipBigPointConfig = root["VipBigPointConfig"] as JObject ?? new JObject();
        JObject accessKeys = vipBigPointConfig["AccessKeys"] as JObject ?? new JObject();
        accessKeys[userId] = accessKey;
        vipBigPointConfig["AccessKeys"] = accessKeys;
        root["VipBigPointConfig"] = vipBigPointConfig;

        await AtomicFileWriter.WriteAsync(
            fileInfo.PhysicalPath!,
            async stream =>
            {
                await using var writer = new StreamWriter(stream, leaveOpen: true);
                await writer.WriteAsync(root.ToString(Formatting.Indented));
                await writer.FlushAsync(cancellationToken);
            },
            cancellationToken
        );
        logger.LogInformation("已保存 VipBigPoint access_key 到本地 cookies.json");
    }

    public async Task<bool> SaveAccessKeyToQingLongAsync(
        string userId,
        string accessKey,
        CancellationToken cancellationToken
    )
    {
        vipBigPointAccessKeyStore.Set(userId, accessKey);
        string envName = $"Ray_VipBigPointConfig__AccessKeys__{userId}";

        try
        {
            var token = await GetQingLongAuthTokenAsync();
            if (string.IsNullOrEmpty(token))
            {
                throw new Exception("获取青龙token失败");
            }

            var qlEnvList = await qingLongApi.GetEnvsAsync(envName, token);
            if (qlEnvList.Code != 200)
            {
                throw new Exception($"查询环境变量失败：{qlEnvList.ToJsonStr()}");
            }

            var existingEnv = qlEnvList.Data.FirstOrDefault(x => x.name == envName);
            if (existingEnv != null)
            {
                logger.LogInformation("已存在 access_key，开始更新");
                var update = new UpdateQingLongEnv
                {
                    id = existingEnv.id,
                    name = existingEnv.name,
                    value = accessKey,
                    remarks = existingEnv.remarks ?? $"BiliBiliToolPro App access_key ({userId})",
                };

                var updateRe = await qingLongApi.UpdateEnvsAsync(update, token);
                if (updateRe.Code != 200)
                {
                    logger.LogError("更新青龙 access_key 失败，返回码：{code}", updateRe.Code);
                    return false;
                }

                logger.LogInformation("更新成功！");
                return true;
            }

            logger.LogInformation("不存在 access_key，开始新增");
            var add = new AddQingLongEnv
            {
                name = envName,
                value = accessKey,
                remarks = $"BiliBiliToolPro App access_key ({userId})",
            };

            var addRe = await qingLongApi.AddEnvsAsync([add], token);
            if (addRe.Code != 200)
            {
                logger.LogError("新增青龙 access_key 失败，返回码：{code}", addRe.Code);
                return false;
            }

            logger.LogInformation("新增成功！");
            return true;
        }
        catch (Exception ex)
        {
            logger.LogWarning("保存 access_key 到青龙失败：{msg}", ex.Message);
            return false;
        }
    }

    public async Task<bool> SaveCookieToQinLongAsync(
        BiliCookie ckInfo,
        CancellationToken cancellationToken
    )
    {
        try
        {
            var userName = await TryGetUserNameAsync(ckInfo);
            var token = await GetQingLongAuthTokenAsync();
            if (string.IsNullOrEmpty(token))
            {
                throw new Exception("获取青龙token失败");
            }

            var qlEnvList = await qingLongApi.GetEnvsAsync("Ray_BiliBiliCookies__", token);
            if (qlEnvList.Code != 200)
            {
                throw new Exception($"查询环境变量失败：{qlEnvList.ToJsonStr()}");
            }

            logger.LogDebug(qlEnvList.Data.ToJsonStr());
            logger.LogDebug(ckInfo.ToString());

            var list = qlEnvList
                .Data.Where(x => x.name.StartsWith("Ray_BiliBiliCookies__"))
                .ToList();
            var oldEnv = list.FirstOrDefault(x => x.value.Contains(ckInfo.UserId));

            if (oldEnv != null)
            {
                logger.LogInformation("用户已存在，更新cookie");
                logger.LogInformation("Key：{key}", oldEnv.name);
                var update = new UpdateQingLongEnv
                {
                    id = oldEnv.id,
                    name = oldEnv.name,
                    value = ckInfo.CookieStr,
                    remarks = QingLongCookieRemarkFormatter.ResolveRemark(
                        ckInfo.UserId,
                        userName,
                        oldEnv.remarks
                    ),
                };

                var updateRe = await qingLongApi.UpdateEnvsAsync(update, token);
                logger.LogInformation(updateRe.Code == 200 ? "更新成功！" : updateRe.ToJsonStr());

                return true;
            }

            logger.LogInformation("用户不存在，新增cookie");
            var maxNum = -1;
            if (list.Any())
            {
                maxNum = list.Select(x =>
                    {
                        var num = x.name.Replace("Ray_BiliBiliCookies__", "");
                        var parseSuc = int.TryParse(num, out int envNum);
                        return parseSuc ? envNum : 0;
                    })
                    .Max();
            }

            var name = $"Ray_BiliBiliCookies__{maxNum + 1}";
            logger.LogInformation("Key：{key}", name);

            var add = new AddQingLongEnv
            {
                name = name,
                value = ckInfo.CookieStr,
                remarks = QingLongCookieRemarkFormatter.BuildAutoRemark(ckInfo.UserId, userName),
            };
            var addRe = await qingLongApi.AddEnvsAsync([add], token);
            logger.LogInformation(addRe.Code == 200 ? "新增成功！" : addRe.ToJsonStr());
            return true;
        }
        catch
        {
            await PrintIfSaveCookieFailAsync(ckInfo, cancellationToken);
            return false;
        }
    }

    private async Task<string?> TryGetUserNameAsync(BiliCookie ckInfo)
    {
        try
        {
            var response = await userInfoApi.LoginByCookie(ckInfo.ToString());
            if (response.Code == 0 && response.Data?.IsLogin == true)
            {
                return response.Data.Uname;
            }
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "获取用户名用于生成青龙备注失败");
        }

        return null;
    }

    #region private

    private void GenerateQrCode(string str)
    {
        var qrGenerator = new QRCodeGenerator();
        QRCodeData qrCodeData = qrGenerator.CreateQrCode(str, QRCodeGenerator.ECCLevel.L);

        logger.LogInformation("AsciiQRCode：");
        //var qrCode = new AsciiQRCode(qrCodeData);
        //var qrCodeStr = qrCode.GetGraphic(1, drawQuietZones: false);
        //_logger.LogInformation(Environment.NewLine + qrCodeStr);

        //Console.WriteLine("Console：");
        //Print(qrCodeData);
        PrintSmall(qrCodeData);
    }

    private void Print(QRCodeData qrCodeData)
    {
        Console.BackgroundColor = ConsoleColor.White;
        for (int i = 0; i < qrCodeData.ModuleMatrix.Count + 2; i++)
            Console.Write("　"); //中文全角的空格符
        Console.WriteLine();
        for (int j = 0; j < qrCodeData.ModuleMatrix.Count; j++)
        {
            for (int i = 0; i < qrCodeData.ModuleMatrix.Count; i++)
            {
                //char charToPoint = qrCode.Matrix[i, j] ? '█' : '　';
                Console.Write(i == 0 ? "　" : ""); //中文全角的空格符
                Console.BackgroundColor = qrCodeData.ModuleMatrix[i][j]
                    ? ConsoleColor.Black
                    : ConsoleColor.White;
                Console.Write('　'); //中文全角的空格符
                Console.BackgroundColor = ConsoleColor.White;
                Console.Write(i == qrCodeData.ModuleMatrix.Count - 1 ? "　" : ""); //中文全角的空格符
            }
            Console.WriteLine();
        }
        for (int i = 0; i < qrCodeData.ModuleMatrix.Count + 2; i++)
            Console.Write("　"); //中文全角的空格符

        Console.WriteLine();
    }

    private void PrintSmall(QRCodeData qrCodeData)
    {
        //黑黑（" "）
        //白白（"█"）
        //黑白（"▄"）
        //白黑（"▀"）
        var dic = new Dictionary<string, char>()
        {
            { "11", ' ' },
            { "00", '█' },
            { "10", '▄' },
            { "01", '▀' }, //todo:win平台的cmd会显示？,是已知问题，待想办法解决
            //{"01", '^'},//▼▔
        };

        var count = qrCodeData.ModuleMatrix.Count;

        var list = new List<string>();
        for (int rowNum = 0; rowNum < count; rowNum++)
        {
            var rowStr = "";
            for (int colNum = 0; colNum < count; colNum++)
            {
                var num = qrCodeData.ModuleMatrix[colNum][rowNum] ? "1" : "0";
                var numDown = "0";
                if (rowNum + 1 < count)
                    numDown = qrCodeData.ModuleMatrix[colNum][rowNum + 1] ? "1" : "0";

                rowStr += dic[num + numDown];
            }
            list.Add(rowStr);
            rowNum++;
        }

        logger.LogInformation(Environment.NewLine + string.Join(Environment.NewLine, list));
    }

    private string GetOnlinePic(string str)
    {
        var encode = System.Web.HttpUtility.UrlEncode(str);
        return $"https://tool.lu/qrcode/basic.html?text={encode}";
    }

    private async Task SaveJson(List<string> lines, IFileInfo fileInfo)
    {
        var newJson = string.Join(Environment.NewLine, lines);

        await AtomicFileWriter.WriteAsync(
            fileInfo.PhysicalPath!,
            async stream =>
            {
                await using var writer = new StreamWriter(stream, leaveOpen: true);
                await writer.WriteAsync(newJson);
                await writer.FlushAsync();
            }
        );
    }

    private async Task<IFileInfo> EnsureLocalConfigFileAsync()
    {
        string path = hostingEnvironment.ContentRootPath;
        var indexOfBin = path.LastIndexOf("bin");
        if (indexOfBin != -1)
        {
            path = path[..indexOfBin];
        }
        if (string.Equals(configuration["PlatformType"], "Web", StringComparison.OrdinalIgnoreCase))
        {
            path = Path.Combine(path, "config");
        }

        var fileProvider = new PhysicalFileProvider(path);
        IFileInfo fileInfo = fileProvider.GetFileInfo("cookies.json");
        if (!fileInfo.Exists)
        {
            await File.WriteAllTextAsync(fileInfo.PhysicalPath!, $"{{{Environment.NewLine}}}");
        }

        return fileInfo;
    }

    #region qinglong

    private async Task<string> GetQingLongAuthTokenAsync()
    {
        logger.LogWarning("使用OpenAPI鉴权");
        if (
            string.IsNullOrWhiteSpace(qingLongOptions.Value.ClientId)
            || string.IsNullOrWhiteSpace(qingLongOptions.Value.ClientSecret)
        )
        {
            logger.LogWarning("未配置青龙的ClientId和ClientSecret，无法自动获取token");
            logger.LogWarning(
                "教程：{qingDoc}",
                Ray.BiliBiliTool.Config.Constants.QingLongReadmeUrl
            );
            return "";
        }

        var token = await qingLongApi.GetTokenAsync(
            qingLongOptions.Value.ClientId!,
            qingLongOptions.Value.ClientSecret!
        );

        return $"{token.Data.token_type} {token.Data.token}";
    }

    private Task PrintIfSaveCookieFailAsync(BiliCookie ckInfo, CancellationToken cancellationToken)
    {
        logger.LogError("持久化失败，青龙版本高于2.18，请手动添加环境变量到青龙");
        logger.LogWarning("变量Key：{key}", "Ray_BiliBiliCookies__0");
        logger.LogWarning("Cookie 内容已隐藏，请在青龙环境变量页面手动填写");
        logger.LogWarning(
            "如果Key已存在，请自行+1，如Ray_BiliBiliCookies__1，Ray_BiliBiliCookies__2..."
        );
        return Task.CompletedTask;
    }

    #endregion

    #endregion
}
