using SlcMinecraftLaunch.Domain.Models;
using SlcMinecraftLaunch.Infrastructure.Persistence;

namespace SlcMinecraftLaunch.Tests;

public sealed class JsonCrashReportStoreTests
{
    [Fact]
    public async Task History_LoadsFiltersDeletesAndRetainsThirtyDayPolicy()
    {
        var root = Path.Combine(Path.GetTempPath(), $"slc-crash-history-{Guid.NewGuid():N}");
        try
        {
            var store = new JsonCrashReportStore(root);
            var oldReport = CreateReport("old", DateTimeOffset.Now.AddDays(-31));
            var newReport = CreateReport("new", DateTimeOffset.Now);
            await store.SaveAsync(oldReport);
            await store.SaveAsync(newReport);

            var history = await store.LoadHistoryAsync();

            Assert.Equal(["new", "old"], history.Select(item => item.Report.ReportId));
            Assert.Equal(2, (await store.LoadHistoryAsync("1.21.4")).Count);
            Assert.Empty(await store.LoadHistoryAsync("missing"));

            var cleanup = await store.CleanupExpiredAsync(DateTimeOffset.Now.AddDays(-30));
            Assert.Equal(1, cleanup);
            Assert.Equal(["new"], (await store.LoadHistoryAsync()).Select(item => item.Report.ReportId));

            Assert.True(await store.DeleteAsync("new"));
            Assert.Empty(await store.LoadHistoryAsync());
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }
    }

    private static CrashAnalysisReport CreateReport(string id, DateTimeOffset analyzedAt)
    {
        return new CrashAnalysisReport(
            "1.21.4",
            analyzedAt,
            1,
            [
                new CrashInsight(
                    "ModCausedGameCrash",
                    CrashCategory.Mod,
                    CrashSeverity.Error,
                    "Crash.Category.Mod.Title",
                    "Crash.Category.Mod.Suggestion")
            ],
            id,
            @"C:\Minecraft\logs\latest.log");
    }
}
