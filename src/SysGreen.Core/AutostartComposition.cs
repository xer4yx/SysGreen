using System.IO;
using SysGreen.Core.Abstractions;
using SysGreen.Core.Domain;

namespace SysGreen.Core.Startup;

/// <summary>
/// Presents several autostart sources (Run keys, Startup folders, …) as one provider, so the rest
/// of the app sees a single list (ADR-0005). Pure aggregation; order is preserved.
/// </summary>
public sealed class CompositeAutostartProvider : IAutostartProvider
{
    private readonly IReadOnlyList<IAutostartProvider> _providers;

    public CompositeAutostartProvider(params IAutostartProvider[] providers) => _providers = providers;

    public IReadOnlyList<AutostartEntry> Enumerate() =>
        _providers.SelectMany(p => p.Enumerate()).ToList();
}

/// <summary>
/// Builds the Autostart Entry for a Startup-folder shortcut. The display name drops the ".lnk"
/// extension, but the StartupApproved flag is keyed by the shortcut <em>file</em> name — the value
/// Windows actually writes — so disabling/reflecting state targets the right key.
/// </summary>
public static class StartupFolderEntry
{
    public static AutostartEntry Create(
        string shortcutPath, AutostartLocation location, string? targetPath, string? publisher)
    {
        var fileName = Path.GetFileName(shortcutPath);
        return new AutostartEntry(
            Id: $"{location}:{fileName}",
            DisplayName: Path.GetFileNameWithoutExtension(shortcutPath),
            Kind: ItemKind.StartupApp,
            Location: location,
            ExecutablePath: targetPath,
            Publisher: publisher,
            State: AutostartState.Enabled) // the StartupApproved decorator resolves the real state
        {
            StartupApprovedValueName = fileName,
        };
    }
}
