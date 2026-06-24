using Microsoft.Win32;
using SysGreen.Core.Abstractions;

namespace SysGreen.Platform;

/// <summary>
/// Real registry adapter for UWP background access (ADR-0005). Each package family name is a subkey of
/// HKCU\…\BackgroundAccessApplications with a <c>Disabled</c> DWORD (1 = the user blocked background).
/// Humble object — reads/writes that flag only; non-elevated (HKCU).
/// </summary>
public sealed class BackgroundAppRegistryStore : IBackgroundAppStore
{
    private const string BasePath =
        @"Software\Microsoft\Windows\CurrentVersion\BackgroundAccessApplications";

    public bool IsEnabled(string packageFamilyName)
    {
        using var key = Registry.CurrentUser.OpenSubKey($@"{BasePath}\{packageFamilyName}");
        // Allowed unless there's an explicit Disabled = 1.
        return key?.GetValue("Disabled") is not int disabled || disabled == 0;
    }

    public void SetEnabled(string packageFamilyName, bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey($@"{BasePath}\{packageFamilyName}", writable: true);
        key.SetValue("Disabled", enabled ? 0 : 1, RegistryValueKind.DWord);
    }
}
