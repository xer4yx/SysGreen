using System.Windows;
using SysGreen.App.ViewModels;

namespace SysGreen.App;

/// <summary>
/// Interaction logic for SettingsWindow.xaml. Modeless; the view-model is injected (ADR-0011).
/// The destructive Reset action confirms here in the view before invoking the command.
/// </summary>
public partial class SettingsWindow : Window
{
    public SettingsWindow(SettingsViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private void ResetData_Click(object sender, RoutedEventArgs e)
    {
        var confirm = MessageBox.Show(
            this,
            "This permanently clears your history, usage, overrides, and settings on this PC. " +
            "It can't be undone. Continue?",
            "Reset SysGreen data",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (confirm == MessageBoxResult.Yes && DataContext is SettingsViewModel vm)
            vm.ResetDataCommand.Execute(null);
    }
}
