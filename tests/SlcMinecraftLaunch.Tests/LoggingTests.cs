using System.IO.Compression;
using SlcMinecraftLaunch.Domain.Abstractions;
using SlcMinecraftLaunch.Domain.Models;
using SlcMinecraftLaunch.Infrastructure.Logging;

namespace SlcMinecraftLaunch.Tests;

public sealed class LoggingTests
{
    [Fact]
    public void Redact_RemovesAuthorizationTokensAndApiKeys()
    {
        var input =
            "Authorization: Bearer abc.def.ghi\nauthorization=token-value\n" +
            "accessToken=access-secret\nrefresh_token=refresh-secret\n" +
            "x-api-key: cf-secret\ncurseForgeApiKey=curse-secret";

        var output = LogRedactor.Redact(input);

        Assert.DoesNotContain("abc.def.ghi", output);
        Assert.DoesNotContain("access-secret", output);
        Assert.DoesNotContain("refresh-secret", output);
        Assert.DoesNotContain("cf-secret", output);
        Assert.DoesNotContain("curse-secret", output);
        Assert.Contains("[REDACTED]", output);
    }

    [Fact]
    public void SerilogLogger_WritesRedactedRollingLog()
    {
        var root = Path.Combine(Path.GetTempPath(), $"slc-serilog-{Guid.NewGuid():N}");
        try
        {
            using (var logger = SerilogAppLogger.Create(root))
            {
                logger.Error(
                    "request failed with accessToken=access-secret",
                    new InvalidOperationException("x-api-key: cf-secret"));
            }

            var logPath = Assert.Single(Directory.GetFiles(
                Path.Combine(root, "logs"),
                "slc-*.log"));
            var content = File.ReadAllText(logPath);
            Assert.DoesNotContain("access-secret", content);
            Assert.DoesNotContain("cf-secret", content);
            Assert.Contains("[REDACTED]", content);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }
    }

    [Fact]
    public async Task DiagnosticBundle_ContainsSafeLogsInstancesAndJavaOnly()
    {
        var root = Path.Combine(Path.GetTempPath(), $"slc-diagnostics-{Guid.NewGuid():N}");
        try
        {
            var gameRoot = Path.Combine(root, "game");
            var dataRoot = Path.Combine(root, "launcher-data");
            var logDirectory = Path.Combine(dataRoot, "logs");
            Directory.CreateDirectory(logDirectory);
            await File.WriteAllTextAsync(
                Path.Combine(logDirectory, "slc-test.log"),
                "accessToken=access-secret\nnormal line");
            Directory.CreateDirectory(Path.Combine(gameRoot, "versions", "test"));
            await File.WriteAllTextAsync(
                Path.Combine(gameRoot, "versions", "test", "instance.json"),
                """{"id":"test","curseForgeApiKey":"curse-secret"}""");
            var outputPath = Path.Combine(root, "diagnostic.zip");
            var service = new DiagnosticBundleService(
                gameRoot,
                dataRoot,
                new FakeJavaDiscovery());

            await service.CreateAsync(outputPath);

            using var archive = ZipFile.OpenRead(outputPath);
            var names = archive.Entries.Select(entry => entry.FullName).ToArray();
            Assert.Contains("logs/slc-test.log", names);
            Assert.Contains("instances/test/instance.json", names);
            Assert.Contains("java-runtimes.json", names);
            Assert.Contains("diagnostic-manifest.json", names);
            Assert.DoesNotContain(
                names,
                name => name.Contains("credential", StringComparison.OrdinalIgnoreCase) ||
                        name.Contains("account", StringComparison.OrdinalIgnoreCase));
            foreach (var entry in archive.Entries.Where(entry => entry.Name.EndsWith(".json") ||
                                                                 entry.Name.EndsWith(".log")))
            {
                using var reader = new StreamReader(entry.Open());
                var content = await reader.ReadToEndAsync();
                Assert.DoesNotContain("access-secret", content);
                Assert.DoesNotContain("curse-secret", content);
            }
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }
    }

    private sealed class FakeJavaDiscovery : IJavaDiscovery
    {
        public Task<IReadOnlyList<JavaCandidate>> DiscoverAsync(
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<JavaCandidate>>(
                [new JavaCandidate(@"C:\Java\21\java.exe", 21)]);

        public Task<JavaCandidate?> InspectAsync(
            string executablePath,
            CancellationToken cancellationToken = default)
            => Task.FromResult<JavaCandidate?>(null);
    }
}
