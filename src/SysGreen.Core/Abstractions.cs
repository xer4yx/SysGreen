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
