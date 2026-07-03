using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SysGreen.Core.ChangeLog;

namespace SysGreen.App.ViewModels;

/// <summary>
/// One row in the History view: a single committed Change Record, with a per-item Re-enable
/// (ADR-0005). The actual reversal is delegated back to the parent so elevation, the restore
/// point, and the refresh all run in one place.
/// </summary>
public sealed partial class HistoryItemViewModel : ObservableObject
{
    private readonly ChangeRecord _record;
    private readonly Func<IReadOnlyList<ChangeRecord>, Task> _reverse;
    private readonly ApplyBusyState _busy;

    public string DisplayText { get; }

    /// <summary>Label for the reverse button — the opposite of what was done (or n/a for End Task).</summary>
    public string ReverseLabel => _record.Action switch
    {
        ChangeAction.Disable => "Re-enable",
        ChangeAction.Enable => "Disable",
        _ => "—",
    };

    /// <summary>Only a successful, reversible change can be undone (not a failure or a transient End Task).</summary>
    public bool CanReEnable => _record.Success && _record.IsReversible;

    // Executable only when reversible AND no Apply is in flight (busy gate / Phase 6).
    private bool CanReEnableNow => CanReEnable && !_busy.IsApplying;

    public HistoryItemViewModel(ChangeRecord record, Func<IReadOnlyList<ChangeRecord>, Task> reverse, ApplyBusyState busy)
    {
        _record = record;
        _reverse = reverse;
        _busy = busy;
        var stamp = record.TimestampUtc.ToLocalTime().ToString("g");
        var status = record.Success ? "" : "  (failed)";
        DisplayText = $"{stamp}   {record.Action,-8} {record.ItemName}{status}";
    }

    /// <summary>Re-evaluates the busy-gated Re-enable command when an Apply starts or finishes (Phase 6).</summary>
    public void RefreshCommandStates() => ReEnableCommand.NotifyCanExecuteChanged();

    [RelayCommand(CanExecute = nameof(CanReEnableNow))]
    private Task ReEnable() => _reverse([_record]);
}
