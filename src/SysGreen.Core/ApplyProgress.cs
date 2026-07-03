using System.Text.Json;
using System.Text.Json.Serialization;

namespace SysGreen.Core.Apply;

/// <summary>The coarse phase an Apply batch is in, for the header progress strip (Topic B / Phase 6).
/// The restore point is indeterminate, so there is no true percentage — just these stages.</summary>
public enum ApplyStage
{
    /// <summary>Creating the mandatory System Restore point before a risky batch (ADR-0005).</summary>
    CreatingRestorePoint,
    /// <summary>Committing change <see cref="ApplyProgress.Current"/> of <see cref="ApplyProgress.Total"/>.</summary>
    Applying,
    /// <summary>Every change has been committed.</summary>
    Done,
}

/// <summary>
/// One progress update from an Apply batch: the stage plus, while <see cref="ApplyStage.Applying"/>,
/// which change (1-based <see cref="Current"/>) of how many (<see cref="Total"/>) is being committed.
/// <see cref="Current"/> is 0 during the restore-point phase.
/// </summary>
public sealed record ApplyProgress(ApplyStage Stage, int Current, int Total);

/// <summary>
/// Receives progress from an Apply batch (Topic B). A no-op for in-process per-user applies; a file
/// writer in the elevated Helper, whose file the App polls (ADR-0011).
/// </summary>
public interface IApplyProgressSink
{
    void Report(ApplyProgress progress);
}

/// <summary>The default do-nothing sink for callers that don't observe progress (in-process applies
/// and every test that predates Phase 6).</summary>
public sealed class NullApplyProgressSink : IApplyProgressSink
{
    public static readonly NullApplyProgressSink Instance = new();
    public void Report(ApplyProgress progress) { }
}

/// <summary>
/// Writes each Apply progress update to a small JSON file the App polls (Topic B / ADR-0011). Used by
/// the elevated Helper. Writes are best-effort: progress is telemetry, so an I/O hiccup never fails
/// the Apply. The write is staged through a temp sibling then moved over, so a poller never sees a
/// half-written file.
/// </summary>
public sealed class FileApplyProgressSink : IApplyProgressSink
{
    private readonly string _path;

    public FileApplyProgressSink(string path) => _path = path;

    public void Report(ApplyProgress progress)
    {
        try
        {
            var tmp = _path + ".tmp";
            File.WriteAllText(tmp, ApplyProgressFile.Serialize(progress));
            File.Move(tmp, _path, overwrite: true);
        }
        catch { /* progress is best-effort telemetry; never fail an Apply over it */ }
    }
}

/// <summary>JSON (de)serialization plus a tolerant reader for the Apply progress file (Topic B).</summary>
public static class ApplyProgressFile
{
    // Enum-as-string so the file stays human-readable for support, matching the job/result files.
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public static string Serialize(ApplyProgress progress) => JsonSerializer.Serialize(progress, Options);

    /// <summary>The latest progress, or null when the file is absent, empty, or caught mid-write —
    /// all of which the poller simply skips and retries on its next tick.</summary>
    public static ApplyProgress? TryRead(string path)
    {
        try
        {
            if (!File.Exists(path)) return null;
            var json = File.ReadAllText(path);
            return string.IsNullOrWhiteSpace(json)
                ? null
                : JsonSerializer.Deserialize<ApplyProgress>(json, Options);
        }
        catch
        {
            return null;
        }
    }
}
