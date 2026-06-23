using System.IO;
using SysGreen.Core.Domain;

namespace SysGreen.Core.Knowledge;

/// <summary>
/// A user-asserted correction that takes precedence over the Knowledge Base (CONTEXT.md "Override",
/// ADR-0002). It re-labels the Purpose, flags the item to never be recommended, or both. Stored
/// locally and always respected. Keyed by executable name (the launcher's filename or a Squirrel
/// launcher's real target).
/// </summary>
public sealed record UserOverride(string ExecutableName, Purpose? Purpose, bool NeverRecommend);

/// <summary>Local persistence of user Overrides, keyed by executable name (Data layer adapts to SQLite).</summary>
public interface IOverrideStore
{
    /// <summary>The override for this executable name, or null. Implementations normalize the key.</summary>
    UserOverride? Get(string executableName);
    void Set(UserOverride ov);
    void Remove(string executableName);
    IReadOnlyList<UserOverride> GetAll();
}

/// <summary>Normalizes an executable name to a stable override key (case-insensitive, no extension).</summary>
public static class OverrideKey
{
    public static string Normalize(string name)
    {
        name = name.Trim();
        if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) name = name[..^4];
        return name.ToLowerInvariant();
    }
}

/// <summary>
/// The names an item can be matched by, in priority order: the Squirrel <c>--processStart</c> target
/// (the real app) before the launcher executable's own filename (ADR-0010). Shared by KB matching
/// and Override matching so they agree on identity.
/// </summary>
public static class ExecutableIdentity
{
    public static IEnumerable<string> CandidateNames(AutostartEntry entry)
    {
        if (entry.TargetExecutable is { Length: > 0 } target) yield return target;
        if (entry.ExecutablePath is { } path)
        {
            yield return Path.GetFileName(path);
            // For MSIX/Store apps the exe stub is unreadable; the package name is the stable identity.
            if (PackagedApp.PackageName(path) is { } package) yield return package;
        }
    }

    public static string? PrimaryName(AutostartEntry entry) => CandidateNames(entry).FirstOrDefault();
}

/// <summary>
/// Applies user Overrides on top of an inner classifier (KB → heuristic → Unknown), as the
/// top-precedence source (CONTEXT.md). An override re-labels the Purpose and/or forces the item to
/// never be recommended; it can never make an item Safe — only the curated KB does that.
/// </summary>
public sealed class OverridingClassifier : IClassifier
{
    private readonly IClassifier _inner;
    private readonly IOverrideStore _overrides;

    public OverridingClassifier(IClassifier inner, IOverrideStore overrides)
    {
        _inner = inner;
        _overrides = overrides;
    }

    public Classification Classify(AutostartEntry entry)
    {
        var baseClassification = _inner.Classify(entry);
        foreach (var name in ExecutableIdentity.CandidateNames(entry))
        {
            if (_overrides.Get(name) is { } ov)
                return Apply(ov, baseClassification);
        }
        return baseClassification;
    }

    /// <summary>
    /// Overlays the override: re-label the Purpose if the user set one; force a non-recommendable
    /// Safety if they flagged "never recommend". Safety is never promoted to Safe here (ADR-0002).
    /// </summary>
    private static Classification Apply(UserOverride ov, Classification baseClassification) =>
        baseClassification with
        {
            Purpose = ov.Purpose ?? baseClassification.Purpose,
            Safety = ov.NeverRecommend ? SafetyRating.DoNotTouch : baseClassification.Safety,
            Source = ClassificationSource.Override,
        };
}
