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
    private readonly Action<IReadOnlyList<ChangeRecord>> _reverse;

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

    public HistoryItemViewModel(ChangeRecord record, Action<IReadOnlyList<ChangeRecord>> reverse)
    {
        _record = record;
        _reverse = reverse;
        var stamp = record.TimestampUtc.ToLocalTime().ToString("g");
        var status = record.Success ? "" : "  (failed)";
        DisplayText = $"{stamp}   {record.Action,-8} {record.ItemName}{status}";
    }

    [RelayCommand(CanExecute = nameof(CanReEnable))]
    private void ReEnable() => _reverse([_record]);
}
