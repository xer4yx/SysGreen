using System;
using Velopack;

namespace SysGreen.App;

/// <summary>
/// Custom entry point (Velopack — ADR-0009). <see cref="VelopackApp"/> must run before WPF starts so
/// install/update/uninstall hooks are handled without spinning up the UI. On a normal launch it is a
/// no-op and we proceed to construct and run the WPF <see cref="App"/> as usual.
/// </summary>
public static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        VelopackApp.Build().Run();

        var app = new App();
        app.InitializeComponent();
        app.Run();
    }
}
