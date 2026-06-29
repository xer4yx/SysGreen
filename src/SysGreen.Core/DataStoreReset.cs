namespace SysGreen.Core.Usage;

/// <summary>
/// Clears everything the user has accumulated in the Data Store — Usage, Change Records, Overrides,
/// and settings — in place, without deleting the file or restarting. The clean-slate path offered in
/// Settings (the in-app counterpart to a delete-on-uninstall, ADR-0017).
/// </summary>
public interface IDataStoreReset
{
    void Reset();
}
