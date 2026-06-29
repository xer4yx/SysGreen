using NSubstitute;
using SysGreen.App.Services;
using SysGreen.App.ViewModels;
using SysGreen.Core.Usage;

namespace SysGreen.Tests;

public class OnboardingViewModelTests
{
    private static IPolicyProvider Policy(int version = 1, string text = "policy")
    {
        var p = Substitute.For<IPolicyProvider>();
        p.CurrentVersion.Returns(version);
        p.Text.Returns(text);
        return p;
    }

    private static IOnboardingState Onboarding(bool firstRunComplete = false)
    {
        var o = Substitute.For<IOnboardingState>();
        o.FirstRunComplete.Returns(firstRunComplete);
        return o;
    }

    [Fact]
    public void First_run_applies_the_tracking_consent_and_completes_first_run()
    {
        var tracking = Substitute.For<ITrackingSettings>();
        var onboarding = Onboarding(firstRunComplete: false);
        var vm = new OnboardingViewModel(tracking, onboarding, Substitute.For<IPolicyAcceptance>(), Policy())
        {
            TrackingEnabled = true,
        };

        vm.ContinueCommand.Execute(null);

        tracking.Received(1).SetLaunchTrackingEnabled(true);
        onboarding.Received(1).MarkFirstRunComplete();
    }

    [Fact]
    public void First_run_with_tracking_declined_turns_tracking_off()
    {
        var tracking = Substitute.For<ITrackingSettings>();
        var vm = new OnboardingViewModel(tracking, Onboarding(), Substitute.For<IPolicyAcceptance>(), Policy())
        {
            TrackingEnabled = false,
        };

        vm.ContinueCommand.Execute(null);

        tracking.Received(1).SetLaunchTrackingEnabled(false);
    }

    [Fact]
    public void Continuing_records_acceptance_of_the_current_policy_version()
    {
        var acceptance = Substitute.For<IPolicyAcceptance>();
        var vm = new OnboardingViewModel(
            Substitute.For<ITrackingSettings>(), Onboarding(), acceptance, Policy(version: 4));

        vm.ContinueCommand.Execute(null);

        acceptance.Received(1).SetAcceptedPolicyVersion(4);
    }

    [Fact]
    public void A_returning_user_re_accepting_does_not_reset_tracking_or_first_run()
    {
        var tracking = Substitute.For<ITrackingSettings>();
        var onboarding = Onboarding(firstRunComplete: true); // already onboarded; this is a version-bump re-prompt
        var acceptance = Substitute.For<IPolicyAcceptance>();
        var vm = new OnboardingViewModel(tracking, onboarding, acceptance, Policy(version: 2));

        vm.ContinueCommand.Execute(null);

        acceptance.Received(1).SetAcceptedPolicyVersion(2);     // still records the new acceptance
        tracking.DidNotReceive().SetLaunchTrackingEnabled(Arg.Any<bool>()); // but leaves tracking alone
        onboarding.DidNotReceive().MarkFirstRunComplete();
    }

    [Fact]
    public void Tracking_consent_shows_only_on_the_first_run()
    {
        Assert.True(new OnboardingViewModel(
            Substitute.For<ITrackingSettings>(), Onboarding(firstRunComplete: false),
            Substitute.For<IPolicyAcceptance>(), Policy()).IsFirstRun);
        Assert.False(new OnboardingViewModel(
            Substitute.For<ITrackingSettings>(), Onboarding(firstRunComplete: true),
            Substitute.For<IPolicyAcceptance>(), Policy()).IsFirstRun);
    }

    [Fact]
    public void Continue_signals_completion_so_the_app_can_show_the_main_window()
    {
        var vm = new OnboardingViewModel(
            Substitute.For<ITrackingSettings>(), Onboarding(), Substitute.For<IPolicyAcceptance>(), Policy());
        var completed = false;
        vm.Completed += () => completed = true;

        vm.ContinueCommand.Execute(null);

        Assert.True(completed);
    }
}
