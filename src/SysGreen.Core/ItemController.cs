using SysGreen.Core.Abstractions;
using SysGreen.Core.ChangeLog;
using SysGreen.Core.Domain;

namespace SysGreen.Core.Startup;

/// <summary>
/// Performs Disable/Enable non-destructively via the Windows StartupApproved flags, and End Task
/// via process termination — each producing a precise <see cref="ChangeRecord"/> for the undo log
/// (ADR-0005). Registry and process I/O are injected so the logic is unit-testable.
/// </summary>
public sealed class StartupApprovedItemController : IItemController
{
    private readonly IStartupApprovedStore _store;
    private readonly IProcessTerminator _terminator;
    private readonly IClock _clock;

    public StartupApprovedItemController(
        IStartupApprovedStore store, IProcessTerminator terminator, IClock clock)
    {
        _store = store;
        _terminator = terminator;
        _clock = clock;
    }

    public ChangeRecord Disable(AutostartEntry entry)
    {
        var prior = ReadState(entry);
        _store.WriteFlag(entry.Location, entry.MechanismKey, StartupApprovedFlag.EncodeDisabled(_clock.UtcNow));
        return Record(entry, ChangeAction.Disable, prior, "Disabled", "StartupApproved");
    }

    public ChangeRecord Enable(AutostartEntry entry)
    {
        var prior = ReadState(entry);
        _store.WriteFlag(entry.Location, entry.MechanismKey, StartupApprovedFlag.EncodeEnabled());
        return Record(entry, ChangeAction.Enable, prior, "Enabled", "StartupApproved");
    }

    public ChangeRecord EndTask(ProcessInfo process)
    {
        _terminator.Terminate(process.Pid);
        // A live process has no Autostart Entry, so no location/key and nothing to reverse (transient).
        return new ChangeRecord(Guid.NewGuid().ToString("n"), process.Pid.ToString(), process.Name,
            ChangeAction.EndTask, "Running", "Ended", "ProcessKill", _clock.UtcNow, true, null);
    }

    /// <summary>Reads the item's current state from the StartupApproved flag (re-check, ADR-0013).</summary>
    private string ReadState(AutostartEntry entry) =>
        StartupApprovedFlag.IsEnabled(_store.ReadFlag(entry.Location, entry.MechanismKey)) ? "Enabled" : "Disabled";

    private ChangeRecord Record(
        AutostartEntry entry, ChangeAction action, string prior, string next, string mechanism) =>
        new(Guid.NewGuid().ToString("n"), entry.Id, entry.DisplayName, action, prior, next, mechanism,
            _clock.UtcNow, true, null)
        { Location = entry.Location, MechanismKey = entry.MechanismKey };
}
