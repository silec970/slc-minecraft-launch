using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SlcMinecraftLaunch.Domain.Abstractions;
using SlcMinecraftLaunch.Domain.Models;

namespace SlcMinecraftLaunch.Wpf.ViewModels;

public sealed partial class ModsViewModel(
    IVersionService versionService,
    IModService modService) : PageViewModel
{
    public override string TitleKey => "Mods.Title";
    public override string DescriptionKey => "Mods.Description";

    public ObservableCollection<string> Versions { get; } = [];
    public ObservableCollection<ModSearchItem> SearchResults { get; } = [];
    public ObservableCollection<ModEntry> InstalledMods { get; } = [];
    public ObservableCollection<ModConflict> Conflicts { get; } = [];
    public IReadOnlyList<LoaderKind> LoaderKinds { get; } =
        [LoaderKind.Fabric, LoaderKind.Forge, LoaderKind.NeoForge, LoaderKind.Quilt];

    [ObservableProperty]
    private string? _selectedVersion;

    [ObservableProperty]
    private LoaderKind _selectedLoaderKind = LoaderKind.Fabric;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private ModSearchItem? _selectedSearchResult;

    [ObservableProperty]
    private string? _warningDetail;

    [RelayCommand(IncludeCancelCommand = true)]
    private Task LoadAsync(CancellationToken cancellationToken)
    {
        return RunAsync(async token =>
        {
            var catalog = await versionService.GetCatalogAsync(token);
            Versions.Clear();
            foreach (var version in catalog.AllVersions.Select(entry => entry.Version.Id))
            {
                Versions.Add(version);
            }

            SelectedVersion ??= Versions.FirstOrDefault();
            if (SelectedVersion is not null)
            {
                await RefreshInstalledCoreAsync(SelectedVersion, token);
            }
        }, cancellationToken);
    }

    [RelayCommand(IncludeCancelCommand = true)]
    private Task SearchAsync(CancellationToken cancellationToken)
    {
        return RunAsync(async token =>
        {
            if (string.IsNullOrWhiteSpace(SelectedVersion))
            {
                throw new UiOperationException("Mods.SelectInstance");
            }

            var outcome = await modService.SearchAsync(
                new ModSearchQuery(SearchText, SelectedVersion, SelectedLoaderKind),
                token);
            SearchResults.Clear();
            foreach (var item in outcome.Items)
            {
                SearchResults.Add(item);
            }

            if (outcome.Warnings.Count > 0)
            {
                ErrorDetail = string.Join(Environment.NewLine, outcome.Warnings);
            }
        }, cancellationToken);
    }

    [RelayCommand(IncludeCancelCommand = true)]
    private Task InstallSelectedAsync(CancellationToken cancellationToken)
    {
        return RunAsync(async token =>
        {
            if (SelectedVersion is null || SelectedSearchResult is null)
            {
                throw new UiOperationException("Mods.SelectSearchResult");
            }

            var result = await modService.InstallRemoteAsync(
                new ModInstallRequest(
                    SelectedSearchResult.Provider,
                    SelectedSearchResult.ProjectId,
                    SelectedVersion,
                    SelectedLoaderKind),
                CreateProgress(),
                token);
            StatusKey = result.MessageKey;
            ErrorDetail = result.ErrorDetail;
            WarningDetail = result.Warnings is { Count: > 0 }
                ? string.Join(Environment.NewLine, result.Warnings)
                : null;
            await RefreshInstalledCoreAsync(SelectedVersion, token);
        }, cancellationToken);
    }

    [RelayCommand(IncludeCancelCommand = true)]
    private Task RefreshInstalledAsync(CancellationToken cancellationToken)
    {
        return RunAsync(async token =>
        {
            if (SelectedVersion is not null)
            {
                await RefreshInstalledCoreAsync(SelectedVersion, token);
            }
        }, cancellationToken);
    }

    public async Task InstallDroppedFilesAsync(
        IReadOnlyList<string> paths,
        CancellationToken cancellationToken = default)
    {
        if (SelectedVersion is null || IsBusy)
        {
            return;
        }

        await RunAsync(async token =>
        {
            foreach (var path in paths.Where(path =>
                         string.Equals(Path.GetExtension(path), ".jar", StringComparison.OrdinalIgnoreCase)))
            {
                var result = await modService.InstallLocalAsync(
                    new LocalModInstallRequest(path, SelectedVersion, SelectedLoaderKind),
                    token);
                StatusKey = result.MessageKey;
                ErrorDetail = result.ErrorDetail;
                WarningDetail = result.Warnings is { Count: > 0 }
                    ? string.Join(Environment.NewLine, result.Warnings)
                    : null;
            }

            await RefreshInstalledCoreAsync(SelectedVersion, token);
        }, cancellationToken);
    }

    private async Task RefreshInstalledCoreAsync(string versionId, CancellationToken cancellationToken)
    {
        var mods = await modService.ScanAsync(versionId, cancellationToken);
        var conflicts = await modService.DetectConflictsAsync(versionId, cancellationToken);
        InstalledMods.Clear();
        Conflicts.Clear();
        foreach (var mod in mods)
        {
            InstalledMods.Add(mod);
        }

        foreach (var conflict in conflicts)
        {
            Conflicts.Add(conflict);
        }
    }

    public override Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        return LoadAsync(cancellationToken);
    }
}

