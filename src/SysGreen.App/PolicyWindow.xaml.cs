using System.Windows;

namespace SysGreen.App;

/// <summary>Read-only viewer for the Privacy Policy &amp; Terms text (ADR-0018).</summary>
public partial class PolicyWindow : Window
{
    public PolicyWindow(string policyText)
    {
        InitializeComponent();
        PolicyBody.Text = policyText;
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
