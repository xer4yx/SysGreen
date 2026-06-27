namespace SysGreen.App.Services;

/// <summary>Outcome of an update check: whether a newer version is available, and which.</summary>
public sealed record UpdateCheckResult(bool Available, string? Version);

/// <summary>
/// Self-update (Velopack — ADR-0009). The Velopack-backed implementation is a humble adapter; the
/// view-model depends on this interface so the "update available" UX stays unit-testable.
/// </summary>
public interface IUpdateService
{
    Task<UpdateCheckResult> CheckForUpdateAsync();
    Task ApplyAndRestartAsync();
}
