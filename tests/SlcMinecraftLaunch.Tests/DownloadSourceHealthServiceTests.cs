using SlcMinecraftLaunch.Domain.Models;
using SlcMinecraftLaunch.Infrastructure.Persistence;

namespace SlcMinecraftLaunch.Tests;

public sealed class DownloadSourceHealthServiceTests
{
    [Fact]
    public async Task Scores_PersistSuccessBonusAndFailurePenalty()
    {
        var root = Path.Combine(Path.GetTempPath(), $"slc-source-health-{Guid.NewGuid():N}");
        try
        {
            var source = "https://downloads.example/root/";
            var store = new JsonSourceHealthStore(root);
            var service = new DownloadSourceHealthService(store);

            await service.RecordAsync(
                [new DownloadSourceObservation(source, 8, 0)]);
            var afterSuccess = (await service.GetScoresAsync([source]))[source];

            Assert.True(afterSuccess.Score > 50);
            Assert.Equal(8, afterSuccess.SuccessCount);
            Assert.Equal(8, afterSuccess.ConsecutiveSuccesses);

            var reloaded = new DownloadSourceHealthService(new JsonSourceHealthStore(root));
            await reloaded.RecordAsync(
                [new DownloadSourceObservation(source, 0, 3)]);
            var afterFailure = (await reloaded.GetScoresAsync([source]))[source];

            Assert.True(afterFailure.Score < afterSuccess.Score);
            Assert.Equal(3, afterFailure.FailureCount);
            Assert.Equal(0, afterFailure.ConsecutiveSuccesses);
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
