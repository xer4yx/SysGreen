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

    public ApplyService(
        IItemController controller, IChangeLog changeLog,
        IRestorePointService restorePoints, IClock clock)
    {
        _controller = controller;
        _changeLog = changeLog;
        _restorePoints = restorePoints;
        _clock = clock;
    }

    public ApplyResult Apply(IReadOnlyList<PendingChange> changes)
    {
        bool restorePointRequired = changes.Any(c => c.Entry.RequiresElevation);
        bool restorePointCreated = false;
        if (restorePointRequired)
        {
            restorePointCreated = _restorePoints.TryCreateRestorePoint("SysGreen: before applying changes");
            // Mandatory lifeline (ADR-0005): without it, do not touch a risky batch.
            if (!restorePointCreated)
                return new ApplyResult(true, false, []);
        }

        var records = new List<ChangeRecord>(changes.Count);
        foreach (var change in changes)
        {
            var record = ApplyOne(change);
            _changeLog.Record(record);
            records.Add(record);
        }
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
                change.Action, "Unknown", "Unknown", "StartupApproved", _clock.UtcNow, false, ex.Message);
        }
    }
}
