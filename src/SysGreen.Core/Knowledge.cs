using SysGreen.Core.Domain;

namespace SysGreen.Core.Knowledge;

/// <summary>Where a <see cref="Classification"/> came from. See ADR-0002.</summary>
public enum ClassificationSource
{
    /// <summary>Not confidently classified: Purpose=Unknown, Safety=Caution, never auto-recommended.</summary>
    Unknown = 0,
    /// <summary>Matched a curated Knowledge Base entry.</summary>
    KnowledgeBase,
    /// <summary>Inferred from file metadata (signature, description, install path).</summary>
    Heuristic,
    /// <summary>User Override — always wins.</summary>
    Override,
}

/// <summary>The result of classifying an item along the two axes.</summary>
public sealed record Classification(
    Purpose Purpose,
    SafetyRating Safety,
    ClassificationSource Source,
    string? Description,
    bool ProvidesPassiveValue)
{
    public static readonly Classification UnknownCaution =
        new(Purpose.Unknown, SafetyRating.Caution, ClassificationSource.Unknown, null, false);
}

/// <summary>
/// One curated KB entry. Matches a real item by publisher + executable (+ optional hints).
/// See ADR-0010 for schema rationale.
/// </summary>
public sealed record KnowledgeEntry(
    string? MatchPublisher,
    string MatchExecutable,
    string? MatchPathPattern,
    string DisplayName,
    string Description,
    Purpose Purpose,
    SafetyRating Safety,
    long? TypicalRamBytes,
    bool ProvidesPassiveValue);

/// <summary>The shipped KB document, versioned (schema + data). Loaded from JSON.</summary>
public sealed record KnowledgeBaseDocument(
    int SchemaVersion,
    string DataVersion,
    IReadOnlyList<KnowledgeEntry> Entries);

/// <summary>Loads and queries the curated Knowledge Base.</summary>
public interface IKnowledgeBase
{
    KnowledgeBaseDocument Document { get; }

    /// <summary>Returns the matching entry for an executable identity, or null.</summary>
    KnowledgeEntry? Match(string executableName, string? publisher, string? fullPath);
}

/// <summary>
/// Produces a <see cref="Classification"/> for an item, combining (in precedence order)
/// user Override → Knowledge Base → heuristic fallback → Unknown/Caution.
/// </summary>
public interface IClassifier
{
    Classification Classify(AutostartEntry entry);
}
