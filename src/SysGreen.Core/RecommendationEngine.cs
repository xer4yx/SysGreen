using SysGreen.Core.Domain;
using SysGreen.Core.Usage;

namespace SysGreen.Core.Recommendations;

/// <summary>
/// Default engine. Safety is a hard gate (only <see cref="SafetyRating.Safe"/> items are ever
/// recommended). Static evidence (overhead Purpose) gives a baseline; habit evidence (Abandoned)
/// boosts it. Items with both rank highest. Never auto-applies. See ADR-0007.
/// </summary>
public sealed class RecommendationEngine : IRecommendationEngine
{
    private readonly int _abandonedThresholdDays;

    public RecommendationEngine(int abandonedThresholdDays = 30) =>
        _abandonedThresholdDays = abandonedThresholdDays;

    public IReadOnlyList<Recommendation> Recommend(
        IReadOnlyList<ManageableItem> items,
        IReadOnlyList<UsageRecord> usage,
        DateTime nowUtc)
    {
        // Keyed by executable filename: UserAssist records paths under known-folder GUID prefixes
        // while autostart entries carry full paths, so a full-path match would miss (ADR-0008).
        var usageByName = new Dictionary<string, UsageRecord>(StringComparer.OrdinalIgnoreCase);
        foreach (var u in usage) usageByName[ExecutableFileName(u.ExecutablePath)] = u;

        var recommendations = new List<Recommendation>();
        foreach (var item in items)
        {
            if (!item.CanDisable) continue;
            if (item.Safety != SafetyRating.Safe) continue; // hard safety gate

            bool isOverhead = item.Purpose is
                Purpose.WindowsTelemetry or Purpose.Updater or Purpose.OemDriver;

            UsageRecord? record = null;
            if (item.Autostart?.ExecutablePath is { } path)
                usageByName.TryGetValue(ExecutableFileName(path), out record);

            bool staticEvidence = isOverhead;
            bool habitEvidence = record is not null && record.IsAbandoned(nowUtc, _abandonedThresholdDays);
            if (!staticEvidence && !habitEvidence) continue;

            var source = staticEvidence && habitEvidence ? RecommendationSource.Both
                       : staticEvidence ? RecommendationSource.Static
                       : RecommendationSource.Habit;

            double purposeWeight = item.Purpose switch
            {
                Purpose.WindowsTelemetry or Purpose.Updater or Purpose.OemDriver => 1.0,
                Purpose.Media or Purpose.Communication or Purpose.Gaming => 0.5,
                _ => 0.3,
            };
            double habitBoost = habitEvidence
                ? Math.Min(1.0, record!.DaysSinceLastLaunch(nowUtc) / 90.0)
                : 0.0;

            recommendations.Add(new Recommendation(
                item, source, BuildReason(item, source, record, nowUtc), purposeWeight + habitBoost));
        }

        return recommendations.OrderByDescending(r => r.Score).ToList();
    }

    private static string BuildReason(
        ManageableItem item, RecommendationSource source, UsageRecord? record, DateTime nowUtc)
    {
        string ram = item.RamEstimateBytes is { } b ? $" · ≈{FormatRam(b)} at startup" : "";
        return source switch
        {
            RecommendationSource.Both =>
                $"Known {Describe(item.Purpose)} you haven't opened in {record!.DaysSinceLastLaunch(nowUtc)} days{ram}. Safe to disable.",
            RecommendationSource.Static =>
                $"{Describe(item.Purpose)} that loads at startup{ram}. Safe to disable.",
            _ =>
                $"Not opened in {record!.DaysSinceLastLaunch(nowUtc)} days{ram}. Safe to disable — you can still launch it anytime.",
        };
    }

    private static string Describe(Purpose p) => p switch
    {
        Purpose.WindowsTelemetry => "Windows telemetry",
        Purpose.Updater => "background updater",
        Purpose.OemDriver => "manufacturer add-on",
        Purpose.Media => "media app",
        Purpose.Communication => "chat app",
        Purpose.Gaming => "gaming app",
        _ => "background item",
    };

    private static string ExecutableFileName(string path) => System.IO.Path.GetFileName(path);

    private static string FormatRam(long bytes) =>
        bytes >= 1L << 30 ? $"{bytes / (double)(1L << 30):0.#} GB" : $"{bytes / (1L << 20)} MB";
}
