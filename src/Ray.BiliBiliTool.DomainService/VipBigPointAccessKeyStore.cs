namespace Ray.BiliBiliTool.DomainService;

public sealed class VipBigPointAccessKeyStore
{
    private readonly Dictionary<string, string> _accessKeysByUserId = [];
    private readonly object _gate = new();

    public void Set(string userId, string accessKey)
    {
        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(accessKey))
        {
            return;
        }

        lock (_gate)
        {
            _accessKeysByUserId[userId] = accessKey.Trim();
        }
    }

    public string? Get(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return null;
        }

        lock (_gate)
        {
            return _accessKeysByUserId.TryGetValue(userId, out var accessKey) ? accessKey : null;
        }
    }
}
