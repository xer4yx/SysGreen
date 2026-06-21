namespace SysGreen.Core.ChangeLog;

/// <summary>The verbs SysGreen records. Disable/Enable are reversible; EndTask is transient.</summary>
public enum ChangeAction
{
    Disable,
    Enable,
    EndTask,
}

/// <summary>
/// The undo unit. Captures the item, its exact prior state, and the mechanism used,
/// so a change can be restored precisely. See ADR-0005. Disabling never deletes.
/// </summary>
public sealed record ChangeRecord(
    string Id,
    string ItemId,
    string ItemName,
    ChangeAction Action,
    string PriorState,
    string NewState,
    string Mechanism,
    DateTime TimestampUtc,
    bool Success,
    string? Error);
