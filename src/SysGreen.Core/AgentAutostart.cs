namespace SysGreen.Core.Startup;

/// <summary>
/// Reads/writes SysGreen's own HKCU Run value for the Tray Agent (ADR-0014). Humble registry seam;
/// the decision of when to register/unregister is the tested <see cref="AgentAutostart"/>.
/// </summary>
public interface IRunKeyRegistration
{
    bool Exists(string valueName);
    void Set(string valueName, string command);
    void Remove(string valueName);
}

/// <summary>
/// Keeps SysGreen's own autostart entry for the Tray Agent in step with the launch-tracking setting
/// (ADR-0012/0014). It registers the Run value when tracking is on and the value is absent — but
/// never overwrites an existing one, so a user who disabled the Agent (StartupApproved leaves the Run
/// value in place) isn't fought. Turning tracking off removes the value entirely.
/// </summary>
public sealed class AgentAutostart
{
    private readonly IRunKeyRegistration _registration;

    public AgentAutostart(IRunKeyRegistration registration) => _registration = registration;

    public void Sync(bool trackingEnabled, string valueName, string command)
    {
        var exists = _registration.Exists(valueName);
        if (trackingEnabled && !exists)
            _registration.Set(valueName, command);
        else if (!trackingEnabled && exists)
            _registration.Remove(valueName);
    }
}
