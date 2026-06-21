using SysGreen.Core.Abstractions;
using SysGreen.Core.ChangeLog;
using SysGreen.Core.Domain;
using SysGreen.Core.Usage;

namespace SysGreen.Platform;

// Scaffolded seams — real implementations land in later milestones. Each is isolated
// behind a Core interface (ADR-0011) so it can be built and tested independently.

/// <summary>TODO: enumerate logon-triggered tasks via the TaskScheduler NuGet.</summary>
public sealed class ScheduledTaskProvider : IScheduledTaskProvider
{
    public IReadOnlyList<AutostartEntry> Enumerate() => [];
}

/// <summary>TODO (deferred tier, ADR-0001): enumerate services via ServiceController + registry Start type.</summary>
public sealed class WindowsServiceProvider : IWindowsServiceProvider
{
    public IReadOnlyList<AutostartEntry> Enumerate() => [];
}

/// <summary>TODO: read + ROT13-decode HKCU UserAssist as the habit seed (ADR-0008).</summary>
public sealed class UserAssistUsageHistoryProvider : IUsageHistoryProvider
{
    public IReadOnlyList<UsageRecord> ReadSeedHistory() => [];
}

/// <summary>
/// TODO: implement non-destructive Disable/Enable via StartupApproved flags and End Task via
/// process kill, each returning a precise <see cref="ChangeRecord"/> (ADR-0005).
/// </summary>
public sealed class ItemController : IItemController
{
    public ChangeRecord Disable(AutostartEntry entry) =>
        throw new NotImplementedException("Disable via StartupApproved flags — see ADR-0005.");

    public ChangeRecord Enable(AutostartEntry entry) =>
        throw new NotImplementedException("Enable via StartupApproved flags — see ADR-0005.");

    public ChangeRecord EndTask(ProcessInfo process) =>
        throw new NotImplementedException("End Task via process kill — see ADR-0005.");
}

/// <summary>TODO: create a restore point via WMI SystemRestore.CreateRestorePoint (ADR-0005, mandatory).</summary>
public sealed class SystemRestorePointService : IRestorePointService
{
    public bool TryCreateRestorePoint(string description) => false;
}
