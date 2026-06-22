using SysGreen.Core.ChangeLog;
using SysGreen.Core.Domain;

namespace SysGreen.Core.Apply;

/// <summary>
/// Undoes committed Change Records — per-item Re-enable or per-batch Undo (ADR-0005) — by routing the
/// inverse Disable/Enable through the normal Apply pipeline. The reversal is therefore itself recorded
/// and itself reversible, elevates when the item is admin-only, and creates a restore point when risky.
/// </summary>
public interface IChangeReverser
{
    /// <summary>
    /// Reverses the supplied records in a single batch (one record = per-item Re-enable; a whole
    /// batch = per-batch Undo). Records that did not change anything — failures and transient
    /// EndTasks — are skipped.
    /// </summary>
    ApplyResult Reverse(IReadOnlyList<ChangeRecord> records);
}

public sealed class ChangeReverser : IChangeReverser
{
    private readonly IApplyService _apply;

    public ChangeReverser(IApplyService apply) => _apply = apply;

    public ApplyResult Reverse(IReadOnlyList<ChangeRecord> records)
    {
        // Only undo what actually changed: successful, reversible (Disable/Enable) records.
        var changes = records
            .Where(r => r.Success && r.IsReversible)
            .Select(Invert)
            .ToList();

        return changes.Count == 0
            ? new ApplyResult(false, false, [])
            : _apply.Apply(changes);
    }

    /// <summary>Builds the opposite change, reusing the record's captured location so the reversal
    /// targets the same item and elevates exactly when the original did.</summary>
    private static PendingChange Invert(ChangeRecord record)
    {
        var inverseAction = record.Action == ChangeAction.Disable ? ChangeAction.Enable : ChangeAction.Disable;
        var priorState = record.Action == ChangeAction.Disable ? AutostartState.Disabled : AutostartState.Enabled;
        var entry = new AutostartEntry(
            record.ItemId, record.ItemName, ItemKind.StartupApp, record.Location, null, null, priorState)
        {
            // Re-target the exact key the original change used (shortcut name, task path, …).
            MechanismKey = record.MechanismKey,
        };
        return new PendingChange(entry, inverseAction);
    }
}
