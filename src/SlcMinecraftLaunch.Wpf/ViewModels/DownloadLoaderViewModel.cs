using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SlcMinecraftLaunch.Domain.Abstractions;
using SlcMinecraftLaunch.Domain.Models;

namespace SlcMinecraftLaunch.Wpf.ViewModels;

public sealed partial class DownloadLoaderViewModel(
    ILoaderCatalogService loaderCatalog,
    ILoaderInstallationService loaderInstallation,
    IDownloadService downloadService,
    IJavaRuntimeService javaRuntimeService,
    IVanillaVersionCatalogService vanillaVersionCatalog,
    IVanillaInstallationService vanillaInstallation) : PageViewModel
{
    public override string TitleKey => "Download.Title";
    public override string DescriptionKey => "Download.Description";

    private IReadOnlyList<MinecraftReleaseDescriptor> _allCatalogVersions =
        Array.Empty<MinecraftReleaseDescriptor>();

    public ObservableCollection<MinecraftReleaseDescriptor> CatalogVersions { get; } = [];
    public ObservableCollection<LoaderVersionOption> LoaderVersions { get; } = [];
    public IReadOnlyList<LoaderKind> LoaderKinds { get; } =
        [LoaderKind.Fabric, LoaderKind.Forge, LoaderKind.NeoForge, LoaderKind.OptiFine, LoaderKind.LiteLoader];

    [ObservableProperty]
    private string? _selectedMinecraftVersion;

    [ObservableProperty]
    private MinecraftReleaseDescriptor? _selectedCatalogVersion;

    [ObservableProperty]
    private bool _showSnapshots;

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

    partial void OnShowSnapshotsChanged(bool value)
    {
        ApplyCatalogFilter();
    }

    partial void OnSelectedCatalogVersionChanged(MinecraftReleaseDescriptor? value)
    {
        SelectedMinecraftVersion = value?.Id;
    }

    [RelayCommand(IncludeCancelCommand = true)]
    private Task LoadAsync(CancellationToken cancellationToken)
    {
        return RunAsync(async token =>
        {
            _allCatalogVersions = await vanillaVersionCatalog
                .GetVersionsAsync(token)
                .ConfigureAwait(true);
            ApplyCatalogFilter();
        }, cancellationToken);
    }

    [RelayCommand(IncludeCancelCommand = true)]
    private Task InstallVanillaAsync(CancellationToken cancellationToken)
    {
        return RunAsync(async token =>
        {
            if (SelectedCatalogVersion is null)
            {
                throw new UiOperationException("Download.SelectVersionRequired");
            }

            var result = await vanillaInstallation.InstallAsync(
                SelectedCatalogVersion.Id,
                CreateProgress(),
                token);
            InstalledVersionId = result.VersionId;
            StatusKey = result.MessageKey;
            ErrorDetail = result.ErrorDetail;
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

            if (SelectedLoaderKind == LoaderKind.LiteLoader)
            {
                LoaderVersions.Clear();
                SelectedLoaderVersion = null;
                StatusKey = "Download.LiteLoaderUnsupported";
                return;
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

    private void ApplyCatalogFilter()
    {
        var selectedId = SelectedCatalogVersion?.Id;
        CatalogVersions.Clear();
        foreach (var version in _allCatalogVersions.Where(version =>
                     version.Type == "release" || ShowSnapshots))
        {
            CatalogVersions.Add(version);
        }

        SelectedCatalogVersion = CatalogVersions.FirstOrDefault(version =>
                                    string.Equals(version.Id, selectedId, StringComparison.OrdinalIgnoreCase))
                                ?? CatalogVersions.FirstOrDefault(version => version.IsLatestRelease)
                                ?? CatalogVersions.FirstOrDefault();
    }
}

