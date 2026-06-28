using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using QRCoder;
using Ray.BiliBiliTool.Agent;
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

                return biliCookie;
            }
            logger.LogError("访问主站失败：{msg}", homePage.ToJsonStr());
        }
        catch (Exception e)
        {
            //buvid只影响分享和投币，可以吞掉异常
            logger.LogError(e.ToJsonStr());
        }

        return biliCookie;
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

        await File.WriteAllTextAsync(
            fileInfo.PhysicalPath!,
            root.ToString(Formatting.Indented),
            cancellationToken
        );
        logger.LogInformation("已保存 VipBigPoint access_key 到本地 cookies.json");
    }

    public async Task SaveAccessKeyToQingLongAsync(
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
                logger.LogInformation(updateRe.Code == 200 ? "更新成功！" : updateRe.ToJsonStr());
                return;
            }

            logger.LogInformation("不存在 access_key，开始新增");
            var add = new AddQingLongEnv
            {
                name = envName,
                value = accessKey,
                remarks = $"BiliBiliToolPro App access_key ({userId})",
            };

            var addRe = await qingLongApi.AddEnvsAsync([add], token);
            logger.LogInformation(addRe.Code == 200 ? "新增成功！" : addRe.ToJsonStr());
        }
        catch (Exception ex)
        {
            logger.LogWarning("保存 access_key 到青龙失败：{msg}", ex.Message);
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

        await using var sw = new StreamWriter(fileInfo.PhysicalPath!);
        await sw.WriteAsync(newJson);
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
        logger.LogWarning("变量值：{value}", ckInfo.CookieStr);
        logger.LogWarning(
            "如果Key已存在，请自行+1，如Ray_BiliBiliCookies__1，Ray_BiliBiliCookies__2..."
        );
        return Task.CompletedTask;
    }

    #endregion

    #endregion
}
