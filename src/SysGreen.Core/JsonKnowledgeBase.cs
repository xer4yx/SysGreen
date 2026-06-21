using System.Text.Json;
using System.Text.Json.Serialization;

namespace SysGreen.Core.Knowledge;

/// <summary>Loads the curated Knowledge Base from shipped JSON (ADR-0002, ADR-0006, ADR-0010).</summary>
public sealed class JsonKnowledgeBase : IKnowledgeBase
{
    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        Converters = { new JsonStringEnumConverter() },
    };

    public KnowledgeBaseDocument Document { get; }

    public JsonKnowledgeBase(KnowledgeBaseDocument document) => Document = document;

    public static JsonKnowledgeBase LoadFromFile(string path)
    {
        using var stream = File.OpenRead(path);
        var doc = JsonSerializer.Deserialize<KnowledgeBaseDocument>(stream, JsonOptions)
                  ?? new KnowledgeBaseDocument(1, "empty", []);
        return new JsonKnowledgeBase(doc);
    }

    public KnowledgeEntry? Match(string executableName, string? publisher, string? fullPath)
    {
        foreach (var entry in Document.Entries)
        {
            if (!NameMatches(entry.MatchExecutable, executableName)) continue;
            if (entry.MatchPublisher is { } reqPub &&
                !string.Equals(reqPub, publisher, StringComparison.OrdinalIgnoreCase)) continue;
            if (entry.MatchPathPattern is { } pat && fullPath is not null &&
                !fullPath.Contains(pat, StringComparison.OrdinalIgnoreCase)) continue;
            return entry;
        }
        return null;
    }

    private static bool NameMatches(string pattern, string actual)
    {
        actual = actual.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? actual[..^4] : actual;
        pattern = pattern.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? pattern[..^4] : pattern;
        return string.Equals(pattern, actual, StringComparison.OrdinalIgnoreCase);
    }
}
