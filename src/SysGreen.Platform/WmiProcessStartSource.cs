using System.Management;
using SysGreen.Core.Usage;

namespace SysGreen.Platform;

/// <summary>
/// Raises an event for each new process start by subscribing to WMI process-creation events
/// (non-elevated: <c>__InstanceCreationEvent</c> over <c>Win32_Process</c>, ~1s poll). Humble object;
/// the recording decision is the tested <see cref="LaunchSampler"/>. ExecutablePath is null for
/// processes this user can't read — the sampler ignores those.
/// </summary>
public sealed class WmiProcessStartSource : IProcessStartSource, IDisposable
{
    private ManagementEventWatcher? _watcher;

    public event Action<string?>? ProcessStarted;

    public void Start()
    {
        if (_watcher is not null) return;
        var query = new WqlEventQuery(
            "__InstanceCreationEvent", TimeSpan.FromSeconds(1), "TargetInstance ISA 'Win32_Process'");
        _watcher = new ManagementEventWatcher(query);
        _watcher.EventArrived += OnEventArrived;
        _watcher.Start();
    }

    public void Stop()
    {
        if (_watcher is null) return;
        try { _watcher.Stop(); }
        catch { /* already stopped / disposed */ }
    }

    private void OnEventArrived(object sender, EventArrivedEventArgs e)
    {
        try
        {
            var target = (ManagementBaseObject)e.NewEvent["TargetInstance"];
            ProcessStarted?.Invoke(target["ExecutablePath"] as string);
        }
        catch
        {
            // Malformed event / process vanished — ignore this one.
        }
    }

    public void Dispose()
    {
        Stop();
        _watcher?.Dispose();
        _watcher = null;
    }
}
