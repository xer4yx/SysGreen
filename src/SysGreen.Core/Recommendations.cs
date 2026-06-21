using SysGreen.Core.Domain;
using SysGreen.Core.Usage;

namespace SysGreen.Core.Recommendations;

/// <summary>Which source(s) produced a recommendation. See ADR-0007.</summary>
public enum RecommendationSource
{
    /// <summary>From the Knowledge Base alone (telemetry/updater/bloat). Primary.</summary>
    Static,
    /// <summary>From observed usage (abandoned apps). Secondary.</summary>
    Habit,
    /// <summary>Both sources agree — ranks highest.</summary>
    Both,
}

/// <summary>
/// A suggested action (almost always "disable this Autostart Entry") with a
/// human-readable reason and the score used to rank it.
/// </summary>
public sealed record Recommendation(
    ManageableItem Item,
    RecommendationSource Source,
    string Reason,
    double Score);

/// <summary>
/// Combines the two sources into a ranked recommended set. Safety is a hard gate
/// (only <see cref="SafetyRating.Safe"/> items are ever recommended). Never auto-applies.
/// See ADR-0007.
/// </summary>
public interface IRecommendationEngine
{
    IReadOnlyList<Recommendation> Recommend(
        IReadOnlyList<ManageableItem> items,
        IReadOnlyList<UsageRecord> usage,
        DateTime nowUtc);
}
