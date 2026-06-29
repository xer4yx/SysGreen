using System.Text.Json.Serialization;

namespace SysGreen.Core.Domain;

/// <summary>The kind of a <see cref="ManageableItem"/>. See ADR-0001 / CONTEXT.md.</summary>
public enum ItemKind
{
    /// <summary>Auto-launched program (Run keys, Startup folder, logon scheduled task).</summary>
    StartupApp,
    /// <summary>UWP / Store app allowed to run in the background.</summary>
    BackgroundApp,
    /// <summary>Task Scheduler entry.</summary>
    ScheduledTask,
    /// <summary>A strict Windows Service (deferred, higher-risk tier).</summary>
    Service,
    /// <summary>A live runtime instance.</summary>
    Process,
}

/// <summary>What an item is for. Fixed taxonomy; distinct from <see cref="SafetyRating"/>.</summary>
public enum Purpose
{
    Unknown = 0,
    Gaming,
    Media,
    Communication,
    Productivity,
    Updater,
    OemDriver,
    WindowsTelemetry,
    WindowsCore,
    Security,
}

/// <summary>How risky it is to disable an item. The axis the recommender gates on.</summary>
public enum SafetyRating
{
    /// <summary>Unknown items default here and are never auto-recommended.</summary>
    Caution = 0,
    Safe,
    DoNotTouch,
    RequiredForBoot,
}

/// <summary>Current enable/disable state of an Autostart Entry.</summary>
public enum AutostartState
{
    Unknown = 0,
    Enabled,
    Disabled,
}

/// <summary>Where an Autostart Entry physically lives. Drives elevation + mechanism.</summary>
public enum AutostartLocation
{
    Unknown = 0,
    RegistryRunCurrentUser,   // HKCU\...\Run            (no elevation)
    RegistryRunLocalMachine,  // HKLM\...\Run            (elevation)
    StartupFolderCurrentUser, // %AppData%\...\Startup    (no elevation)
    StartupFolderCommon,      // %ProgramData%\...\Startup (elevation)
    ScheduledTask,
    Service,
    BackgroundApp,            // UWP background access, HKCU BackgroundAccessApplications (no elevation)
}

/// <summary>A live runtime instance consuming RAM now. Transient; acted on with End Task.</summary>
public sealed record ProcessInfo(
    int Pid,
    string Name,
    string? ExecutablePath,
    long PrivateWorkingSetBytes);

/// <summary>
/// The configuration that causes something to launch at boot/logon. Persistent;
/// acted on with Disable/Enable. Linked to a <see cref="ProcessInfo"/> by app identity.
/// </summary>
public sealed record AutostartEntry(
    string Id,
    string DisplayName,
    ItemKind Kind,
    AutostartLocation Location,
    string? ExecutablePath,
    string? Publisher,
    AutostartState State)
{
    /// <summary>
    /// For Squirrel-style launchers (e.g. Discord's <c>Update.exe --processStart Discord.exe</c>),
    /// the real application executable name. Null for ordinary entries. Used for KB matching.
    /// </summary>
    public string? TargetExecutable { get; init; }

    private readonly string? _mechanismKey;

    /// <summary>
    /// The key the disable mechanism uses to address this entry: the Run value name (or shortcut
    /// file name like "Spotify.lnk") under the StartupApproved key, or the full task path for a
    /// scheduled task. Captured on each Change Record so a reversal targets the same key.
    /// Defaults to <see cref="DisplayName"/>.
    /// </summary>
    public string MechanismKey
    {
        get => _mechanismKey ?? DisplayName;
        init => _mechanismKey = string.IsNullOrEmpty(value) ? null : value;
    }

    /// <summary>True when changing this entry's state requires admin elevation.</summary>
    [JsonIgnore]
    public bool RequiresElevation => Location is
        AutostartLocation.RegistryRunLocalMachine or
        AutostartLocation.StartupFolderCommon or
        AutostartLocation.ScheduledTask or
        AutostartLocation.Service;
}

/// <summary>
/// The unified, user-facing view: an optional Autostart Entry correlated with an
/// optional live Process, plus classification and the RAM estimate.
/// </summary>
public sealed record ManageableItem(
    string Id,
    string DisplayName,
    ItemKind Kind,
    AutostartEntry? Autostart,
    ProcessInfo? RunningProcess,
    Purpose Purpose,
    SafetyRating Safety,
    long? RamEstimateBytes)
{
    public bool IsRunning => RunningProcess is not null;
    public bool CanDisable => Autostart is { State: AutostartState.Enabled };
    public bool CanEnable => Autostart is { State: AutostartState.Disabled };
}
