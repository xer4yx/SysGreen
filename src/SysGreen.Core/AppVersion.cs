namespace SysGreen.Core;

/// <summary>
/// Formats the assembly's <c>AssemblyInformationalVersion</c> into the short string shown in the UI.
/// The single source of truth for the version is &lt;Version&gt; in Directory.Build.props (ADR-0015);
/// the build stamps it onto the informational version, which the App reads at runtime.
/// Any build metadata after '+' (the git commit the .NET SDK appends in a repo, or a future automated
/// stamp) is dropped, leaving just the semantic version prefixed with 'v'.
/// </summary>
public static class AppVersion
{
    public static string Format(string? informationalVersion)
    {
        if (string.IsNullOrWhiteSpace(informationalVersion)) return "";

        var version = informationalVersion.Trim();
        var plus = version.IndexOf('+');
        if (plus >= 0) version = version[..plus];

        return version.Length == 0 ? "" : $"v{version}";
    }
}
