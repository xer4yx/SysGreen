namespace SysGreen.App.Services;

/// <summary>
/// Shows closable in-app toasts for completed-action feedback (Topic C / Phase 7). The view-model
/// depends on this seam only — the WPF overlay lives in the App/View layer — so the outcome UX stays
/// unit-testable (mirrors <see cref="IUpdateService"/>). Success toasts auto-dismiss; errors persist.
/// </summary>
public interface IToastService
{
    void ShowSuccess(string message);
    void ShowError(string message);
}

/// <summary>The do-nothing toast service for callers that don't render toasts (tests, headless use).</summary>
public sealed class NullToastService : IToastService
{
    public static readonly NullToastService Instance = new();
    public void ShowSuccess(string message) { }
    public void ShowError(string message) { }
}
