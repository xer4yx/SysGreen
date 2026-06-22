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
    private readonly Action<IReadOnlyList<ChangeRecord>> _reverse;

    public string Header { get; }
    public IReadOnlyList<HistoryItemViewModel> Items { get; }

    /// <summary>Enabled when the batch has at least one change that can actually be undone.</summary>
    public bool CanUndo => _records.Any(r => r.Success && r.IsReversible);

    public HistoryBatchViewModel(IReadOnlyList<ChangeRecord> records, Action<IReadOnlyList<ChangeRecord>> reverse)
    {
        _records = records;
        _reverse = reverse;
        var when = records[0].TimestampUtc.ToLocalTime().ToString("g");
        var count = records.Count;
        Header = $"{when}   ·   {count} change{(count == 1 ? "" : "s")}";
        Items = records.Select(r => new HistoryItemViewModel(r, reverse)).ToList();
    }

    [RelayCommand(CanExecute = nameof(CanUndo))]
    private void Undo() => _reverse(_records.Where(r => r.Success && r.IsReversible).ToList());
}
