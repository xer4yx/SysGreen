using System.Text.Json.Serialization;
using SysGreen.Core.Domain;

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
    string? Error)
{
    /// <summary>
    /// Groups the records committed together in one Apply, so the History view can offer a
    /// per-batch Undo. Empty for a record written outside a batch.
    /// </summary>
    public string BatchId { get; init; } = "";

    /// <summary>
    /// Where the affected Autostart Entry lives — captured so a record is self-sufficient for
    /// reversal (it determines the disable mechanism and whether elevation is needed) without
    /// re-enumerating the system. <see cref="AutostartLocation.Unknown"/> for non-autostart records.
    /// </summary>
    public AutostartLocation Location { get; init; } = AutostartLocation.Unknown;

    /// <summary>True when this change can be undone — i.e. a Disable/Enable, not a transient EndTask.</summary>
    [JsonIgnore]
    public bool IsReversible => Action is ChangeAction.Disable or ChangeAction.Enable;
}
