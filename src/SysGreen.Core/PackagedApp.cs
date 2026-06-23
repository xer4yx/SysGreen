namespace SysGreen.Core.Knowledge;

/// <summary>
/// Identifies MSIX/Store-packaged apps from their app-execution-alias path under WindowsApps
/// (ADR-0010). The exe stub there can't be read for an Authenticode publisher, so the stable
/// identity is the **package name** embedded in the path
/// (<c>…\WindowsApps\MSTeams_8wekyb3d8bbwe\ms-teams.exe</c> → <c>MSTeams</c>) — the part of the
/// package family name before its publisher hash. Used as a Knowledge Base match candidate.
/// </summary>
public static class PackagedApp
{
    public static string? PackageName(string? executablePath)
    {
        if (string.IsNullOrEmpty(executablePath)) return null;

        var parts = executablePath.Split('\\', '/');
        var i = Array.FindIndex(parts, p => string.Equals(p, "WindowsApps", StringComparison.OrdinalIgnoreCase));
        if (i < 0 || i + 1 >= parts.Length) return null;

        // The next segment is the package family name: "<package name>_<publisher hash>".
        var familyName = parts[i + 1];
        var hash = familyName.LastIndexOf('_');
        var name = hash > 0 ? familyName[..hash] : familyName;
        return string.IsNullOrEmpty(name) ? null : name;
    }
}
