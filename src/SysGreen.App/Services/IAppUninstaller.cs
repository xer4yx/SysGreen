namespace SysGreen.App.Services;

/// <summary>
/// Triggers SysGreen's own uninstall (ADR-0017). Abstracted so the Settings view-model can drive the
/// in-app "Uninstall" action without referencing Velopack, and so it can be faked in tests (the real
/// implementation launches the Velopack uninstaller, which must never run during a test).
/// </summary>
public interface IAppUninstaller
{
    void Uninstall();
}
