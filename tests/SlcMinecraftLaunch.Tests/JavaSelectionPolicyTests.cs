using SlcMinecraftLaunch.Application;
using SlcMinecraftLaunch.Domain.Models;

namespace SlcMinecraftLaunch.Tests;

public sealed class JavaSelectionPolicyTests
{
    private readonly JavaSelectionPolicy _policy = new();

    [Fact]
    public void Select_PrefersExactMajorVersion()
    {
        var result = _policy.Select(
            [
                new JavaCandidate(@"C:\Java\17\bin\java.exe", 17),
                new JavaCandidate(@"C:\Java\21\bin\java.exe", 21),
                new JavaCandidate(@"C:\Java\25\bin\java.exe", 25)
            ],
            "1.21.4",
            LoaderKind.Vanilla);

        Assert.Equal(21, result.Selected?.MajorVersion);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void Select_AllowsJava25FallbackForVanillaWithWarning()
    {
        var result = _policy.Select(
            [new JavaCandidate(@"C:\Java\25\bin\java.exe", 25)],
            "1.21.4",
            LoaderKind.Vanilla);

        Assert.Equal(25, result.Selected?.MajorVersion);
        Assert.Contains(JavaSelectionWarning.JavaAboveBaseline, result.Warnings);
    }

    [Fact]
    public void Select_RejectsJava25FallbackForForge()
    {
        var result = _policy.Select(
            [new JavaCandidate(@"C:\Java\25\bin\java.exe", 25)],
            "1.21.4",
            LoaderKind.Forge);

        Assert.Null(result.Selected);
    }

    [Fact]
    public void Select_RequiresJava25ForNewVersionSeries()
    {
        var result = _policy.Select(
            [new JavaCandidate(@"C:\Java\21\bin\java.exe", 21)],
            "26.3",
            LoaderKind.Vanilla);

        Assert.Null(result.Selected);
    }

    [Fact]
    public void Select_HonorsExplicitJavaWithWarning()
    {
        var path = @"C:\Java\25\bin\java.exe";
        var result = _policy.Select(
            [new JavaCandidate(path, 25)],
            "1.21.4",
            LoaderKind.Forge,
            path);

        Assert.Equal(25, result.Selected?.MajorVersion);
        Assert.Contains(JavaSelectionWarning.JavaAboveBaseline, result.Warnings);
        Assert.Contains(JavaSelectionWarning.UserOverride, result.Warnings);
    }

    [Fact]
    public void Select_RejectsInvalidManagedRuntime()
    {
        var result = _policy.Select(
            [
                new JavaCandidate(
                    @"C:\Minecraft\runtimes\mojang\java-runtime-delta\21\bin\java.exe",
                    21,
                    JavaRuntimeSource.Mojang,
                    JavaIntegrityStatus.Invalid)
            ],
            "1.21.4",
            LoaderKind.Vanilla);

        Assert.Null(result.Selected);
    }

    [Theory]
    [InlineData("1.12.2", 8)]
    [InlineData("1.16.5", 8)]
    [InlineData("1.17.1", 16)]
    [InlineData("1.20.4", 17)]
    [InlineData("1.20.5", 21)]
    [InlineData("1.21.4", 21)]
    [InlineData("26.3", 25)]
    public void GetRequiredJavaMajor_UsesVersionPolicy(string version, int expected)
    {
        Assert.Equal(expected, JavaSelectionPolicy.GetRequiredJavaMajor(version));
    }
}

