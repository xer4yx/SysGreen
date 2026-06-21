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
/// Measured as Private Working Set.
/// </summary>
public sealed record RamEstimate(long? Bytes, RamEstimateSource Source)
{
    public static readonly RamEstimate Unknown = new(null, RamEstimateSource.Unknown);
    public bool IsKnown => Bytes is > 0;
}

/// <summary>Reads existing Windows launch history to seed the habit engine (UserAssist primary).</summary>
public interface IUsageHistoryProvider
{
    IReadOnlyList<UsageRecord> ReadSeedHistory();
}
