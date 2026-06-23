namespace SysGreen.Core.Usage;

/// <summary>
/// Tracks whether the user has been through the first-run welcome/consent flow (ADR-0014). Stored
/// locally; the App shows onboarding once, until it's marked complete.
/// </summary>
public interface IOnboardingState
{
    bool FirstRunComplete { get; }
    void MarkFirstRunComplete();
}
