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

/// <summary>
/// Records which version of the Privacy Policy &amp; Terms the user has accepted (ADR-0018). The
/// acceptance gate re-appears only when the current policy version exceeds the accepted one — not on
/// every install or update. Defaults to 0 (nothing accepted), so a fresh or wiped store always prompts.
/// </summary>
public interface IPolicyAcceptance
{
    int AcceptedPolicyVersion { get; }
    void SetAcceptedPolicyVersion(int version);
}
