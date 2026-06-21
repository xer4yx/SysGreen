using System.Windows;
using SysGreen.App.ViewModels;

namespace SysGreen.App;

/// <summary>
/// Interaction logic for MainWindow.xaml. The view-model is injected (ADR-0011).
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
