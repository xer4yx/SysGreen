using Velopack;
using Velopack.Sources;

namespace SysGreen.App.Services;

/// <summary>
/// <see cref="IUpdateService"/> backed by Velopack, reading releases from GitHub. Fails safe to "no
/// update" when the app is not a Velopack install (dev runs, or a non-Velopack installer) so the
/// check never throws — Velopack's <see cref="UpdateManager"/> requires a Velopack-managed install.
/// </summary>
public sealed class VelopackUpdateService : IUpdateService
{
    private readonly UpdateManager _manager;
    private UpdateInfo? _pending;

    public VelopackUpdateService(string repositoryUrl)
        => _manager = new UpdateManager(new GithubSource(repositoryUrl, null, prerelease: false));

    public async Task<UpdateCheckResult> CheckForUpdateAsync()
    {
        if (!_manager.IsInstalled) return new UpdateCheckResult(false, null);

        _pending = await _manager.CheckForUpdatesAsync();
        return _pending is null
            ? new UpdateCheckResult(false, null)
            : new UpdateCheckResult(true, _pending.TargetFullRelease.Version.ToString());
    }

    public async Task ApplyAndRestartAsync()
    {
        if (_pending is null) return;
        await _manager.DownloadUpdatesAsync(_pending);
        _manager.ApplyUpdatesAndRestart(_pending);
    }
}
