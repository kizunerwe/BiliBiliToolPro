using System.Text.Json;
using Microsoft.Extensions.Logging;
using Ray.BiliBiliTool.Infrastructure;

namespace Ray.BiliBiliTool.DomainService;

public sealed class DonateCoinSelectionStateStore(
    ILogger<DonateCoinSelectionStateStore> logger,
    string? stateFilePath = null
)
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly string _stateFilePath =
        stateFilePath ?? Path.Combine(AppContext.BaseDirectory, "config", "donate-coin-state.json");
    private readonly JsonSerializerOptions _jsonSerializerOptions = new(
        JsonSerializerOptionsBuilder.DefaultOptions
    )
    {
        WriteIndented = true,
    };

    private DonateCoinSelectionStateDocument? _cachedState;

    public async Task<DonateCoinAccountSelectionStateSnapshot> GetAccountStateAsync(string userId)
    {
        await _gate.WaitAsync();
        try
        {
            var document = await GetOrLoadStateAsync();
            if (!document.Accounts.TryGetValue(userId, out var accountState))
            {
                return DonateCoinAccountSelectionStateSnapshot.Empty;
            }

            return CreateSnapshot(accountState);
        }
        finally
        {
            _gate.Release();
        }
    }

    public Task MarkVideoAsBlacklistedAsync(string userId, long aid)
    {
        return UpdateAccountStateAsync(
            userId,
            accountState => accountState.BlacklistedAids.Add(aid)
        );
    }

    public Task UpdateConfigUpProgressAsync(
        string userId,
        long upId,
        DonateCoinConfigUpProgressSnapshot progress
    )
    {
        return UpdateAccountStateAsync(
            userId,
            accountState =>
            {
                accountState.ConfigUpProgressByUpId[upId] = new DonateCoinConfigUpProgressState
                {
                    VideoCount = progress.VideoCount,
                    VideoCountUpdatedOn = progress.VideoCountUpdatedOn,
                    NextPageNumber = progress.NextPageNumber,
                    RecordedVideoCount = progress.RecordedVideoCount,
                };
            }
        );
    }

    private async Task UpdateAccountStateAsync(
        string userId,
        Action<DonateCoinAccountState> updater
    )
    {
        await _gate.WaitAsync();
        try
        {
            var document = await GetOrLoadStateAsync();
            if (!document.Accounts.TryGetValue(userId, out var accountState))
            {
                accountState = new DonateCoinAccountState();
                document.Accounts[userId] = accountState;
            }

            updater(accountState);
            await SaveStateAsync(document);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<DonateCoinSelectionStateDocument> GetOrLoadStateAsync()
    {
        if (_cachedState != null)
        {
            return _cachedState;
        }

        _cachedState = await LoadStateAsync();
        return _cachedState;
    }

    private async Task<DonateCoinSelectionStateDocument> LoadStateAsync()
    {
        try
        {
            if (!File.Exists(_stateFilePath))
            {
                return new DonateCoinSelectionStateDocument();
            }

            await using var stream = File.OpenRead(_stateFilePath);
            return await JsonSerializer.DeserializeAsync<DonateCoinSelectionStateDocument>(
                    stream,
                    _jsonSerializerOptions
                ) ?? new DonateCoinSelectionStateDocument();
        }
        catch (Exception ex)
        {
            logger.LogWarning("读取投币选择进度缓存失败，已回退为空状态：{message}", ex.Message);
            return new DonateCoinSelectionStateDocument();
        }
    }

    private async Task SaveStateAsync(DonateCoinSelectionStateDocument document)
    {
        var directory = Path.GetDirectoryName(_stateFilePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var stream = File.Create(_stateFilePath);
        await JsonSerializer.SerializeAsync(stream, document, _jsonSerializerOptions);
    }

    private static DonateCoinAccountSelectionStateSnapshot CreateSnapshot(
        DonateCoinAccountState accountState
    )
    {
        return new DonateCoinAccountSelectionStateSnapshot(
            accountState.BlacklistedAids.ToHashSet(),
            accountState.ConfigUpProgressByUpId.ToDictionary(
                x => x.Key,
                x => new DonateCoinConfigUpProgressSnapshot(
                    x.Value.VideoCount,
                    x.Value.VideoCountUpdatedOn,
                    x.Value.NextPageNumber,
                    x.Value.RecordedVideoCount
                )
            )
        );
    }

    private sealed class DonateCoinSelectionStateDocument
    {
        public Dictionary<string, DonateCoinAccountState> Accounts { get; set; } = [];
    }

    private sealed class DonateCoinAccountState
    {
        public HashSet<long> BlacklistedAids { get; set; } = [];

        public Dictionary<
            long,
            DonateCoinConfigUpProgressState
        > ConfigUpProgressByUpId { get; set; } = [];
    }

    private sealed class DonateCoinConfigUpProgressState
    {
        public int VideoCount { get; set; }

        public DateOnly VideoCountUpdatedOn { get; set; }

        public int NextPageNumber { get; set; }

        public int RecordedVideoCount { get; set; }
    }
}

public sealed record DonateCoinAccountSelectionStateSnapshot(
    IReadOnlySet<long> BlacklistedAids,
    IReadOnlyDictionary<long, DonateCoinConfigUpProgressSnapshot> ConfigUpProgressByUpId
)
{
    public static DonateCoinAccountSelectionStateSnapshot Empty { get; } =
        new(new HashSet<long>(), new Dictionary<long, DonateCoinConfigUpProgressSnapshot>());
}

public sealed record DonateCoinConfigUpProgressSnapshot(
    int VideoCount,
    DateOnly VideoCountUpdatedOn,
    int NextPageNumber,
    int RecordedVideoCount
);
