using SysGreen.Core.Abstractions;
using SysGreen.Core.Domain;

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

// UserAssistUsageHistoryProvider is now a real implementation in its own file (TDD'd decoder,
// ADR-0008).

// ItemController is now implemented as StartupApprovedItemController in SysGreen.Core
// (test-driven, ADR-0005), backed by the StartupApprovedRegistryStore + ProcessTerminator adapters.

// SystemRestorePointService is now RestorePointService in SysGreen.Core (test-driven),
// backed by the WmiRestorePointApi adapter (ADR-0005).
