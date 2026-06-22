using SysGreen.Core.Abstractions;
using SysGreen.Core.ChangeLog;
using SysGreen.Core.Domain;

namespace SysGreen.Core.Startup;

/// <summary>
/// Disables/enables a logon scheduled task non-destructively via its enabled flag (ADR-0005),
/// addressing it by the task path carried in <see cref="AutostartEntry.MechanismKey"/>. The decision
/// logic is here; the Task Scheduler I/O lives behind <see cref="IScheduledTaskStore"/>.
/// </summary>
public sealed class ScheduledTaskItemController : IItemController
{
    private readonly IScheduledTaskStore _store;
    private readonly IClock _clock;

    public ScheduledTaskItemController(IScheduledTaskStore store, IClock clock)
    {
        _store = store;
        _clock = clock;
    }

    public ChangeRecord Disable(AutostartEntry entry)
    {
        var prior = ReadState(entry);
        _store.SetEnabled(entry.MechanismKey, false);
        return Record(entry, ChangeAction.Disable, prior, "Disabled");
    }

    public ChangeRecord Enable(AutostartEntry entry)
    {
        var prior = ReadState(entry);
        _store.SetEnabled(entry.MechanismKey, true);
        return Record(entry, ChangeAction.Enable, prior, "Enabled");
    }

    public ChangeRecord EndTask(ProcessInfo process) =>
        throw new NotSupportedException("A scheduled task is disabled, not ended.");

    private string ReadState(AutostartEntry entry) =>
        _store.IsEnabled(entry.MechanismKey) ? "Enabled" : "Disabled";

    private ChangeRecord Record(AutostartEntry entry, ChangeAction action, string prior, string next) =>
        new(Guid.NewGuid().ToString("n"), entry.Id, entry.DisplayName, action, prior, next, "ScheduledTask",
            _clock.UtcNow, true, null)
        { Location = entry.Location, MechanismKey = entry.MechanismKey };
}

/// <summary>
/// Routes each Disable/Enable to the controller for the item's mechanism — StartupApproved flags for
/// Run keys and Startup folders, the Task Scheduler for scheduled tasks (ADR-0005). End Task is
/// process termination, mechanism-agnostic, so it always goes to the StartupApproved controller.
/// </summary>
public sealed class DispatchingItemController : IItemController
{
    private readonly IItemController _startupApproved;
    private readonly IItemController _scheduledTask;

    public DispatchingItemController(IItemController startupApproved, IItemController scheduledTask)
    {
        _startupApproved = startupApproved;
        _scheduledTask = scheduledTask;
    }

    public ChangeRecord Disable(AutostartEntry entry) => For(entry).Disable(entry);
    public ChangeRecord Enable(AutostartEntry entry) => For(entry).Enable(entry);
    public ChangeRecord EndTask(ProcessInfo process) => _startupApproved.EndTask(process);

    private IItemController For(AutostartEntry entry) =>
        entry.Location == AutostartLocation.ScheduledTask ? _scheduledTask : _startupApproved;
}
