using System.Diagnostics;
using System.IO;

namespace SysGreen.App.Services;

/// <summary>
/// Launches the Velopack uninstaller (ADR-0017). Velopack runs the app from <c>&lt;root&gt;\current\</c>
/// and keeps <c>Update.exe</c> in <c>&lt;root&gt;</c>; uninstalling is <c>Update.exe --uninstall</c>.
/// A no-op when <c>Update.exe</c> isn't found (a dev run or non-Velopack install), so it's harmless
/// outside a packaged install. The Velopack uninstall hook (Program.cs) then honors the retention choice.
/// </summary>
public sealed class VelopackUninstaller : IAppUninstaller
{
    public void Uninstall()
    {
        var updateExe = UpdateExePathFor(AppContext.BaseDirectory);
        if (updateExe is null || !File.Exists(updateExe)) return; // dev run / not a Velopack install

        var psi = new ProcessStartInfo(updateExe) { UseShellExecute = false };
        psi.ArgumentList.Add("--uninstall"); // Velopack's uninstall command
        Process.Start(psi)?.Dispose(); // fire-and-forget; the updater takes over from here
    }

    /// <summary>
    /// The expected <c>Update.exe</c> path for an app running from <paramref name="appBaseDirectory"/>
    /// (the Velopack <c>current\</c> folder): its parent, the install root. Pure so it can be tested.
    /// </summary>
    public static string? UpdateExePathFor(string appBaseDirectory)
    {
        var root = new DirectoryInfo(appBaseDirectory).Parent;
        return root is null ? null : Path.Combine(root.FullName, "Update.exe");
    }
}
