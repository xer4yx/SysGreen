using Microsoft.Win32;
using SysGreen.Core.Abstractions;
using SysGreen.Core.Domain;

namespace SysGreen.Platform;

/// <summary>
/// Enumerates Autostart Entries from the HKCU/HKLM Run keys. (TODO: also read the Startup
/// folders and reflect the StartupApproved disable flags as state — ADR-0005.)
/// </summary>
public sealed class RegistryAutostartProvider : IAutostartProvider
{
    private const string RunSubKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

    public IReadOnlyList<AutostartEntry> Enumerate()
    {
        var entries = new List<AutostartEntry>();
        ReadRunKey(Registry.CurrentUser, AutostartLocation.RegistryRunCurrentUser, entries);
        ReadRunKey(Registry.LocalMachine, AutostartLocation.RegistryRunLocalMachine, entries);
        return entries;
    }

    private static void ReadRunKey(RegistryKey hive, AutostartLocation location, List<AutostartEntry> sink)
    {
        using var key = hive.OpenSubKey(RunSubKey);
        if (key is null) return;

        foreach (var name in key.GetValueNames())
        {
            var command = key.GetValue(name)?.ToString();
            sink.Add(new AutostartEntry(
                Id: $"{location}:{name}",
                DisplayName: name,
                Kind: ItemKind.StartupApp,
                Location: location,
                ExecutablePath: ExtractExecutablePath(command),
                Publisher: null, // TODO: read Authenticode publisher for KB matching (ADR-0010)
                State: AutostartState.Enabled)); // TODO: consult StartupApproved flags
        }
    }

    /// <summary>Best-effort extraction of the exe path from a (possibly quoted, arg-bearing) command.</summary>
    private static string? ExtractExecutablePath(string? command)
    {
        if (string.IsNullOrWhiteSpace(command)) return null;
        command = command.Trim();
        if (command.StartsWith('"'))
        {
            var end = command.IndexOf('"', 1);
            return end > 0 ? command[1..end] : command;
        }
        var space = command.IndexOf(' ');
        return space > 0 ? command[..space] : command;
    }
}
