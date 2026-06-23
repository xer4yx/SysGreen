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
using SysGreen.Core.Usage;
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
    private readonly IOverrideStore _overrides;
    private readonly IItemController _controller;

    [ObservableProperty]
    private string _summary = "Loading…";

    [ObservableProperty]
    private string _applyStatus = "";

    [ObservableProperty]
    private string _historyStatus = "";

    [ObservableProperty]
    private bool _historyEmpty;

    public ObservableCollection<RecommendationViewModel> Recommendations { get; } = [];
    public ObservableCollection<PurposeGroupViewModel> AllItemGroups { get; } = [];
    public ObservableCollection<HistoryBatchViewModel> History { get; } = [];

    public MainViewModel(
        IAutostartProvider autostart,
        IProcessProvider processes,
        IClassifier classifier,
        IRecommendationEngine engine,
        IUsageRepository usage,
        IChangeRecordRepository history,
        IApplyService apply,
        IChangeReverser reverser,
        IOverrideStore overrides,
        IItemController controller)
    {
        _autostart = autostart;
        _processes = processes;
        _classifier = classifier;
        _engine = engine;
        _usage = usage;
        _history = history;
        _apply = apply;
        _reverser = reverser;
        _overrides = overrides;
        _controller = controller;
        Refresh();
    }

    /// <summary>
    /// End Task (CONTEXT.md / ADR-0005): kills the item's running process for instant RAM relief.
    /// Transient — the app returns next time it starts — and not part of the undo model. The kill is
    /// logged so it shows in History; a process we can't terminate (e.g. elevated) reports an error.
    /// </summary>
    public void EndTask(ManageableItem item)
    {
        if (item.RunningProcess is not { } process) return;
        try
        {
            _history.Add(_controller.EndTask(process));
            ApplyStatus = $"Ended {item.DisplayName}. It returns next time it starts.";
        }
        catch (Exception ex)
        {
            ApplyStatus = $"Couldn't end {item.DisplayName}: {ex.Message}";
        }
        Refresh();
    }

    /// <summary>
    /// Records a "never recommend" Override for the item (top-precedence classification, CONTEXT.md),
    /// then refreshes so it drops out of the recommendations.
    /// </summary>
    public void NeverRecommend(ManageableItem item)
    {
        if (item.Autostart is not { } entry) return;
        if (ExecutableIdentity.PrimaryName(entry) is not { } name) return;

        // Preserve any existing Purpose correction; just assert "never recommend".
        var existing = _overrides.Get(name);
        _overrides.Set(new UserOverride(name, existing?.Purpose, NeverRecommend: true));

        ApplyStatus = $"Won't recommend {item.DisplayName} again.";
        Refresh();
    }

    /// <summary>Disables the given items in one batch (per-item or whole-group from All Items).</summary>
    public void DisableItems(IReadOnlyList<ManageableItem> items)
    {
        var changes = items
            .Where(i => i.Autostart is not null && i.CanDisable)
            .Select(i => new PendingChange(i.Autostart!, ChangeAction.Disable))
            .ToList();
        if (changes.Count == 0)
        {
            ApplyStatus = "Nothing to disable.";
            return;
        }

        var result = _apply.Apply(changes);
        ApplyStatus = ProblemMessage(result)
            ?? $"Disabled {result.SucceededCount} of {changes.Count}" +
               (result.FailedCount > 0 ? $", {result.FailedCount} failed." : ".");
        Refresh();
    }

    /// <summary>Records a user Override that relabels the item's Purpose (CONTEXT.md "Override").</summary>
    public void SetItemPurpose(ManageableItem item, Purpose purpose)
    {
        if (item.Autostart is not { } entry) return;
        if (ExecutableIdentity.PrimaryName(entry) is not { } name) return;

        var existing = _overrides.Get(name);
        _overrides.Set(new UserOverride(name, purpose, existing?.NeverRecommend ?? false));
        ApplyStatus = $"Set {item.DisplayName}'s purpose to {purpose}.";
        Refresh();
    }

    [RelayCommand]
    private void Apply()
    {
        var selected = Recommendations
            .Where(r => r.IsSelected && r.Item.Autostart is not null)
            .Select(r => r.Item)
            .ToList();
        if (selected.Count == 0)
        {
            ApplyStatus = "Nothing selected.";
            return;
        }

        DisableItems(selected);
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
            Recommendations.Add(new RecommendationViewModel(r, NeverRecommend));

        AllItemGroups.Clear();
        foreach (var group in BuildItemGroups(items))
            AllItemGroups.Add(group);

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

    /// <summary>Groups items by Purpose into actionable rows for the All Items view (Q15).</summary>
    private IEnumerable<PurposeGroupViewModel> BuildItemGroups(IReadOnlyList<ManageableItem> items) =>
        items
            .GroupBy(i => i.Purpose)
            .OrderBy(g => g.Key)
            .Select(g => new PurposeGroupViewModel(
                g.Key, g.OrderBy(i => i.DisplayName).Select(MakeItemVm).ToList(), DisableItems));

    private AllItemViewModel MakeItemVm(ManageableItem item) =>
        new(item, i => DisableItems([i]), SetItemPurpose, NeverRecommend, EndTask);

    private List<ManageableItem> BuildItems(
        IReadOnlyList<AutostartEntry> entries, IReadOnlyList<ProcessInfo> processes)
    {
        var items = new List<ManageableItem>(entries.Count);
        foreach (var entry in entries)
        {
            var classification = _classifier.Classify(entry);
            var running = FindRunningProcess(entry, processes);
            // RAM estimate chain (CONTEXT.md / Q12): live Private Working Set → historical median
            // (the Tray Agent isn't sampling yet, so null) → KB typical → Unknown.
            var ram = RamEstimate.Resolve(
                running?.PrivateWorkingSetBytes, historicalMedian: null, classification.TypicalRamBytes).Bytes;
            items.Add(new ManageableItem(
                entry.Id, entry.DisplayName, entry.Kind, entry, running,
                classification.Purpose, classification.Safety, ram));
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
