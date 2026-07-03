using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SysGreen.Core.ChangeLog;

namespace SysGreen.App.ViewModels;

/// <summary>
/// A group of Change Records committed together in one Apply, offering a single per-batch Undo
/// (ADR-0005) alongside the per-item Re-enable on each row.
/// </summary>
public sealed partial class HistoryBatchViewModel : ObservableObject
{
    private readonly IReadOnlyList<ChangeRecord> _records;
    private readonly Func<IReadOnlyList<ChangeRecord>, Task> _reverse;
    private readonly ApplyBusyState _busy;

    public string Header { get; }
    public IReadOnlyList<HistoryItemViewModel> Items { get; }

    /// <summary>True when the batch has at least one change that can actually be undone.</summary>
    public bool CanUndo => _records.Any(r => r.Success && r.IsReversible);

    // Executable only when there is something to undo AND no Apply is in flight (busy gate / Phase 6).
    private bool CanUndoNow => CanUndo && !_busy.IsApplying;

    public HistoryBatchViewModel(
        IReadOnlyList<ChangeRecord> records, Func<IReadOnlyList<ChangeRecord>, Task> reverse, ApplyBusyState busy)
    {
        _records = records;
        _reverse = reverse;
        _busy = busy;
        var when = records[0].TimestampUtc.ToLocalTime().ToString("g");
        var count = records.Count;
        Header = $"{when}   ·   {count} change{(count == 1 ? "" : "s")}";
        Items = records.Select(r => new HistoryItemViewModel(r, reverse, busy)).ToList();
    }

    /// <summary>Re-evaluates the busy-gated Undo command (and its rows) when an Apply starts/finishes.</summary>
    public void RefreshCommandStates()
    {
        UndoCommand.NotifyCanExecuteChanged();
        foreach (var item in Items) item.RefreshCommandStates();
    }

    [RelayCommand(CanExecute = nameof(CanUndoNow))]
    private Task Undo() => _reverse(_records.Where(r => r.Success && r.IsReversible).ToList());
}
