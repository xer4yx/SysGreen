namespace SysGreen.Agent;

/// <summary>
/// The SysGreen Tray Agent: a deliberately tiny, non-elevated, resident component that
/// records app launches to power the habit engine (ADR-0004, ADR-0006). It runs at logon
/// via SysGreen's own (transparent, user-disableable) autostart entry (ADR-0014).
///
/// This scaffold shows a tray icon with a context menu; launch sampling is wired in a
/// later milestone.
/// </summary>
static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new TrayApplicationContext());
    }
}

/// <summary>Hosts the tray icon and (later) the launch sampler — no main window.</summary>
sealed class TrayApplicationContext : ApplicationContext
{
    private readonly NotifyIcon _trayIcon;

    public TrayApplicationContext()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("Open SysGreen", null, (_, _) => OpenMainApp());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => ExitThread());

        _trayIcon = new NotifyIcon
        {
            Icon = SystemIcons.Application, // TODO: ship a SysGreen tray icon
            Text = "SysGreen — tracking launches locally",
            Visible = true,
            ContextMenuStrip = menu,
        };

        // TODO: start the launch sampler and persist via SysGreen.Data (ADR-0006).
    }

    private static void OpenMainApp()
    {
        // TODO: launch / focus SysGreen.App.
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
        }
        base.Dispose(disposing);
    }
}
