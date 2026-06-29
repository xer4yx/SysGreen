using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SysGreen.App.Services;
using SysGreen.Core.Usage;

namespace SysGreen.App.ViewModels;

/// <summary>
/// Drives the first-run / policy-acceptance gate (ADR-0014/0018). Always presents the Privacy Policy
/// &amp; Terms and records acceptance of the current version. On the very first run it also shows the
/// launch-tracking consent (ADR-0012) and marks first-run complete; a later version-bump re-prompt
/// shows only the policy and does <b>not</b> touch the tracking preference.
/// </summary>
public sealed partial class OnboardingViewModel : ObservableObject
{
    private readonly ITrackingSettings _tracking;
    private readonly IOnboardingState _onboarding;
    private readonly IPolicyAcceptance _acceptance;
    private readonly IPolicyProvider _policy;

    /// <summary>Launch tracking is on by default; the user can opt out here on first run (ADR-0012).</summary>
    [ObservableProperty]
    private bool _trackingEnabled = true;

    /// <summary>True only on the very first run — the tracking consent shows only then.</summary>
    public bool IsFirstRun { get; }

    /// <summary>The Privacy Policy &amp; Terms text to display and accept.</summary>
    public string PolicyText => _policy.Text;

    /// <summary>Raised once the user accepts, so the App can show the main window.</summary>
    public event Action? Completed;

    public OnboardingViewModel(
        ITrackingSettings tracking, IOnboardingState onboarding,
        IPolicyAcceptance acceptance, IPolicyProvider policy)
    {
        _tracking = tracking;
        _onboarding = onboarding;
        _acceptance = acceptance;
        _policy = policy;
        IsFirstRun = !onboarding.FirstRunComplete;
    }

    [RelayCommand]
    private void Continue()
    {
        // Always record acceptance of the version just shown (ADR-0018).
        _acceptance.SetAcceptedPolicyVersion(_policy.CurrentVersion);

        // The tracking consent and first-run flag are a first-run-only concern; a version-bump
        // re-prompt must not reset the user's existing tracking choice (ADR-0012/0018).
        if (IsFirstRun)
        {
            _tracking.SetLaunchTrackingEnabled(TrackingEnabled);
            _onboarding.MarkFirstRunComplete();
        }

        Completed?.Invoke();
    }
}
