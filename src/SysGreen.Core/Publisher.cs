namespace SysGreen.Core.Knowledge;

/// <summary>
/// Normalizes a publisher string for Knowledge Base matching. Authenticode subjects come back as
/// X.500 distinguished names (e.g. "CN=Spotify AB, O=Spotify AB, L=Stockholm, C=SE"), while the KB
/// stores the plain common name ("Spotify AB"). This extracts the CN so the two can be compared.
/// </summary>
public static class PublisherName
{
    public static string? Normalize(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return raw is null ? null : raw.Trim();

        var cn = raw.IndexOf("CN=", StringComparison.OrdinalIgnoreCase);
        if (cn < 0) return raw.Trim();

        var rest = raw[(cn + 3)..].TrimStart();
        if (rest.StartsWith('"'))
        {
            var close = rest.IndexOf('"', 1);
            return (close > 0 ? rest[1..close] : rest[1..]).Trim();
        }

        var comma = rest.IndexOf(',');
        return (comma >= 0 ? rest[..comma] : rest).Trim();
    }
}
