using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Ray.BiliBiliTool.Agent.BiliBiliAgent.Dtos.VipTask;

namespace Ray.BiliBiliTool.Agent.BiliBiliAgent.Utils;

public static class VipBigPointAppRequestSigner
{
    public const string AppKey = "1d8b6e7d45233436";
    public const string Build = "7720200";
    private const string AppSec = "560c52ccd288fed045859ed18bffd973";
    private const string OgvTaskSecret = "df2a46fd53";

    public static void PopulateOgvTaskSigns(CompleteOgvWatchRequest request)
    {
        request.task_sign = ToMd5($"{request.timestamp}#{OgvTaskSecret}&{request.token}");
    }

    public static void PopulateAppSign(StartOgvWatchRequest request, string accessKey)
    {
        PopulateAppSignCore(request, accessKey);
    }

    public static void PopulateAppSign(CompleteOgvWatchRequest request, string accessKey)
    {
        PopulateAppSignCore(request, accessKey);
    }

    public static void PopulateAppSign(VipBigPointTaskAppRequest request, string accessKey)
    {
        PopulateAppSignCore(request, accessKey);
    }

    private static void PopulateAppSignCore(object request, string accessKey)
    {
        if (string.IsNullOrWhiteSpace(accessKey))
        {
            return;
        }

        SetPropertyValue(request, "access_key", accessKey.Trim());
        SetPropertyValue(request, "actionKey", "appkey");
        SetPropertyValue(request, "appkey", AppKey);
        SetPropertyValue(request, "csrf", null);

        string sign = BuildSign(ToDictionary(request, "sign"));
        SetPropertyValue(request, "sign", sign);
    }

    private static string BuildSign(IReadOnlyDictionary<string, string> parameters)
    {
        var sortedParams = string.Join(
            "&",
            parameters
                .OrderBy(x => x.Key, StringComparer.Ordinal)
                .Select(x => $"{Uri.EscapeDataString(x.Key)}={Uri.EscapeDataString(x.Value)}")
        );

        return ToMd5(sortedParams + AppSec);
    }

    private static Dictionary<string, string> ToDictionary(
        object request,
        params string[] excludedKeys
    )
    {
        HashSet<string> excluded = [.. excludedKeys];

        return request
            .GetType()
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(x => x.CanRead && !excluded.Contains(x.Name))
            .Select(x => new { x.Name, Value = x.GetValue(request) })
            .Where(x => x.Value is not null)
            .ToDictionary(x => x.Name, x => x.Value!.ToString() ?? string.Empty);
    }

    private static void SetPropertyValue(object request, string propertyName, object? value)
    {
        PropertyInfo? property = request.GetType().GetProperty(propertyName);
        if (property?.CanWrite == true)
        {
            property.SetValue(request, value);
        }
    }

    private static string ToMd5(string text)
    {
        using var md5 = MD5.Create();
        var bytes = md5.ComputeHash(Encoding.UTF8.GetBytes(text));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
