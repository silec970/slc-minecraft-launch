using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SlcMinecraftLaunch.Domain.Abstractions;
using SlcMinecraftLaunch.Domain.Models;

namespace SlcMinecraftLaunch.Wpf.ViewModels;

public sealed partial class VersionsViewModel(
    IVersionService versionService,
    IInstanceService instanceService,
    IInstanceTransferService instanceTransferService) : PageViewModel
{
    private static readonly LoaderKind[] GroupOrder =
    [
        LoaderKind.Vanilla,
        LoaderKind.NeoForge,
        LoaderKind.Forge,
        LoaderKind.Fabric,
        LoaderKind.Quilt,
        LoaderKind.OptiFine,
        LoaderKind.LiteLoader,
        LoaderKind.Other
    ];

    public override string TitleKey => "Versions.Title";
    public override string DescriptionKey => "Versions.Description";

    public ObservableCollection<InstanceGroupItem> Groups { get; } = [];
    public IReadOnlyList<LoaderKind> LoaderKinds { get; } =
    [
        LoaderKind.Vanilla,
        LoaderKind.Forge,
        LoaderKind.NeoForge,
        LoaderKind.Fabric,
        LoaderKind.Quilt,
        LoaderKind.OptiFine,
        LoaderKind.LiteLoader
    ];

    [ObservableProperty]
    private InstanceRowItem? _selectedInstance;

    [ObservableProperty]
    private string _newInstanceId = string.Empty;

    [ObservableProperty]
    private string _newInstanceName = string.Empty;

    [ObservableProperty]
    private string _newMinecraftVersion = "1.21.4";

    [ObservableProperty]
    private LoaderKind _newLoaderKind = LoaderKind.Vanilla;

    [ObservableProperty]
    private string _newLoaderVersion = string.Empty;

    [ObservableProperty]
    private string _editName = string.Empty;

    [ObservableProperty]
    private int _minimumMemoryMb = 1024;

    [ObservableProperty]
    private int _maximumMemoryMb = 4096;

    [ObservableProperty]
    private int? _suggestedJavaMajor;

    [ObservableProperty]
    private string _javaExecutablePath = string.Empty;

    [ObservableProperty]
    private string _jvmArgumentsText = string.Empty;

    [ObservableProperty]
    private string _gameArgumentsText = string.Empty;

    [ObservableProperty]
    private int _windowWidth = 854;

    [ObservableProperty]
    private int _windowHeight = 480;

    [ObservableProperty]
    private bool _fullscreen;

    [ObservableProperty]
    private string _copyTargetId = string.Empty;

    [ObservableProperty]
    private string _copyTargetName = string.Empty;

    [ObservableProperty]
    private bool _includeSaves;

    [ObservableProperty]
    private bool _includeScreenshots;

    [ObservableProperty]
    private bool _includeServersFile;

    [ObservableProperty]
    private bool _includeLogs;

    public bool HasSelectedInstance => SelectedInstance is not null;
    public bool IsSelectedPlaceholder => SelectedInstance?.IsPlaceholder == true;

    partial void OnSelectedInstanceChanged(InstanceRowItem? value)
    {
        OnPropertyChanged(nameof(HasSelectedInstance));
        OnPropertyChanged(nameof(IsSelectedPlaceholder));
        if (value is null)
        {
            return;
        }

        EditName = value.Name;
        MinimumMemoryMb = value.Settings.MinMemoryMb;
        MaximumMemoryMb = value.Settings.MaxMemoryMb;
        SuggestedJavaMajor = value.Settings.SuggestedJavaMajor;
        JavaExecutablePath = value.Settings.JavaExecutablePath ?? string.Empty;
        JvmArgumentsText = string.Join(Environment.NewLine, value.Settings.JvmArguments ?? []);
        GameArgumentsText = string.Join(Environment.NewLine, value.Settings.GameArguments ?? []);
        WindowWidth = value.Settings.WindowWidth;
        WindowHeight = value.Settings.WindowHeight;
        Fullscreen = value.Settings.Fullscreen;
        CopyTargetId = $"{value.Id}-copy";
        CopyTargetName = $"{value.Name} - Copy";
    }

    [RelayCommand(IncludeCancelCommand = true)]
    private Task LoadAsync(CancellationToken cancellationToken)
    {
        return RunAsync(async token =>
        {
            var selectedId = SelectedInstance?.Id;
            var instances = await instanceService.GetInstancesAsync(token);
            Groups.Clear();
            foreach (var group in GroupOrder
                         .Select(loaderKind => new InstanceGroupItem(
                             loaderKind,
                             $"VersionGroup.{loaderKind}",
                             instances
                                 .Where(instance => instance.LoaderKind == loaderKind)
                                 .OrderByDescending(instance => instance.IsFavorite)
                                 .ThenBy(instance => instance.MinecraftVersion, StringComparer.OrdinalIgnoreCase)
                                 .Select(instance => new InstanceRowItem(
                                     instance.Id,
                                     instance.Name,
                                     instance.MinecraftVersion,
                                     instance.LoaderKind,
                                     instance.LoaderVersion,
                                     instance.IsFavorite,
                                     instance.IsIsolated,
                                     instance.IsPlaceholder,
                                     instance.LaunchSettings))
                                 .ToArray()))
                         .Where(group => group.Instances.Count > 0))
            {
                Groups.Add(group);
            }

            SelectedInstance = Groups
                .SelectMany(group => group.Instances)
                .FirstOrDefault(instance =>
                    string.Equals(instance.Id, selectedId, StringComparison.OrdinalIgnoreCase))
                ?? Groups.SelectMany(group => group.Instances).FirstOrDefault();
        }, cancellationToken);
    }

    [RelayCommand(IncludeCancelCommand = true)]
    private Task CreateEmptyAsync(CancellationToken cancellationToken)
    {
        return RunAsync(async token =>
        {
            var result = await instanceService.CreateEmptyAsync(
                new NewInstanceRequest(
                    NewInstanceId,
                    NewInstanceName,
                    NewMinecraftVersion,
                    NewLoaderKind,
                    string.IsNullOrWhiteSpace(NewLoaderVersion) ? null : NewLoaderVersion),
                token);
            ApplyOutcome(result);
            if (result.Succeeded)
            {
                await LoadAsync(token);
            }
        }, cancellationToken);
    }

    [RelayCommand]
    private void SelectInstance(InstanceRowItem? instance)
    {
        SelectedInstance = instance;
    }

    [RelayCommand(IncludeCancelCommand = true)]
    private Task SaveSelectedAsync(CancellationToken cancellationToken)
    {
        return RunAsync(async token =>
        {
            if (SelectedInstance is null)
            {
                throw new UiOperationException("Instances.SelectRequired");
            }

            var manifest = await instanceService
                .GetManifestAsync(SelectedInstance.Id, token)
                ?? new InstanceManifest(
                    SelectedInstance.Id,
                    SelectedInstance.Name,
                    SelectedInstance.MinecraftVersion,
                    SelectedInstance.LoaderKind,
                    SelectedInstance.LoaderVersion,
                    SelectedInstance.Settings,
                    SelectedInstance.IsPlaceholder,
                    DateTimeOffset.UtcNow,
                    DateTimeOffset.UtcNow);
            var result = await instanceService.SaveAsync(
                manifest with
                {
                    Name = EditName,
                    LaunchSettings = new InstanceLaunchSettings(
                        MinimumMemoryMb,
                        MaximumMemoryMb,
                        SuggestedJavaMajor,
                        string.IsNullOrWhiteSpace(JavaExecutablePath) ? null : JavaExecutablePath,
                        ParseArgumentLines(JvmArgumentsText),
                        ParseArgumentLines(GameArgumentsText),
                        WindowWidth,
                        WindowHeight,
                        Fullscreen)
                },
                token);
            ApplyOutcome(result);
            if (result.Succeeded)
            {
                await LoadAsync(token);
            }
        }, cancellationToken);
    }

    [RelayCommand(IncludeCancelCommand = true)]
    private Task ToggleFavoriteAsync(InstanceRowItem? row, CancellationToken cancellationToken)
    {
        return row is null
            ? Task.CompletedTask
            : ToggleFavoriteCoreAsync(row, cancellationToken);
    }

    private Task ToggleFavoriteCoreAsync(
        InstanceRowItem row,
        CancellationToken cancellationToken)
    {
        return RunAsync(async token =>
        {
            await versionService.SetFavoriteAsync(row.Id, !row.IsFavorite, token);
            await LoadAsync(token);
        }, cancellationToken);
    }

    [RelayCommand(IncludeCancelCommand = true)]
    private Task ToggleIsolationAsync(InstanceRowItem? row, CancellationToken cancellationToken)
    {
        return row is null
            ? Task.CompletedTask
            : ToggleIsolationCoreAsync(row, cancellationToken);
    }

    private Task ToggleIsolationCoreAsync(
        InstanceRowItem row,
        CancellationToken cancellationToken)
    {
        return RunAsync(async token =>
        {
            await versionService.SetIsolationAsync(row.Id, !row.IsIsolated, token);
            await LoadAsync(token);
        }, cancellationToken);
    }

    [RelayCommand(IncludeCancelCommand = true)]
    private Task CopySelectedAsync(CancellationToken cancellationToken)
    {
        return RunAsync(async token =>
        {
            if (SelectedInstance is null)
            {
                throw new UiOperationException("Instances.SelectRequired");
            }

            var result = await instanceService.CopyAsync(
                SelectedInstance.Id,
                CopyTargetId,
                CopyTargetName,
                token);
            ApplyOutcome(result);
            if (result.Succeeded)
            {
                await LoadAsync(token);
            }
        }, cancellationToken);
    }

    [RelayCommand(IncludeCancelCommand = true)]
    private Task DeleteSelectedAsync(CancellationToken cancellationToken)
    {
        return RunAsync(async token =>
        {
            if (SelectedInstance is null)
            {
                throw new UiOperationException("Instances.SelectRequired");
            }

            var result = await instanceService.DeleteAsync(SelectedInstance.Id, token);
            ApplyOutcome(result);
            SelectedInstance = null;
            await LoadAsync(token);
        }, cancellationToken);
    }

    [RelayCommand(IncludeCancelCommand = true)]
    private Task ExportSelectedAsync(string? path, CancellationToken cancellationToken)
    {
        return RunAsync(async token =>
        {
            if (SelectedInstance is null)
            {
                throw new UiOperationException("Instances.SelectRequired");
            }

            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            ApplyOutcome(await instanceTransferService.ExportAsync(
                SelectedInstance.Id,
                path,
                new InstanceExportOptions(
                    IncludeSaves,
                    IncludeScreenshots,
                    IncludeServersFile,
                    IncludeLogs),
                token));
        }, cancellationToken);
    }

    [RelayCommand(IncludeCancelCommand = true)]
    private Task SaveBackupAsync(string? path, CancellationToken cancellationToken)
    {
        return RunAsync(async token =>
        {
            if (SelectedInstance is null)
            {
                throw new UiOperationException("Instances.SelectRequired");
            }

            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            ApplyOutcome(await instanceTransferService.SaveBackupAsync(
                SelectedInstance.Id,
                path,
                token));
        }, cancellationToken);
    }

    [RelayCommand(IncludeCancelCommand = true)]
    private Task ImportAsync(string? path, CancellationToken cancellationToken)
    {
        return RunAsync(async token =>
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            var result = await instanceTransferService.ImportAsync(path, token);
            ApplyOutcome(result);
            if (result.Succeeded)
            {
                await LoadAsync(token);
            }
        }, cancellationToken);
    }

    public override Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        return LoadAsync(cancellationToken);
    }

    private void ApplyOutcome(InstanceOperationOutcome outcome)
    {
        StatusKey = outcome.MessageKey;
        ErrorDetail = outcome.ErrorDetail;
    }

    private static IReadOnlyList<string> ParseArgumentLines(string value)
    {
        return value.Split(
                ['\r', '\n'],
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(line => line.Length > 0)
            .ToArray();
    }
}

public sealed record InstanceGroupItem(
    LoaderKind LoaderKind,
    string NameKey,
    IReadOnlyList<InstanceRowItem> Instances);

public sealed record InstanceRowItem(
    string Id,
    string Name,
    string MinecraftVersion,
    LoaderKind LoaderKind,
    string? LoaderVersion,
    bool IsFavorite,
    bool IsIsolated,
    bool IsPlaceholder,
    InstanceLaunchSettings Settings)
{
    public string FavoriteGlyph => IsFavorite ? "\uE735" : "\uE734";
    public string IsolationGlyph => IsIsolated ? "\uE73E" : "\uE739";
    public string LoaderVersionText => string.IsNullOrWhiteSpace(LoaderVersion) ? "-" : LoaderVersion;
    public string PlaceholderTextKey => IsPlaceholder ? "Instances.Placeholder" : "Instances.Ready";
}
