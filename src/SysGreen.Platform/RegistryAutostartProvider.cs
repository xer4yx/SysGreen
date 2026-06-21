using Microsoft.Win32;
using SysGreen.Core.Abstractions;
using SysGreen.Core.Domain;

namespace SysGreen.Platform;

/// <summary>
/// Enumerates Autostart Entries from the HKCU/HKLM Run keys, populating each entry's publisher
/// from its executable's Authenticode signature so the Knowledge Base can match it (ADR-0010).
/// (TODO: also read the Startup folders and reflect the StartupApproved disable flags as state.)
/// </summary>
public sealed class RegistryAutostartProvider : IAutostartProvider
{
    private const string RunSubKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

    private readonly IExecutablePublisherReader _publisherReader;

    public RegistryAutostartProvider(IExecutablePublisherReader publisherReader) =>
        _publisherReader = publisherReader;

    public IReadOnlyList<AutostartEntry> Enumerate()
    {
        var entries = new List<AutostartEntry>();
        ReadRunKey(Registry.CurrentUser, AutostartLocation.RegistryRunCurrentUser, entries);
        ReadRunKey(Registry.LocalMachine, AutostartLocation.RegistryRunLocalMachine, entries);
        return entries;
    }

    private void ReadRunKey(RegistryKey hive, AutostartLocation location, List<AutostartEntry> sink)
    {
        using var key = hive.OpenSubKey(RunSubKey);
        if (key is null) return;

        foreach (var name in key.GetValueNames())
        {
            var command = key.GetValue(name)?.ToString();
            var executablePath = ExtractExecutablePath(command);
            sink.Add(new AutostartEntry(
                Id: $"{location}:{name}",
                DisplayName: name,
                Kind: ItemKind.StartupApp,
                Location: location,
                ExecutablePath: executablePath,
                Publisher: executablePath is null ? null : _publisherReader.ReadPublisher(executablePath),
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
