using System.IO;
using SysGreen.Core.Abstractions;
using SysGreen.Core.Domain;
using SysGreen.Core.Startup;

namespace SysGreen.Platform;

/// <summary>
/// Enumerates Autostart Entries from the per-user and common Startup folders (ADR-0005), resolving
/// each shortcut's target so the Knowledge Base can classify it. Humble object — the directory scan
/// and the COM shortcut resolution live here; the entry shape is the tested
/// <see cref="StartupFolderEntry"/>. The real enable/disable state is layered on by the
/// StartupApproved decorator, so entries are reported Enabled here.
/// </summary>
public sealed class StartupFolderAutostartProvider : IAutostartProvider
{
    private readonly IExecutablePublisherReader _publisherReader;

    public StartupFolderAutostartProvider(IExecutablePublisherReader publisherReader) =>
        _publisherReader = publisherReader;

    public IReadOnlyList<AutostartEntry> Enumerate()
    {
        var entries = new List<AutostartEntry>();
        Scan(Environment.GetFolderPath(Environment.SpecialFolder.Startup),
            AutostartLocation.StartupFolderCurrentUser, entries);
        Scan(Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup),
            AutostartLocation.StartupFolderCommon, entries);
        return entries;
    }

    private void Scan(string folder, AutostartLocation location, List<AutostartEntry> sink)
    {
        if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder)) return;

        foreach (var shortcut in Directory.EnumerateFiles(folder, "*.lnk"))
        {
            var target = ResolveShortcutTarget(shortcut);
            var publisher = target is null ? null : _publisherReader.ReadPublisher(target);
            sink.Add(StartupFolderEntry.Create(shortcut, location, target, publisher));
        }
    }

    /// <summary>
    /// Reads a shortcut's target path via the Windows Script Host shell (COM). Best-effort: a
    /// shortcut we can't resolve still enumerates (classified Unknown), it just has no target.
    /// </summary>
    private static string? ResolveShortcutTarget(string shortcutPath)
    {
        try
        {
            var shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType is null) return null;

            dynamic shell = Activator.CreateInstance(shellType)!;
            dynamic link = shell.CreateShortcut(shortcutPath);
            string target = link.TargetPath;
            return string.IsNullOrWhiteSpace(target) ? null : target;
        }
        catch
        {
            return null;
        }
    }
}
