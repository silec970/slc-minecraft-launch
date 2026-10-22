using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SlcMinecraftLaunch.Domain.Abstractions;
using SlcMinecraftLaunch.Domain.Models;

namespace SlcMinecraftLaunch.Wpf.ViewModels;

public sealed partial class DownloadLoaderViewModel(
    IVersionService versionService,
    ILoaderCatalogService loaderCatalog,
    ILoaderInstallationService loaderInstallation,
    IDownloadService downloadService,
    IJavaRuntimeService javaRuntimeService) : PageViewModel
{
    public override string TitleKey => "Download.Title";
    public override string DescriptionKey => "Download.Description";

    public ObservableCollection<string> MinecraftVersions { get; } = [];
    public ObservableCollection<LoaderVersionOption> LoaderVersions { get; } = [];
    public IReadOnlyList<LoaderKind> LoaderKinds { get; } =
        [LoaderKind.Fabric, LoaderKind.Forge, LoaderKind.NeoForge, LoaderKind.OptiFine];

    [ObservableProperty]
    private string? _selectedMinecraftVersion;

    [ObservableProperty]
    private LoaderKind _selectedLoaderKind = LoaderKind.Fabric;

    [ObservableProperty]
    private LoaderVersionOption? _selectedLoaderVersion;

    [ObservableProperty]
    private string? _javaPath;

    [ObservableProperty]
    private string? _installedVersionId;

    [ObservableProperty]
    private bool _canCompleteResources;

    [RelayCommand(IncludeCancelCommand = true)]
    private Task LoadAsync(CancellationToken cancellationToken)
    {
        return RunAsync(async token =>
        {
            var catalog = await versionService.GetCatalogAsync(token);
            var versions = catalog.AllVersions
                .Select(entry => entry.Version.BaseVersion)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderByDescending(version => version, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            MinecraftVersions.Clear();
            foreach (var version in versions)
            {
                MinecraftVersions.Add(version);
            }

            SelectedMinecraftVersion ??= MinecraftVersions.FirstOrDefault();
        }, cancellationToken);
    }

    [RelayCommand(IncludeCancelCommand = true)]
    private Task ResolveLoaderVersionsAsync(CancellationToken cancellationToken)
    {
        return RunAsync(async token =>
        {
            if (string.IsNullOrWhiteSpace(SelectedMinecraftVersion))
            {
                throw new UiOperationException("Download.SelectVersionRequired");
            }

            var versions = await loaderCatalog.GetVersionsAsync(
                SelectedMinecraftVersion,
                SelectedLoaderKind,
                token);
            LoaderVersions.Clear();
            foreach (var version in versions)
            {
                LoaderVersions.Add(version);
            }

            SelectedLoaderVersion = LoaderVersions.FirstOrDefault(version => version.IsStable)
                                    ?? LoaderVersions.FirstOrDefault();
        }, cancellationToken);
    }

    [RelayCommand(IncludeCancelCommand = true)]
    private Task InstallLoaderAsync(CancellationToken cancellationToken)
    {
        return RunAsync(async token =>
        {
            if (string.IsNullOrWhiteSpace(SelectedMinecraftVersion))
            {
                throw new UiOperationException("Download.SelectVersionRequired");
            }

            var result = await loaderInstallation.InstallAsync(
                new LoaderInstallRequest(
                    SelectedMinecraftVersion,
                    SelectedLoaderKind,
                    SelectedLoaderVersion?.Version,
                    JavaPath),
                CreateProgress(),
                token);
            InstalledVersionId = result.InstalledVersionId;
            CanCompleteResources = result.Succeeded;
            StatusKey = result.MessageKey;
            ErrorDetail = result.ErrorDetail;
        }, cancellationToken);
    }

    [RelayCommand(IncludeCancelCommand = true)]
    private Task CompleteResourcesAsync(CancellationToken cancellationToken)
    {
        return RunAsync(async token =>
        {
            if (string.IsNullOrWhiteSpace(InstalledVersionId))
            {
                throw new UiOperationException("Download.InstallLoaderRequired");
            }

            var java = await javaRuntimeService.EnsureAsync(
                InstalledVersionId,
                SelectedLoaderKind,
                JavaPath,
                CreateProgress(),
                token);
            if (java.Selected is null)
            {
                throw new InvalidOperationException(java.MessageKey);
            }

            var result = await downloadService.CompleteResourcesAsync(
                new ResourceCompletionRequest(InstalledVersionId, java.Selected.ExecutablePath),
                CreateProgress(),
                token);
            StatusKey = result.MessageKey;
            ErrorDetail = result.ErrorDetail;
        }, cancellationToken);
    }

    public override Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        return LoadAsync(cancellationToken);
    }
}

