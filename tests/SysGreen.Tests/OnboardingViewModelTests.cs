using NSubstitute;
using SysGreen.App.ViewModels;
using SysGreen.Core.Usage;

namespace SysGreen.Tests;

public class OnboardingViewModelTests
{
    [Fact]
    public void Continue_applies_the_tracking_consent_and_completes_first_run()
    {
        var tracking = Substitute.For<ITrackingSettings>();
        var onboarding = Substitute.For<IOnboardingState>();
        var vm = new OnboardingViewModel(tracking, onboarding) { TrackingEnabled = true };

        vm.ContinueCommand.Execute(null);

        tracking.Received(1).SetLaunchTrackingEnabled(true);
        onboarding.Received(1).MarkFirstRunComplete();
    }

    [Fact]
    public void Continue_with_tracking_declined_turns_tracking_off()
    {
        var tracking = Substitute.For<ITrackingSettings>();
        var vm = new OnboardingViewModel(tracking, Substitute.For<IOnboardingState>()) { TrackingEnabled = false };

        vm.ContinueCommand.Execute(null);

        tracking.Received(1).SetLaunchTrackingEnabled(false);
    }

    [Fact]
    public void Continue_signals_completion_so_the_app_can_show_the_main_window()
    {
        var vm = new OnboardingViewModel(Substitute.For<ITrackingSettings>(), Substitute.For<IOnboardingState>());
        var completed = false;
        vm.Completed += () => completed = true;

        vm.ContinueCommand.Execute(null);

        Assert.True(completed);
    }
}
