using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using SysGreen.Core.Abstractions;
using SysGreen.Core.Domain;
using SysGreen.Core.Knowledge;
using SysGreen.Core.Recommendations;
using SysGreen.Data;

namespace SysGreen.App.ViewModels;

/// <summary>
/// Drives the three-view shell (ADR / Q15): Recommendations, All Items (grouped by Purpose),
/// and History. This scaffold runs the real pipeline end-to-end — enumerate Run keys,
/// classify against the Knowledge Base, and produce recommendations — rendered as text.
/// </summary>
public sealed partial class MainViewModel : ObservableObject
{
    private readonly IAutostartProvider _autostart;
    private readonly IProcessProvider _processes;
    private readonly IClassifier _classifier;
    private readonly IRecommendationEngine _engine;
    private readonly IUsageRepository _usage;
    private readonly IChangeRecordRepository _history;

    [ObservableProperty]
    private string _summary = "Loading…";

    public ObservableCollection<string> Recommendations { get; } = [];
    public ObservableCollection<string> AllItems { get; } = [];
    public ObservableCollection<string> History { get; } = [];

    public MainViewModel(
        IAutostartProvider autostart,
        IProcessProvider processes,
        IClassifier classifier,
        IRecommendationEngine engine,
        IUsageRepository usage,
        IChangeRecordRepository history)
    {
        _autostart = autostart;
        _processes = processes;
        _classifier = classifier;
        _engine = engine;
        _usage = usage;
        _history = history;
        Refresh();
    }

    public void Refresh()
    {
        var autostartEntries = _autostart.Enumerate();
        var processes = _processes.Enumerate();

        var items = BuildItems(autostartEntries, processes);
        var usageRecords = _usage.GetAll();
        var recommendations = _engine.Recommend(items, usageRecords, DateTime.UtcNow);

        Recommendations.Clear();
        if (recommendations.Count == 0)
            Recommendations.Add("No recommendations — nothing safe-and-unneeded found on this machine yet.");
        foreach (var r in recommendations)
            Recommendations.Add($"{r.Item.DisplayName}  —  {r.Reason}  [{r.Source}]");

        AllItems.Clear();
        foreach (var i in items.OrderBy(i => i.Purpose).ThenBy(i => i.DisplayName))
            AllItems.Add($"{i.DisplayName,-32} [{i.Purpose}/{i.Safety}]  " +
                         $"{(i.IsRunning ? "running" : "stopped")}  ({i.Autostart?.Location})");

        History.Clear();
        var recent = _history.GetRecent();
        if (recent.Count == 0) History.Add("No changes yet. Anything you disable will appear here, fully reversible.");
        foreach (var c in recent)
            History.Add($"{c.TimestampUtc:g}  {c.Action}  {c.ItemName}  ({(c.Success ? "ok" : "failed")})");

        Summary = $"{items.Count} startup items · {recommendations.Count} recommendations · " +
                  $"{processes.Count} processes running";
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
