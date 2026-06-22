namespace SysGreen.Core.Domain;

/// <summary>
/// Parses a Run-key launch command into the launcher executable and, for Squirrel-style
/// updaters (e.g. Discord's <c>Update.exe --processStart Discord.exe</c>), the real target
/// executable. The launcher path is what gets signature-checked; the target name is what the
/// Knowledge Base should match against (ADR-0010).
/// </summary>
public static class LaunchCommand
{
    private const string ProcessStartFlag = "--processStart";

    public static (string? LauncherPath, string? TargetExecutable) Parse(string? command)
    {
        if (string.IsNullOrWhiteSpace(command)) return (null, null);
        command = command.Trim();
        return (ExtractFirstToken(command), ExtractProcessStartTarget(command));
    }

    /// <summary>
    /// The leading executable token: a quoted path verbatim, otherwise up to and including the
    /// first <c>.exe</c> (so unquoted paths with spaces like "C:\Program Files\..." survive),
    /// falling back to the first space.
    /// </summary>
    private static string ExtractFirstToken(string command)
    {
        if (command.StartsWith('"'))
        {
            var end = command.IndexOf('"', 1);
            return end > 0 ? command[1..end] : command[1..];
        }

        var exe = command.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
        if (exe >= 0) return command[..(exe + ".exe".Length)];

        var space = command.IndexOf(' ');
        return space > 0 ? command[..space] : command;
    }

    private static string? ExtractProcessStartTarget(string command)
    {
        var idx = command.IndexOf(ProcessStartFlag, StringComparison.OrdinalIgnoreCase);
        if (idx < 0) return null;

        var rest = command[(idx + ProcessStartFlag.Length)..].TrimStart(' ', '=');
        if (rest.Length == 0) return null;
        return ExtractFirstToken(rest);
    }
}
