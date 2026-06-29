namespace SysGreen.Core.Usage;

/// <summary>
/// The user's choice of whether the Data Store survives an uninstall (ADR-0017). Read by the
/// Velopack uninstall hook; surfaced as a toggle in the Settings window.
/// </summary>
public interface IDataRetentionSettings
{
    bool KeepDataOnUninstall { get; }
    void SetKeepDataOnUninstall(bool keep);
}
