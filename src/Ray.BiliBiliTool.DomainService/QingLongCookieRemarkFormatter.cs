namespace Ray.BiliBiliTool.DomainService;

public static class QingLongCookieRemarkFormatter
{
    public static string BuildAutoRemark(string userId, string? userName)
    {
        if (!string.IsNullOrWhiteSpace(userName))
        {
            return $"{userName} | {userId}";
        }

        return $"UID {userId}";
    }

    public static string ResolveRemark(string userId, string? userName, string? currentRemark)
    {
        if (string.IsNullOrWhiteSpace(currentRemark) || IsLegacyAutoRemark(userId, currentRemark))
        {
            return BuildAutoRemark(userId, userName);
        }

        return currentRemark;
    }

    private static bool IsLegacyAutoRemark(string userId, string currentRemark)
    {
        return string.Equals(currentRemark, $"bili-{userId}", StringComparison.OrdinalIgnoreCase);
    }
}
