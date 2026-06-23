using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SysGreen.Core.Usage;

namespace SysGreen.App.ViewModels;

/// <summary>
/// Drives the first-run welcome/consent screen (ADR-0012/0014): plain disclosure that launch tracking
/// is local-only, with an on-by-default off switch. Continuing applies the consent and marks first run
/// complete so the screen never shows again.
/// </summary>
public sealed partial class OnboardingViewModel : ObservableObject
{
    private readonly ITrackingSettings _tracking;
    private readonly IOnboardingState _onboarding;

    /// <summary>Launch tracking is on by default; the user can opt out here (ADR-0012).</summary>
    [ObservableProperty]
    private bool _trackingEnabled = true;

    /// <summary>Raised once the user finishes onboarding, so the App can show the main window.</summary>
    public event Action? Completed;

    public OnboardingViewModel(ITrackingSettings tracking, IOnboardingState onboarding)
    {
        _tracking = tracking;
        _onboarding = onboarding;
    }

    [RelayCommand]
    private void Continue()
    {
        _tracking.SetLaunchTrackingEnabled(TrackingEnabled);
        _onboarding.MarkFirstRunComplete();
        Completed?.Invoke();
    }
}
