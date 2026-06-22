namespace SysGreen.Core.Apply;

/// <summary>
/// Applies a batch of admin-only changes out-of-process via the short-lived elevated Helper
/// (ADR-0004/0011). Implemented in SysGreen.Platform as a process launcher; abstracted here so the
/// routing decision stays pure and unit-testable.
/// </summary>
public interface IElevatedApplyClient
{
    ApplyResult Apply(IReadOnlyList<PendingChange> elevatedChanges);
}

/// <summary>
/// The App-side Apply entry point (ADR-0004). A fully per-user batch is applied directly in-process;
/// a batch that touches any admin-only item is delegated <em>whole</em> to the elevated Helper under
/// a single UAC prompt, where the mandatory restore point also runs (ADR-0005/0011).
/// </summary>
public sealed class RoutingApplyService : IApplyService
{
    private readonly IApplyService _inProcess;
    private readonly IElevatedApplyClient _elevated;

    public RoutingApplyService(IApplyService inProcess, IElevatedApplyClient elevated)
    {
        _inProcess = inProcess;
        _elevated = elevated;
    }

    public ApplyResult Apply(IReadOnlyList<PendingChange> changes)
    {
        // One UAC prompt per Apply (ADR-0004): if any item is admin-only, the whole batch goes to
        // the elevated Helper — which also creates the mandatory restore point and persists the
        // Change Records to the shared database (ADR-0011). Fully per-user batches stay in-process.
        return changes.Any(c => c.Entry.RequiresElevation)
            ? _elevated.Apply(changes)
            : _inProcess.Apply(changes);
    }
}
