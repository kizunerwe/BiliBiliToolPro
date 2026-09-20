using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;

namespace DomainServiceTest;

public sealed class DonateCoinSelectionStateStoreTest
{
    [Fact]
    public async Task MarkConfigUpVideoTerminal_ShouldPersistBlacklistAndProgressTogether()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);

        try
        {
            var stateFilePath = Path.Combine(tempDirectory, "state.json");
            var store = new DonateCoinSelectionStateStore(
                NullLogger<DonateCoinSelectionStateStore>.Instance,
                stateFilePath
            );
            var progress = new DonateCoinConfigUpProgressSnapshot(
                3,
                new DateOnly(2026, 8, 12),
                1,
                1,
                DonateCoinConfigUpScanStatus.InProgress,
                NextVideoIndex: 2,
                RecordedAids: new HashSet<long> { 100 }
            );

            await store.MarkConfigUpVideoTerminalAsync("10001", 487417170, 101, progress);
            await store.MarkConfigUpVideoTerminalAsync("10001", 487417170, 101, progress);

            var reloaded = new DonateCoinSelectionStateStore(
                NullLogger<DonateCoinSelectionStateStore>.Instance,
                stateFilePath
            );
            var account = await reloaded.GetAccountStateAsync("10001");
            Assert.Contains(101, account.BlacklistedAids);
            Assert.Equal(2, account.ConfigUpProgressByUpId[487417170].RecordedVideoCount);
            Assert.Contains(101, account.ConfigUpProgressByUpId[487417170].RecordedAids);
            Assert.Equal(2, account.ConfigUpProgressByUpId[487417170].NextVideoIndex);
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task StateStore_ShouldPersistBlacklistAndProgressPerAccount()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);

        try
        {
            var stateFilePath = Path.Combine(tempDirectory, "donate-coin-state.json");
            var store = new DonateCoinSelectionStateStore(
                NullLogger<DonateCoinSelectionStateStore>.Instance,
                stateFilePath
            );

            await store.MarkVideoAsBlacklistedAsync("10001", 101);
            await store.UpdateConfigUpProgressAsync(
                "10001",
                487417170,
                new DonateCoinConfigUpProgressSnapshot(
                    3,
                    new DateOnly(2026, 6, 27),
                    2,
                    1,
                    NextVideoIndex: 2,
                    RecordedAids: new HashSet<long> { 101 }
                )
            );

            Assert.Empty(Directory.GetFiles(tempDirectory, ".donate-coin-state.json.*.tmp"));

            var reloaded = new DonateCoinSelectionStateStore(
                NullLogger<DonateCoinSelectionStateStore>.Instance,
                stateFilePath
            );

            var firstAccount = await reloaded.GetAccountStateAsync("10001");
            var secondAccount = await reloaded.GetAccountStateAsync("20002");

            Assert.Contains(101, firstAccount.BlacklistedAids);
            Assert.Equal(2, firstAccount.ConfigUpProgressByUpId[487417170].NextPageNumber);
            Assert.Equal(3, firstAccount.ConfigUpProgressByUpId[487417170].VideoCount);
            Assert.Equal(1, firstAccount.ConfigUpProgressByUpId[487417170].RecordedVideoCount);
            Assert.Equal(2, firstAccount.ConfigUpProgressByUpId[487417170].NextVideoIndex);
            Assert.Contains(101, firstAccount.ConfigUpProgressByUpId[487417170].RecordedAids);
            Assert.Empty(secondAccount.BlacklistedAids);
            Assert.Empty(secondAccount.ConfigUpProgressByUpId);
        }
        finally
        {
            if (Directory.Exists(tempDirectory))
            {
                Directory.Delete(tempDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public async Task Load_ShouldMapLegacyProgressWithoutStatusToUnknown()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);

        try
        {
            var stateFilePath = Path.Combine(tempDirectory, "donate-coin-state.json");
            await File.WriteAllTextAsync(
                stateFilePath,
                """
                {
                  "accounts": {
                    "10001": {
                      "blacklistedAids": [],
                      "configUpProgressByUpId": {
                        "487417170": {
                          "videoCount": 3,
                          "videoCountUpdatedOn": "2026-06-27",
                          "nextPageNumber": 2,
                          "recordedVideoCount": 1
                        }
                      }
                    }
                  }
                }
                """
            );

            var store = new DonateCoinSelectionStateStore(
                NullLogger<DonateCoinSelectionStateStore>.Instance,
                stateFilePath
            );

            var snapshot = await store.GetAccountStateAsync("10001");
            var progress = snapshot.ConfigUpProgressByUpId[487417170];

            Assert.Equal(DonateCoinConfigUpScanStatus.Unknown, progress.Status);
            Assert.Null(progress.FailureReason);
            Assert.Equal(0, progress.NextVideoIndex);
            Assert.Empty(progress.RecordedAids);
        }
        finally
        {
            if (Directory.Exists(tempDirectory))
            {
                Directory.Delete(tempDirectory, recursive: true);
            }
        }
    }

    [Theory]
    [InlineData(DonateCoinConfigUpScanStatus.Unknown)]
    [InlineData(DonateCoinConfigUpScanStatus.InProgress)]
    [InlineData(DonateCoinConfigUpScanStatus.ConfirmedExhausted)]
    [InlineData(DonateCoinConfigUpScanStatus.RetryableFailure)]
    public async Task SaveAndLoad_ShouldPreserveScanStatus(DonateCoinConfigUpScanStatus status)
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);

        try
        {
            var stateFilePath = Path.Combine(tempDirectory, "donate-coin-state.json");
            var store = new DonateCoinSelectionStateStore(
                NullLogger<DonateCoinSelectionStateStore>.Instance,
                stateFilePath
            );

            await store.UpdateConfigUpProgressAsync(
                "10001",
                487417170,
                new DonateCoinConfigUpProgressSnapshot(3, new DateOnly(2026, 6, 27), 2, 1, status)
            );

            var reloaded = new DonateCoinSelectionStateStore(
                NullLogger<DonateCoinSelectionStateStore>.Instance,
                stateFilePath
            );
            var progress = (await reloaded.GetAccountStateAsync("10001")).ConfigUpProgressByUpId[
                487417170
            ];

            Assert.Equal(status, progress.Status);
        }
        finally
        {
            if (Directory.Exists(tempDirectory))
            {
                Directory.Delete(tempDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public async Task SaveAndLoad_ShouldPreserveRetryableFailureReason()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);

        try
        {
            var stateFilePath = Path.Combine(tempDirectory, "donate-coin-state.json");
            var store = new DonateCoinSelectionStateStore(
                NullLogger<DonateCoinSelectionStateStore>.Instance,
                stateFilePath
            );
            var reason = "获取第2页失败";

            await store.UpdateConfigUpProgressAsync(
                "10001",
                487417170,
                new DonateCoinConfigUpProgressSnapshot(
                    3,
                    new DateOnly(2026, 6, 27),
                    2,
                    1,
                    DonateCoinConfigUpScanStatus.RetryableFailure,
                    reason
                )
            );

            var reloaded = new DonateCoinSelectionStateStore(
                NullLogger<DonateCoinSelectionStateStore>.Instance,
                stateFilePath
            );
            var progress = (await reloaded.GetAccountStateAsync("10001")).ConfigUpProgressByUpId[
                487417170
            ];

            Assert.Equal(DonateCoinConfigUpScanStatus.RetryableFailure, progress.Status);
            Assert.Equal(reason, progress.FailureReason);
        }
        finally
        {
            if (Directory.Exists(tempDirectory))
            {
                Directory.Delete(tempDirectory, recursive: true);
            }
        }
    }
}
