using Microsoft.Win32;
using SysGreen.Core.Abstractions;
using SysGreen.Core.Domain;

namespace SysGreen.Platform;

/// <summary>
/// Real registry adapter for the StartupApproved flags. Maps an <see cref="AutostartLocation"/>
/// to its StartupApproved subkey under HKCU/HKLM and reads/writes the REG_BINARY value.
/// Humble object — no logic beyond the mapping; the decision logic lives in the tested
/// <see cref="StartupApprovedItemController"/> (ADR-0011).
/// </summary>
public sealed class StartupApprovedRegistryStore : IStartupApprovedStore
{
    private const string RunPath =
        @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";
    private const string FolderPath =
        @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\StartupFolder";

    public byte[]? ReadFlag(AutostartLocation location, string valueName)
    {
        var (hive, path) = Map(location);
        using var key = hive.OpenSubKey(path);
        return key?.GetValue(valueName) as byte[];
    }

    public void WriteFlag(AutostartLocation location, string valueName, byte[] data)
    {
        var (hive, path) = Map(location);
        using var key = hive.CreateSubKey(path, writable: true);
        key.SetValue(valueName, data, RegistryValueKind.Binary);
    }

    private static (RegistryKey Hive, string Path) Map(AutostartLocation location) => location switch
    {
        AutostartLocation.RegistryRunCurrentUser   => (Registry.CurrentUser, RunPath),
        AutostartLocation.RegistryRunLocalMachine  => (Registry.LocalMachine, RunPath),
        AutostartLocation.StartupFolderCurrentUser => (Registry.CurrentUser, FolderPath),
        AutostartLocation.StartupFolderCommon      => (Registry.LocalMachine, FolderPath),
        _ => throw new NotSupportedException($"StartupApproved flags do not apply to {location}."),
    };
}
