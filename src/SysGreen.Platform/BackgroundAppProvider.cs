using Microsoft.Win32;
using SysGreen.Core.Abstractions;
using SysGreen.Core.Domain;
using SysGreen.Core.Knowledge;

namespace SysGreen.Platform;

/// <summary>
/// Enumerates UWP apps allowed to run in the background (ADR-0005) from HKCU\…\BackgroundAccess
/// Applications — each subkey is a package family name. Surfaces them as BackgroundApp entries with
/// their real allowed/blocked state, addressed by family name for disable. Humble object; the
/// background-access flag itself is the tested mechanism.
/// </summary>
public sealed class BackgroundAppProvider : IAutostartProvider
{
    private const string BasePath =
        @"Software\Microsoft\Windows\CurrentVersion\BackgroundAccessApplications";

    public IReadOnlyList<AutostartEntry> Enumerate()
    {
        var entries = new List<AutostartEntry>();
        using var baseKey = Registry.CurrentUser.OpenSubKey(BasePath);
        if (baseKey is null) return entries;

        foreach (var familyName in baseKey.GetSubKeyNames())
        {
            using var sub = baseKey.OpenSubKey(familyName);
            var blocked = sub?.GetValue("Disabled") is int d && d == 1;
            entries.Add(new AutostartEntry(
                Id: $"{AutostartLocation.BackgroundApp}:{familyName}",
                DisplayName: PackagedApp.NameFromFamily(familyName),
                Kind: ItemKind.BackgroundApp,
                Location: AutostartLocation.BackgroundApp,
                ExecutablePath: null,
                Publisher: null,
                State: blocked ? AutostartState.Disabled : AutostartState.Enabled)
            {
                MechanismKey = familyName, // the family name addresses the background-access flag
            });
        }
        return entries;
    }
}
