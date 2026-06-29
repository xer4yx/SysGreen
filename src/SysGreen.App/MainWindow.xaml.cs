using System;
using System.Windows;
using SysGreen.App.ViewModels;

namespace SysGreen.App;

/// <summary>
/// Interaction logic for MainWindow.xaml. The view-model is injected (ADR-0011); the Settings window
/// is created on demand from an injected factory so it gets a fresh, DI-composed view-model.
/// </summary>
public partial class MainWindow : Window
{
    private readonly Func<SettingsViewModel> _settingsViewModel;

    public MainWindow(MainViewModel viewModel, Func<SettingsViewModel> settingsViewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        _settingsViewModel = settingsViewModel;
    }

    private void OpenSettings_Click(object sender, RoutedEventArgs e) =>
        new SettingsWindow(_settingsViewModel()) { Owner = this }.Show(); // modeless (grilling Q3)
}
