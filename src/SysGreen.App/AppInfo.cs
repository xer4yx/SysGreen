using System.Reflection;
using SysGreen.Core;

namespace SysGreen.App;

/// <summary>
/// App-global facts read once from the running assembly. The display version is derived from
/// <see cref="AssemblyInformationalVersionAttribute"/> — stamped from &lt;Version&gt; in
/// Directory.Build.props (ADR-0015) — via the unit-tested <see cref="AppVersion.Format"/>.
/// The reflection read here is the humble adapter around that tested logic.
/// </summary>
internal static class AppInfo
{
    public static string DisplayVersion { get; } = AppVersion.Format(
        Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion);
}
