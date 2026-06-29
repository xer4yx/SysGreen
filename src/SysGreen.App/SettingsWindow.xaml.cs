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

    private void Uninstall_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not SettingsViewModel vm) return;

        // Present the keep/delete choice in a surface we control (ADR-0017 option C):
        // Yes = uninstall and keep data, No = uninstall and delete data, Cancel = stay installed.
        var choice = MessageBox.Show(
            this,
            "This will uninstall SysGreen from this PC.\n\n" +
            "Keep your saved data (history and settings)?\n\n" +
            "Yes — uninstall and keep my data\n" +
            "No — uninstall and delete my data\n" +
            "Cancel — don't uninstall",
            "Uninstall SysGreen",
            MessageBoxButton.YesNoCancel,
            MessageBoxImage.Warning);

        switch (choice)
        {
            case MessageBoxResult.Yes: vm.Uninstall(keepData: true); break;
            case MessageBoxResult.No: vm.Uninstall(keepData: false); break;
        }
    }

    private void ViewPolicy_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is SettingsViewModel vm)
            new PolicyWindow(vm.PolicyText) { Owner = this }.Show();
    }
}
