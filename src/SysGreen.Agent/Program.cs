using System.Diagnostics;
using System.IO;
using SysGreen.Core;
using SysGreen.Core.Startup;
using SysGreen.Core.Usage;
using SysGreen.Data;
using SysGreen.Platform;

namespace SysGreen.Agent;

/// <summary>
/// The SysGreen Tray Agent: a deliberately tiny, non-elevated, resident component that records app
/// launches to power the habit engine (ADR-0004/0006/0008). It runs at logon via SysGreen's own
/// transparent, user-disableable autostart entry (ADR-0014), and tracking is on by default with a
/// visible off switch (ADR-0012).
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

/// <summary>Hosts the tray icon and the launch sampler — no main window.</summary>
sealed class TrayApplicationContext : ApplicationContext
{
    private const string AutostartValueName = "SysGreen Agent";

    private readonly NotifyIcon _trayIcon;
    private readonly ToolStripMenuItem _trackingItem;
    private readonly ITrackingSettings _settings;
    private readonly AgentAutostart _autostart;
    private readonly IProcessStartSource _processStarts;
    private readonly LaunchSampler _sampler;

    public TrayApplicationContext()
    {
        // Shared local store (ADR-0006); the App normally created it first, but be self-sufficient.
        var factory = new SqliteConnectionFactory(SqliteConnectionFactory.DefaultDatabasePath());
        new DatabaseBootstrapper(factory).EnsureCreated();

        _settings = new SettingsRepository(factory);
        _autostart = new AgentAutostart(new RunKeyRegistration());
        _sampler = new LaunchSampler(new UsageRepository(factory), _settings, new SystemClock());
        _processStarts = new WmiProcessStartSource();
        _processStarts.ProcessStarted += _sampler.OnProcessStarted;

        // Keep SysGreen's own autostart entry in step with the setting, then start sampling if on.
        SyncAutostart();
        if (_settings.LaunchTrackingEnabled) _processStarts.Start();

        _trackingItem = new ToolStripMenuItem("", null, (_, _) => ToggleTracking());

        var menu = new ContextMenuStrip();
        menu.Items.Add("Open SysGreen", null, (_, _) => OpenMainApp());
        menu.Items.Add(_trackingItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => ExitThread());

        _trayIcon = new NotifyIcon
        {
            Icon = SystemIcons.Application, // TODO: ship a SysGreen tray icon
            Visible = true,
            ContextMenuStrip = menu,
        };

        UpdateTrackingUi();
    }

    private void ToggleTracking()
    {
        var nowEnabled = !_settings.LaunchTrackingEnabled;
        _settings.SetLaunchTrackingEnabled(nowEnabled);
        SyncAutostart();
        if (nowEnabled) _processStarts.Start(); else _processStarts.Stop();
        UpdateTrackingUi();
    }

    private void SyncAutostart() =>
        _autostart.Sync(_settings.LaunchTrackingEnabled, AutostartValueName, Environment.ProcessPath ?? "");

    private void UpdateTrackingUi()
    {
        var on = _settings.LaunchTrackingEnabled;
        _trackingItem.Text = on ? "Pause launch tracking" : "Resume launch tracking";
        _trayIcon.Text = on ? "SysGreen — tracking launches locally" : "SysGreen — launch tracking paused";
    }

    private static void OpenMainApp()
    {
        var app = Path.Combine(AppContext.BaseDirectory, "SysGreen.App.exe");
        if (File.Exists(app)) Process.Start(new ProcessStartInfo(app) { UseShellExecute = true });
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            (_processStarts as IDisposable)?.Dispose();
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
        }
        base.Dispose(disposing);
    }
}
