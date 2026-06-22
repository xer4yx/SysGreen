using Microsoft.Win32;
using SysGreen.Core.Startup;

namespace SysGreen.Platform;

/// <summary>
/// Real HKCU Run registry adapter for SysGreen's own Tray Agent autostart (ADR-0014). Humble object —
/// per-user Run key only (no elevation); the register/unregister decision is the tested
/// <see cref="AgentAutostart"/>.
/// </summary>
public sealed class RunKeyRegistration : IRunKeyRegistration
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

    public bool Exists(string valueName)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey);
        return key?.GetValue(valueName) is not null;
    }

    public void Set(string valueName, string command)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKey, writable: true);
        key.SetValue(valueName, command, RegistryValueKind.String);
    }

    public void Remove(string valueName)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
        key?.DeleteValue(valueName, throwOnMissingValue: false);
    }
}
