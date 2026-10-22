using SlcMinecraftLaunch.Domain.Models;
using SlcMinecraftLaunch.Infrastructure.Persistence;

namespace SlcMinecraftLaunch.Tests;

public sealed class JsonDownloadTaskStoreTests
{
    [Fact]
    public async Task Store_RoundTripsTaskSourcesAndFailedFiles()
    {
        var root = Path.Combine(Path.GetTempPath(), $"slc-download-tasks-{Guid.NewGuid():N}");
        try
        {
            var now = DateTimeOffset.UtcNow;
            var expected = new DownloadTaskRecord(
                "task-1",
                "1.21.4",
                @"C:\Java\21\java.exe",
                @"C:\Minecraft",
                DownloadTaskStatus.Failed,
                "Download.Task.Failed",
                0.75,
                2,
                now,
                now,
                [new DownloadTaskFileRecord(
                    "asset.bin",
                    @"C:\Minecraft\assets\asset.bin",
                    "https://source.example/asset.bin",
                    42,
                    "ABC",
                    "timeout")],
                ["https://source.example/"]);
            var store = new JsonDownloadTaskStore(root);

            await store.SaveAsync([expected]);
            var loaded = Assert.Single(await new JsonDownloadTaskStore(root).LoadAsync());

            Assert.Equal(expected.TaskId, loaded.TaskId);
            Assert.Equal(expected.Status, loaded.Status);
            Assert.Equal(expected.FailedFiles, loaded.FailedFiles);
            Assert.Equal(expected.Sources, loaded.Sources);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }
    }
}
