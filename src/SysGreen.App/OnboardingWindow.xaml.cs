using System.Windows;

namespace SysGreen.App;

/// <summary>The first-run welcome/consent screen (ADR-0012/0014). DataContext is an OnboardingViewModel.</summary>
public partial class OnboardingWindow : Window
{
    public OnboardingWindow() => InitializeComponent();
}
