using Ray.BiliBiliTool.Infrastructure.IO;

namespace InfrastructureTest;

public sealed class AtomicFileWriterTest
{
    [Fact]
    public async Task WriteAsync_WhenWriterFails_ShouldKeepOriginalAndDeleteTemporaryFile()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);

        try
        {
            var targetPath = Path.Combine(tempDirectory, "state.json");
            await File.WriteAllTextAsync(targetPath, "original");

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                AtomicFileWriter.WriteAsync(
                    targetPath,
                    async stream =>
                    {
                        await using var writer = new StreamWriter(stream, leaveOpen: true);
                        await writer.WriteAsync("replacement");
                        await writer.FlushAsync();
                        throw new InvalidOperationException("write failed");
                    }
                )
            );

            Assert.Equal("original", await File.ReadAllTextAsync(targetPath));
            Assert.Empty(Directory.GetFiles(tempDirectory, ".state.json.*.tmp"));
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }
}
