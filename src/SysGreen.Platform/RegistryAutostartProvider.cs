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
            var (launcher, target) = LaunchCommand.Parse(command);
            sink.Add(new AutostartEntry(
                Id: $"{location}:{name}",
                DisplayName: name,
                Kind: ItemKind.StartupApp,
                Location: location,
                ExecutablePath: launcher,
                Publisher: launcher is null ? null : _publisherReader.ReadPublisher(launcher),
                State: AutostartState.Enabled) // TODO: consult StartupApproved flags
            {
                TargetExecutable = target,
            });
        }
    }
}
