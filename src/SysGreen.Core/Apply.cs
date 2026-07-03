using System.Text.Json.Serialization;
using SysGreen.Core.Abstractions;
using SysGreen.Core.ChangeLog;
using SysGreen.Core.Domain;

namespace SysGreen.Core.Apply;

/// <summary>A requested Disable/Enable on one Autostart Entry, pending commit by Apply.</summary>
public sealed record PendingChange(AutostartEntry Entry, ChangeAction Action);

/// <summary>The outcome of an Apply batch.</summary>
public sealed record ApplyResult(
    bool RestorePointRequired,
    bool RestorePointCreated,
    IReadOnlyList<ChangeRecord> Records)
{
    /// <summary>
    /// True when an admin-only batch was not applied because the user dismissed the UAC prompt
    /// (ADR-0004). Distinct from a restore-point failure — nothing was attempted at all.
    /// </summary>
    public bool ElevationDeclined { get; init; }

    /// <summary>True when a required restore point could not be created, so nothing was applied.</summary>
    [JsonIgnore]
    public bool Aborted => RestorePointRequired && !RestorePointCreated;
    [JsonIgnore]
    public int SucceededCount => Records.Count(r => r.Success);
    [JsonIgnore]
    public int FailedCount => Records.Count(r => !r.Success);
}

/// <summary>
/// Commits a batch of pending changes: creates a mandatory System Restore point first if the
/// batch is risky (touches HKLM/services), then applies each change best-effort (continue on
/// error), persisting a Change Record for each. See ADR-0005 and ADR-0013.
/// </summary>
public interface IApplyService
{
    ApplyResult Apply(IReadOnlyList<PendingChange> changes);
}

public sealed class ApplyService : IApplyService
{
    private readonly IItemController _controller;
    private readonly IChangeLog _changeLog;
    private readonly IRestorePointService _restorePoints;
    private readonly IClock _clock;
    private readonly IApplyProgressSink _progress;

    public ApplyService(
        IItemController controller, IChangeLog changeLog,
        IRestorePointService restorePoints, IClock clock,
        IApplyProgressSink? progress = null)
    {
        _controller = controller;
        _changeLog = changeLog;
        _restorePoints = restorePoints;
        _clock = clock;
        // Optional so every existing caller/test compiles unchanged; in-process applies stay silent.
        _progress = progress ?? NullApplyProgressSink.Instance;
    }

    public ApplyResult Apply(IReadOnlyList<PendingChange> changes)
    {
        var total = changes.Count;
        bool restorePointRequired = changes.Any(c => c.Entry.RequiresElevation);
        bool restorePointCreated = false;
        if (restorePointRequired)
        {
            _progress.Report(new ApplyProgress(ApplyStage.CreatingRestorePoint, 0, total));
            restorePointCreated = _restorePoints.TryCreateRestorePoint("SysGreen: before applying changes");
            // Mandatory lifeline (ADR-0005): without it, do not touch a risky batch.
            if (!restorePointCreated)
                return new ApplyResult(true, false, []);
        }

        // One id per Apply so the History view can offer a per-batch Undo (ADR-0005).
        var batchId = Guid.NewGuid().ToString("n");
        var records = new List<ChangeRecord>(changes.Count);
        for (var i = 0; i < changes.Count; i++)
        {
            _progress.Report(new ApplyProgress(ApplyStage.Applying, i + 1, total));
            var record = ApplyOne(changes[i]) with { BatchId = batchId };
            _changeLog.Record(record);
            records.Add(record);
        }
        _progress.Report(new ApplyProgress(ApplyStage.Done, total, total));
        return new ApplyResult(restorePointRequired, restorePointCreated, records);
    }

    /// <summary>Best-effort: a failure becomes a failed Change Record, not a thrown batch (ADR-0013).</summary>
    private ChangeRecord ApplyOne(PendingChange change)
    {
        try
        {
            return change.Action switch
            {
                ChangeAction.Disable => _controller.Disable(change.Entry),
                ChangeAction.Enable => _controller.Enable(change.Entry),
                _ => throw new NotSupportedException($"Apply does not support {change.Action}."),
            };
        }
        catch (Exception ex)
        {
            return new ChangeRecord(
                Guid.NewGuid().ToString("n"), change.Entry.Id, change.Entry.DisplayName,
                change.Action, "Unknown", "Unknown", "StartupApproved", _clock.UtcNow, false, ex.Message)
            { Location = change.Entry.Location, MechanismKey = change.Entry.MechanismKey };
        }
    }
}
