using System.Text.Json;
using System.Text.Json.Serialization;

namespace SysGreen.Core.Apply;

/// <summary>
/// The serialized batch the App hands to the elevated Helper over a temp job file (ADR-0011).
/// Self-contained: it carries the shared database path and where the Helper should write its
/// result, so the elevated process needs no other configuration or shared state.
/// </summary>
public sealed record ApplyJob(
    int Version,
    string DatabasePath,
    string ResultPath,
    IReadOnlyList<PendingChange> Changes);

/// <summary>JSON (de)serialization for the App↔Helper job + result files (ADR-0011).</summary>
public static class ApplyJobSerializer
{
    public const int CurrentVersion = 1;

    // Enums as strings + indented: the temp files stay human-readable for support/debugging.
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public static string SerializeJob(ApplyJob job) => JsonSerializer.Serialize(job, Options);

    public static ApplyJob DeserializeJob(string json) =>
        JsonSerializer.Deserialize<ApplyJob>(json, Options)
        ?? throw new InvalidDataException("Job file was empty or not a SysGreen apply job.");

    public static string SerializeResult(ApplyResult result) => JsonSerializer.Serialize(result, Options);

    public static ApplyResult DeserializeResult(string json) =>
        JsonSerializer.Deserialize<ApplyResult>(json, Options)
        ?? throw new InvalidDataException("Result file was empty or not a SysGreen apply result.");
}
