using SysGreen.Core.Abstractions;
using SysGreen.Core.ChangeLog;
using SysGreen.Core.Domain;

namespace SysGreen.Core.Startup;

/// <summary>
/// Disables/enables a UWP app's background access non-destructively via its allowed flag (ADR-0005),
/// addressing it by the package family name in <see cref="AutostartEntry.MechanismKey"/>. Reversible
/// by construction; the registry I/O lives behind <see cref="IBackgroundAppStore"/>.
/// </summary>
public sealed class BackgroundAppItemController : IItemController
{
    private readonly IBackgroundAppStore _store;
    private readonly IClock _clock;

    public BackgroundAppItemController(IBackgroundAppStore store, IClock clock)
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
        throw new NotSupportedException("A background app's access is disabled, not ended.");

    private string ReadState(AutostartEntry entry) =>
        _store.IsEnabled(entry.MechanismKey) ? "Enabled" : "Disabled";

    private ChangeRecord Record(AutostartEntry entry, ChangeAction action, string prior, string next) =>
        new(Guid.NewGuid().ToString("n"), entry.Id, entry.DisplayName, action, prior, next, "BackgroundAccess",
            _clock.UtcNow, true, null)
        { Location = entry.Location, MechanismKey = entry.MechanismKey };
}
