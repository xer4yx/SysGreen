using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SysGreen.Core.Abstractions;
using SysGreen.Core.Apply;
using SysGreen.Core.ChangeLog;
using SysGreen.Core.Domain;
using SysGreen.Core.Knowledge;
using SysGreen.Core.Recommendations;
using SysGreen.Data;

namespace SysGreen.App.ViewModels;

/// <summary>
/// Drives the three-view shell (ADR / Q15): Recommendations (selectable + Apply), All Items,
/// and History. Apply commits the checked recommendations through <see cref="IApplyService"/>
/// (never auto-applied — ADR-0007).
/// </summary>
public sealed partial class MainViewModel : ObservableObject
{
    private readonly IAutostartProvider _autostart;
    private readonly IProcessProvider _processes;
    private readonly IClassifier _classifier;
    private readonly IRecommendationEngine _engine;
    private readonly IUsageRepository _usage;
    private readonly IChangeRecordRepository _history;
    private readonly IApplyService _apply;
    private readonly IChangeReverser _reverser;

    [ObservableProperty]
    private string _summary = "Loading…";

    [ObservableProperty]
    private string _applyStatus = "";

    [ObservableProperty]
    private string _historyStatus = "";

    [ObservableProperty]
    private bool _historyEmpty;

    public ObservableCollection<RecommendationViewModel> Recommendations { get; } = [];
    public ObservableCollection<string> AllItems { get; } = [];
    public ObservableCollection<HistoryBatchViewModel> History { get; } = [];

    public MainViewModel(
        IAutostartProvider autostart,
        IProcessProvider processes,
        IClassifier classifier,
        IRecommendationEngine engine,
        IUsageRepository usage,
        IChangeRecordRepository history,
        IApplyService apply,
        IChangeReverser reverser)
    {
        _autostart = autostart;
        _processes = processes;
        _classifier = classifier;
        _engine = engine;
        _usage = usage;
        _history = history;
        _apply = apply;
        _reverser = reverser;
        Refresh();
    }

    [RelayCommand]
    private void Apply()
    {
        var selected = Recommendations
            .Where(r => r.IsSelected && r.Item.Autostart is not null)
            .ToList();
        if (selected.Count == 0)
        {
            ApplyStatus = "Nothing selected.";
            return;
        }

        var changes = selected
            .Select(r => new PendingChange(r.Item.Autostart!, ChangeAction.Disable))
            .ToList();

        var result = _apply.Apply(changes);
        ApplyStatus = ProblemMessage(result)
            ?? $"Disabled {result.SucceededCount} of {changes.Count}" +
               (result.FailedCount > 0 ? $", {result.FailedCount} failed." : ".");

        Refresh();
    }

    /// <summary>
    /// Re-enables or undoes committed changes (one record from a row, or a whole batch) by routing the
    /// inverse through <see cref="IChangeReverser"/> — which elevates and creates a restore point as the
    /// item requires (ADR-0005). Centralized here so status and refresh happen in one place.
    /// </summary>
    public void ReverseChanges(IReadOnlyList<ChangeRecord> records)
    {
        if (records.Count == 0) return;

        var result = _reverser.Reverse(records);
        HistoryStatus = ProblemMessage(result)
            ?? $"Reversed {result.SucceededCount} change{(result.SucceededCount == 1 ? "" : "s")}" +
               (result.FailedCount > 0 ? $", {result.FailedCount} failed." : ".");

        Refresh();
    }

    /// <summary>The shared "nothing was applied" explanations for Apply and Undo, or null on success.</summary>
    private static string? ProblemMessage(ApplyResult result) => result switch
    {
        { ElevationDeclined: true } => "No changes were made — you declined the Windows permission prompt.",
        { Aborted: true } => "Couldn't create a restore point — no changes were made.",
        _ => null,
    };

    public void Refresh()
    {
        var autostartEntries = _autostart.Enumerate();
        var processes = _processes.Enumerate();

        var items = BuildItems(autostartEntries, processes);
        var usageRecords = _usage.GetAll();
        var recommendations = _engine.Recommend(items, usageRecords, DateTime.UtcNow);

        Recommendations.Clear();
        foreach (var r in recommendations)
            Recommendations.Add(new RecommendationViewModel(r));

        AllItems.Clear();
        foreach (var i in items.OrderBy(i => i.Purpose).ThenBy(i => i.DisplayName))
            AllItems.Add($"{i.DisplayName,-32} [{i.Purpose}/{i.Safety}]  " +
                         $"{(i.IsRunning ? "running" : "stopped")}  ({i.Autostart?.Location})");

        History.Clear();
        var recent = _history.GetRecent();
        HistoryEmpty = recent.Count == 0;
        foreach (var batch in GroupByBatch(recent))
            History.Add(new HistoryBatchViewModel(batch, ReverseChanges));

        Summary = $"{items.Count} startup items · {recommendations.Count} recommendations · " +
                  $"{processes.Count} processes running";
    }

    /// <summary>
    /// Groups the (newest-first) records into Apply batches by their <c>BatchId</c>. A batch's records
    /// share a timestamp so they are contiguous in the list; records without a batch id stand alone.
    /// </summary>
    private static IEnumerable<IReadOnlyList<ChangeRecord>> GroupByBatch(IReadOnlyList<ChangeRecord> records)
    {
        for (var i = 0; i < records.Count;)
        {
            var batchId = records[i].BatchId;
            var group = new List<ChangeRecord> { records[i] };
            i++;
            // An empty batch id (e.g. a stray End Task) never coalesces — each stands on its own.
            while (!string.IsNullOrEmpty(batchId) && i < records.Count && records[i].BatchId == batchId)
                group.Add(records[i++]);
            yield return group;
        }
    }

    private List<ManageableItem> BuildItems(
        IReadOnlyList<AutostartEntry> entries, IReadOnlyList<ProcessInfo> processes)
    {
        var items = new List<ManageableItem>(entries.Count);
        foreach (var entry in entries)
        {
            var classification = _classifier.Classify(entry);
            var running = FindRunningProcess(entry, processes);
            items.Add(new ManageableItem(
                entry.Id, entry.DisplayName, entry.Kind, entry, running,
                classification.Purpose, classification.Safety,
                running?.PrivateWorkingSetBytes));
        }
        return items;
    }

    private static ProcessInfo? FindRunningProcess(AutostartEntry entry, IReadOnlyList<ProcessInfo> processes)
    {
        if (entry.ExecutablePath is not { } path) return null;
        var fileName = Path.GetFileNameWithoutExtension(path);
        return processes.FirstOrDefault(p =>
            string.Equals(p.Name, fileName, StringComparison.OrdinalIgnoreCase));
    }
}
