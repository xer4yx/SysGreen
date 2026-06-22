using System.IO;
using SysGreen.Core.Domain;

namespace SysGreen.Core.Knowledge;

/// <summary>
/// Heuristic fallback when the Knowledge Base has no match (ADR-0002). Infers a rough Purpose/Safety
/// from the signer, install path, and name — using only metadata already on the entry, so it stays
/// pure and deterministic. Deliberately conservative on Safety: it promotes only the clearly-safe
/// overhead (background updaters) to Safe, protects security software as Do-Not-Touch, and labels
/// Windows/OEM items while leaving them at Caution so they are never auto-recommended.
/// </summary>
public static class HeuristicClassifier
{
    // Signer/path/name fragments that mark security software — protected, never recommended.
    private static readonly string[] SecurityHints =
    {
        "defender", "mcafee", "norton", "symantec", "avast", "avg ", "kaspersky", "bitdefender",
        "eset", "malwarebytes", "sophos", "trend micro", "webroot", "windows security",
    };

    // Signers that mark device-maker / driver software — labelled, but kept at Caution.
    private static readonly string[] OemVendors =
    {
        "lenovo", "dell", "hewlett-packard", "hp inc", "asus", "acer", "realtek", "intel",
        "nvidia", "advanced micro devices", "synaptics", "logitech",
    };

    public static Classification? Classify(AutostartEntry entry)
    {
        var publisher = (entry.Publisher ?? "").ToLowerInvariant();
        var path = (entry.ExecutablePath ?? "").ToLowerInvariant();
        var fileName = entry.ExecutablePath is { Length: > 0 } p
            ? Path.GetFileNameWithoutExtension(p).ToLowerInvariant() : "";
        var display = entry.DisplayName.ToLowerInvariant();

        // Order matters: protect security first; keep Windows components conservative before the
        // updater rule could promote a Windows-Update component to Safe.
        if (ContainsAny(SecurityHints, publisher, path, display))
            return Heuristic(Purpose.Security, SafetyRating.DoNotTouch, "Security software");

        if (IsUnderWindows(path))
            return Heuristic(Purpose.WindowsCore, SafetyRating.Caution, "Windows component");

        if (IsUpdater(entry, fileName, display, path))
            return Heuristic(Purpose.Updater, SafetyRating.Safe, "Background updater");

        if (ContainsAny(OemVendors, publisher))
            return Heuristic(Purpose.OemDriver, SafetyRating.Caution, "Device-maker software");

        return null; // nothing confident — caller falls back to Unknown/Caution
    }

    private static bool IsUnderWindows(string path) => path.Contains(@":\windows\");

    private static bool IsUpdater(AutostartEntry entry, string fileName, string display, string path) =>
        entry.TargetExecutable is null // a Squirrel launcher fronts a real app — not itself an updater
        && (fileName.Contains("update") || display.Contains("update") || path.Contains("clicktorun"));

    private static bool ContainsAny(string[] needles, params string[] haystacks) =>
        haystacks.Any(h => needles.Any(h.Contains));

    private static Classification Heuristic(Purpose purpose, SafetyRating safety, string description) =>
        new(purpose, safety, ClassificationSource.Heuristic, description, ProvidesPassiveValue: false);
}
