using SysGreen.Core.Abstractions;

namespace SysGreen.Core.Usage;

/// <summary>
/// The user's launch-tracking consent (CONTEXT.md "Tray Agent", ADR-0012): on by default, with a
/// visible off switch. When off, the Tray Agent records nothing and is not kept resident.
/// </summary>
public interface ITrackingSettings
{
    bool LaunchTrackingEnabled { get; }
    void SetLaunchTrackingEnabled(bool enabled);
}

/// <summary>Records a user app launch into the habit store (Core port; Data adapts to SQLite).</summary>
public interface ILaunchRecorder
{
    void RecordLaunch(string executablePath, DateTime whenUtc);
}

/// <summary>
/// Raises an event for every new process start (the Tray Agent's forward sampling source, ADR-0008).
/// Humble Win32/WMI seam; the decision of what to record is the tested <see cref="LaunchSampler"/>.
/// </summary>
public interface IProcessStartSource
{
    event Action<string?> ProcessStarted;
    void Start();
    void Stop();
}

/// <summary>
/// Turns raw process-start events into habit launches (CONTEXT.md "Usage"): records a launch when
/// tracking is on and the process is a user application — ignoring Windows system processes and
/// non-executables, which aren't user-initiated launches. Pure logic over injected ports.
/// </summary>
public sealed class LaunchSampler
{
    private readonly ILaunchRecorder _recorder;
    private readonly ITrackingSettings _settings;
    private readonly IClock _clock;

    public LaunchSampler(ILaunchRecorder recorder, ITrackingSettings settings, IClock clock)
    {
        _recorder = recorder;
        _settings = settings;
        _clock = clock;
    }

    public void OnProcessStarted(string? executablePath)
    {
        if (!_settings.LaunchTrackingEnabled) return;
        if (!IsUserApplication(executablePath)) return;
        _recorder.RecordLaunch(executablePath!, _clock.UtcNow);
    }

    /// <summary>
    /// A coarse "is this a user-launched app" filter: a real .exe outside the Windows directory.
    /// System processes under <c>…\Windows\</c> are background noise, not user launches.
    /// </summary>
    private static bool IsUserApplication(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;
        var lower = path.ToLowerInvariant();
        return lower.EndsWith(".exe") && !lower.Contains(@":\windows\");
    }
}
