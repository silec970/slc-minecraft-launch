using System.IO;
using System.Windows.Media;
using SlcMinecraftLaunch.Domain.Models;
using SlcMinecraftLaunch.Infrastructure.Configuration;

namespace SlcMinecraftLaunch.Wpf.Services;

public sealed class BackgroundMusicService(PortableLauncherPaths paths) : IAsyncDisposable
{
    private static readonly string[] SupportedExtensions = [".mp3", ".wav", ".m4a", ".wma"];
    private readonly MediaPlayer _player = new();
    private string? _currentTrack;

    public Task ApplyAsync(
        UiSettings settings,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!settings.MusicEnabled)
        {
            _player.Stop();
            return Task.CompletedTask;
        }

        var musicDirectory = Path.Combine(paths.DataRoot, "music");
        var track = Directory.Exists(musicDirectory)
            ? Directory.EnumerateFiles(musicDirectory)
                .FirstOrDefault(path => SupportedExtensions.Contains(
                    Path.GetExtension(path),
                    StringComparer.OrdinalIgnoreCase))
            : null;
        if (track is null)
        {
            _player.Stop();
            return Task.CompletedTask;
        }

        _player.Volume = Math.Clamp(settings.MusicVolume, 0, 1);
        if (!string.Equals(_currentTrack, track, StringComparison.OrdinalIgnoreCase))
        {
            _currentTrack = track;
            _player.Open(new Uri(track));
            _player.MediaEnded += OnMediaEnded;
        }

        _player.Play();
        return Task.CompletedTask;
    }

    private void OnMediaEnded(object? sender, EventArgs e)
    {
        _player.Position = TimeSpan.Zero;
        _player.Play();
    }

    public ValueTask DisposeAsync()
    {
        _player.MediaEnded -= OnMediaEnded;
        _player.Stop();
        _player.Close();
        return ValueTask.CompletedTask;
    }
}

