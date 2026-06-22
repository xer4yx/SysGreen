using SysGreen.Core.Domain;

namespace SysGreen.Core.Usage;

/// <summary>
/// The habit signal for one executable: launch recency + frequency.
/// Seeded from UserAssist/Prefetch, then extended by the Tray Agent. See ADR-0008.
/// </summary>
public sealed record UsageRecord(
    string ExecutablePath,
    int LaunchCount,
    DateTime? LastLaunchUtc)
{
    public int DaysSinceLastLaunch(DateTime nowUtc) =>
        LastLaunchUtc is { } last ? (int)(nowUtc - last).TotalDays : int.MaxValue;

    /// <summary>An app not launched within the threshold (default 30 days). See CONTEXT.md "Abandoned".</summary>
    public bool IsAbandoned(DateTime nowUtc, int thresholdDays = 30) =>
        DaysSinceLastLaunch(nowUtc) >= thresholdDays;
}

/// <summary>Where a RAM figure came from. Resolved by a degradation chain. See ADR / Q12.</summary>
public enum RamEstimateSource
{
    Unknown = 0,
    Live,            // measured from the running process
    Historical,      // median of Tray Agent samples
    KnowledgeBase,   // coarse typical value
}

/// <summary>
/// The RAM figure shown per item, always framed as "≈ uses", never "saved".
/// Measured as Private Working Set. Resolved by a degradation chain (CONTEXT.md / Q12).
/// </summary>
public sealed record RamEstimate(long? Bytes, RamEstimateSource Source)
{
    public static readonly RamEstimate Unknown = new(null, RamEstimateSource.Unknown);
    public bool IsKnown => Bytes is > 0;

    /// <summary>
    /// Picks the best available figure in priority order: the live Private Working Set if running,
    /// else a historical median (Tray Agent samples), else the KB's coarse typical, else Unknown.
    /// Each source carries its provenance so the UI can show how sure the number is.
    /// </summary>
    public static RamEstimate Resolve(long? live, long? historicalMedian, long? knowledgeBaseTypical) =>
        live is > 0 ? new(live, RamEstimateSource.Live)
        : historicalMedian is > 0 ? new(historicalMedian, RamEstimateSource.Historical)
        : knowledgeBaseTypical is > 0 ? new(knowledgeBaseTypical, RamEstimateSource.KnowledgeBase)
        : Unknown;

    /// <summary>Coarse, human-readable size — whole MB, or one decimal of GB at/above 1 GB.</summary>
    public static string Format(long bytes) =>
        bytes >= 1L << 30 ? $"{bytes / (double)(1L << 30):0.#} GB" : $"{bytes / (1L << 20)} MB";
}

/// <summary>Reads existing Windows launch history to seed the habit engine (UserAssist primary).</summary>
public interface IUsageHistoryProvider
{
    IReadOnlyList<UsageRecord> ReadSeedHistory();
}
