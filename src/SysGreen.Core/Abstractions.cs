using SysGreen.Core.ChangeLog;
using SysGreen.Core.Domain;

namespace SysGreen.Core.Abstractions;

/// <summary>Enumerates Autostart Entries from registry Run keys + Startup folders.</summary>
public interface IAutostartProvider
{
    IReadOnlyList<AutostartEntry> Enumerate();
}

/// <summary>Enumerates live processes with their Private Working Set.</summary>
public interface IProcessProvider
{
    IReadOnlyList<ProcessInfo> Enumerate();
}

/// <summary>Enumerates Windows Services (deferred tier — present for completeness).</summary>
public interface IWindowsServiceProvider
{
    IReadOnlyList<AutostartEntry> Enumerate();
}

/// <summary>Enumerates logon-triggered scheduled tasks.</summary>
public interface IScheduledTaskProvider
{
    IReadOnlyList<AutostartEntry> Enumerate();
}

/// <summary>
/// Performs the actual Disable/Enable/EndTask using Windows-native non-destructive
/// mechanisms, returning a <see cref="ChangeRecord"/> for the undo log. See ADR-0005.
/// </summary>
public interface IItemController
{
    ChangeRecord Disable(AutostartEntry entry);
    ChangeRecord Enable(AutostartEntry entry);
    ChangeRecord EndTask(ProcessInfo process);
}

/// <summary>
/// Creates a Windows System Restore point before a risky batch — the catastrophe
/// lifeline that works even if SysGreen can't run. Mandatory per ADR-0005.
/// </summary>
public interface IRestorePointService
{
    /// <summary>True if System Restore is enabled and a point was created.</summary>
    bool TryCreateRestorePoint(string description);
}

/// <summary>
/// Reads/writes the Windows StartupApproved flag for a startup item (the non-destructive
/// enable/disable mechanism, ADR-0005). The implementation maps an <see cref="AutostartLocation"/>
/// to the correct registry subkey; callers stay free of registry-path knowledge.
/// </summary>
public interface IStartupApprovedStore
{
    /// <summary>The current flag bytes, or null if the item has never been toggled (= enabled).</summary>
    byte[]? ReadFlag(AutostartLocation location, string valueName);
    void WriteFlag(AutostartLocation location, string valueName, byte[] data);
}

/// <summary>Terminates a live process (the End Task mechanism).</summary>
public interface IProcessTerminator
{
    void Terminate(int pid);
}

/// <summary>Abstracts the system clock so time-dependent behavior is testable.</summary>
public interface IClock
{
    DateTime UtcNow { get; }
}

/// <summary>
/// Persistence port for the undo log. The Data layer adapts this to SQLite; the Apply flow
/// depends only on this Core abstraction (ADR-0006 / ADR-0011).
/// </summary>
public interface IChangeLog
{
    void Record(ChangeRecord record);
}

/// <summary>The outcome of a raw restore-point creation attempt.</summary>
public enum RestorePointStatus
{
    /// <summary>A new restore point was created.</summary>
    Created,
    /// <summary>None created because a recent one already exists (Windows ~24h throttle).</summary>
    AlreadyExistsRecently,
    /// <summary>Creation failed.</summary>
    Failed,
}

/// <summary>
/// Raw restore-point creation. The actual WMI call lives behind this humble seam; turning its
/// result into the <see cref="IRestorePointService"/> contract is the tested logic.
/// </summary>
public interface IRestorePointApi
{
    RestorePointStatus CreateRestorePoint(string description);
}
