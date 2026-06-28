using Microsoft.Extensions.Logging.Abstractions;

namespace DomainServiceTest;

public sealed class DonateCoinSelectionStateStoreTest
{
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
                new DonateCoinConfigUpProgressSnapshot(3, new DateOnly(2026, 6, 27), 2, 1)
            );

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
}
