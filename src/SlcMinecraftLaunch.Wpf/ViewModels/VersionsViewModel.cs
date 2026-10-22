using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using SlcMinecraftLaunch.Domain.Abstractions;

namespace SlcMinecraftLaunch.Wpf.ViewModels;

public sealed partial class VersionsViewModel(IVersionService versionService) : PageViewModel
{
    public override string TitleKey => "Versions.Title";
    public override string DescriptionKey => "Versions.Description";

    public ObservableCollection<VersionGroupItem> Groups { get; } = [];

    [RelayCommand(IncludeCancelCommand = true)]
    private Task LoadAsync(CancellationToken cancellationToken)
    {
        return RunAsync(async token =>
        {
            var catalog = await versionService.GetCatalogAsync(token);
            Groups.Clear();
            foreach (var group in catalog.Groups)
            {
                Groups.Add(new VersionGroupItem(
                    group.NameKey,
                    group.Versions.Select(entry => new VersionRowItem(
                        entry.Version.Id,
                        entry.Version.DisplayName,
                        entry.Version.BaseVersion,
                        entry.Version.JavaMajorVersion?.ToString() ?? "-",
                        entry.IsFavorite,
                        entry.IsIsolated)).ToArray()));
            }
        }, cancellationToken);
    }

    [RelayCommand(IncludeCancelCommand = true)]
    private async Task ToggleFavoriteAsync(VersionRowItem? row, CancellationToken cancellationToken)
    {
        if (row is null)
        {
            return;
        }

        await versionService.SetFavoriteAsync(row.Id, !row.IsFavorite, cancellationToken);
        await RefreshAsync(cancellationToken);
    }

    [RelayCommand(IncludeCancelCommand = true)]
    private async Task ToggleIsolationAsync(VersionRowItem? row, CancellationToken cancellationToken)
    {
        if (row is null)
        {
            return;
        }

        await versionService.SetIsolationAsync(row.Id, !row.IsIsolated, cancellationToken);
        await RefreshAsync(cancellationToken);
    }

    public override Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        return LoadAsync(cancellationToken);
    }
}

public sealed record VersionGroupItem(string NameKey, IReadOnlyList<VersionRowItem> Versions);

public sealed record VersionRowItem(
    string Id,
    string DisplayName,
    string BaseVersion,
    string JavaMajor,
    bool IsFavorite,
    bool IsIsolated)
{
    public string FavoriteGlyph => IsFavorite ? "\uE735" : "\uE734";
    public string IsolationGlyph => IsIsolated ? "\uE73E" : "\uE739";
}

