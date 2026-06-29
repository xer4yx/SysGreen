using System.Diagnostics;
using System.IO;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using SysGreen.App.Services;
using SysGreen.App.ViewModels;
using SysGreen.Core;
using SysGreen.Core.Abstractions;
using SysGreen.Core.Apply;
using SysGreen.Core.Knowledge;
using SysGreen.Core.Recommendations;
using SysGreen.Core.Startup;
using SysGreen.Data;
using SysGreen.Platform;

namespace SysGreen.App;

/// <summary>
/// Application entry point. Composes the object graph (ADR-0011) and shows the main window.
/// Runs non-elevated (ADR-0004); privileged work is delegated to SysGreen.Helper later.
/// </summary>
public partial class App : Application
{
    private ServiceProvider? _services;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _services = ConfigureServices();

        // Ensure the local SQLite schema exists (ADR-0006).
        _services.GetRequiredService<DatabaseBootstrapper>().EnsureCreated();

        // Show the welcome / policy-acceptance gate when the user hasn't accepted the current policy
        // version (ADR-0018) — that covers first run (accepted 0 < current) and any later version bump.
        // On first run it also carries the launch-tracking consent (ADR-0012/0014).
        var acceptance = _services.GetRequiredService<Core.Usage.IPolicyAcceptance>();
        var policy = _services.GetRequiredService<IPolicyProvider>();
        if (policy.CurrentVersion > acceptance.AcceptedPolicyVersion)
            ShowOnboarding();
        else
            ShowMainExperience();
    }

    private void ShowOnboarding()
    {
        var vm = _services!.GetRequiredService<OnboardingViewModel>();
        var window = new OnboardingWindow { DataContext = vm };
        vm.Completed += () =>
        {
            ShowMainExperience(); // show the main window first so the app never hits zero windows
            window.Close();
        };
        window.Show();
    }

    private void ShowMainExperience()
    {
        // Seed the habit store from existing Windows history (ADR-0008), then start the Tray Agent so
        // launches are tracked going forward (only after consent — ADR-0012/0014).
        SeedHabitHistory(_services!);
        EnsureTrayAgentRunning(_services!);
        _services!.GetRequiredService<MainWindow>().Show();
        // Check for a newer release in the background; the banner appears if one is found (ADR-0009).
        _ = _services!.GetRequiredService<MainViewModel>().CheckForUpdatesAsync();
    }

    /// <summary>
    /// Launches the resident Tray Agent if launch tracking is on and it isn't already running
    /// (ADR-0012/0014). The Agent itself keeps SysGreen's autostart entry in step for later logons.
    /// </summary>
    private static void EnsureTrayAgentRunning(IServiceProvider services)
    {
        if (!services.GetRequiredService<Core.Usage.ITrackingSettings>().LaunchTrackingEnabled) return;
        if (Process.GetProcessesByName("SysGreen.Agent").Length > 0) return;

        var agent = Path.Combine(AppContext.BaseDirectory, "SysGreen.Agent.exe");
        if (!File.Exists(agent)) return;
        try { Process.Start(new ProcessStartInfo(agent) { UseShellExecute = true }); }
        catch { /* best effort — the App still works without the Agent (static-only recommendations) */ }
    }

    private static void SeedHabitHistory(IServiceProvider services)
    {
        var usage = services.GetRequiredService<IUsageRepository>();
        if (usage.GetAll().Count > 0) return; // already populated

        var seed = services.GetRequiredService<Core.Usage.IUsageHistoryProvider>().ReadSeedHistory();
        if (seed.Count > 0) usage.UpsertMany(seed);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _services?.Dispose();
        base.OnExit(e);
    }

    private static ServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();

        // Data (ADR-0006)
        services.AddSingleton<IConnectionFactory>(
            _ => new SqliteConnectionFactory(SqliteConnectionFactory.DefaultDatabasePath()));
        services.AddSingleton<DatabaseBootstrapper>();
        services.AddSingleton<IUsageRepository, UsageRepository>();
        services.AddSingleton<ChangeRecordRepository>();
        services.AddSingleton<IChangeRecordRepository>(sp => sp.GetRequiredService<ChangeRecordRepository>());
        services.AddSingleton<IChangeLog>(sp => sp.GetRequiredService<ChangeRecordRepository>());
        services.AddSingleton(sp => new SettingsRepository(sp.GetRequiredService<IConnectionFactory>()));
        services.AddSingleton<Core.Usage.ITrackingSettings>(sp => sp.GetRequiredService<SettingsRepository>());
        services.AddSingleton<Core.Usage.IOnboardingState>(sp => sp.GetRequiredService<SettingsRepository>());
        services.AddSingleton<Core.Usage.IDataRetentionSettings>(sp => sp.GetRequiredService<SettingsRepository>());
        services.AddSingleton<Core.Usage.IPolicyAcceptance>(sp => sp.GetRequiredService<SettingsRepository>());
        services.AddSingleton<Core.Usage.IThresholdSettings>(sp => sp.GetRequiredService<SettingsRepository>());
        services.AddSingleton<Core.Usage.IDataStoreReset>(sp => new DataStoreReset(sp.GetRequiredService<IConnectionFactory>()));

        // Platform providers (ADR-0008 / ADR-0011)
        services.AddSingleton<IExecutablePublisherReader, AuthenticodePublisherReader>();
        // Autostart enumeration (ADR-0005/0008): Run keys + the per-user/common Startup folders +
        // logon scheduled tasks, combined, then decorated so each Run/folder entry's real enable/
        // disable state is read back from the Windows StartupApproved flags (tasks carry their own).
        services.AddSingleton<RegistryAutostartProvider>();
        services.AddSingleton<StartupFolderAutostartProvider>();
        services.AddSingleton<ScheduledTaskProvider>();
        services.AddSingleton<IScheduledTaskProvider>(sp => sp.GetRequiredService<ScheduledTaskProvider>());
        services.AddSingleton<BackgroundAppProvider>();
        services.AddSingleton<IAutostartProvider>(sp => new StartupApprovedAutostartProvider(
            new CompositeAutostartProvider(
                sp.GetRequiredService<RegistryAutostartProvider>(),
                sp.GetRequiredService<StartupFolderAutostartProvider>(),
                sp.GetRequiredService<ScheduledTaskProvider>(),
                sp.GetRequiredService<BackgroundAppProvider>()),
            sp.GetRequiredService<IStartupApprovedStore>()));
        services.AddSingleton<IProcessProvider, ProcessProvider>();
        services.AddSingleton<IWindowsServiceProvider, WindowsServiceProvider>();
        services.AddSingleton<Core.Usage.IUsageHistoryProvider, UserAssistUsageHistoryProvider>();
        services.AddSingleton<IRestorePointApi, WmiRestorePointApi>();
        services.AddSingleton<IRestorePointService, RestorePointService>();

        // Non-destructive disable/enable + end task (ADR-0005), test-driven controller + adapters.
        // The in-process controller dispatches per mechanism; background apps disable non-elevated
        // (HKCU), so the App handles them directly (scheduled tasks elevate to the Helper instead).
        services.AddSingleton<IStartupApprovedStore, StartupApprovedRegistryStore>();
        services.AddSingleton<IScheduledTaskStore, TaskSchedulerStore>();
        services.AddSingleton<IBackgroundAppStore, BackgroundAppRegistryStore>();
        services.AddSingleton<IProcessTerminator, ProcessTerminator>();
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IItemController>(sp => new DispatchingItemController(
            new StartupApprovedItemController(
                sp.GetRequiredService<IStartupApprovedStore>(),
                sp.GetRequiredService<IProcessTerminator>(), sp.GetRequiredService<IClock>()),
            new ScheduledTaskItemController(sp.GetRequiredService<IScheduledTaskStore>(), sp.GetRequiredService<IClock>()),
            new BackgroundAppItemController(sp.GetRequiredService<IBackgroundAppStore>(), sp.GetRequiredService<IClock>())));

        // Apply routing (ADR-0004 / ADR-0011): per-user batches run in-process via ApplyService;
        // any batch with an admin-only item is delegated whole to the elevated Helper (one UAC
        // prompt), which creates the restore point and persists Change Records to the shared DB.
        services.AddSingleton<ApplyService>();
        services.AddSingleton<IElevatedApplyClient>(sp => new HelperElevatedApplyClient(
            Path.Combine(AppContext.BaseDirectory, "SysGreen.Helper.exe"),
            SqliteConnectionFactory.DefaultDatabasePath(),
            sp.GetRequiredService<IClock>()));
        services.AddSingleton<IApplyService>(sp => new RoutingApplyService(
            sp.GetRequiredService<ApplyService>(),
            sp.GetRequiredService<IElevatedApplyClient>()));

        // Undo: re-enable a single change or undo a whole batch by routing the inverse back through
        // the same Apply pipeline — so an undo elevates / creates a restore point as needed (ADR-0005).
        services.AddSingleton<IChangeReverser>(sp => new ChangeReverser(sp.GetRequiredService<IApplyService>()));

        // Knowledge + recommendations (ADR-0002 / ADR-0007 / ADR-0010). Classification precedence:
        // user Override → Knowledge Base → heuristic → Unknown. The OverridingClassifier wraps the
        // KB/heuristic Classifier so a user's "never recommend"/relabel always wins (CONTEXT.md).
        var kbPath = Path.Combine(AppContext.BaseDirectory, "knowledge-base.json");
        services.AddSingleton<IKnowledgeBase>(_ => JsonKnowledgeBase.LoadFromFile(kbPath));
        services.AddSingleton<IOverrideStore>(sp => new OverrideRepository(sp.GetRequiredService<IConnectionFactory>()));
        services.AddSingleton<Classifier>();
        services.AddSingleton<IClassifier>(sp => new OverridingClassifier(
            sp.GetRequiredService<Classifier>(), sp.GetRequiredService<IOverrideStore>()));
        // The engine reads the user's Abandoned threshold (Settings) live on each refresh (ADR-0007).
        services.AddSingleton<IRecommendationEngine>(sp => new RecommendationEngine(
            () => sp.GetRequiredService<Core.Usage.IThresholdSettings>().AbandonedThresholdDays));

        // Privacy Policy & Terms text + version, parsed from the shipped policy.md (ADR-0018).
        services.AddSingleton<IPolicyProvider>(_ =>
            new FilePolicyProvider(Path.Combine(AppContext.BaseDirectory, "policy.md")));

        // Self-update via Velopack reading GitHub Releases (ADR-0009). No-op when not a Velopack
        // install, so it's harmless in dev runs.
        services.AddSingleton<IUpdateService>(_ =>
            new VelopackUpdateService("https://github.com/xer4yx/SysGreen"));

        // In-app uninstall (ADR-0017): launches the Velopack uninstaller; the uninstall hook then
        // honors the keep/delete choice. No-op in a non-Velopack (dev) run.
        services.AddSingleton<IAppUninstaller, VelopackUninstaller>();

        // UI
        services.AddTransient<OnboardingViewModel>();
        services.AddTransient<SettingsViewModel>();
        services.AddSingleton<Func<SettingsViewModel>>(sp => sp.GetRequiredService<SettingsViewModel>);
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<MainWindow>();

        return services.BuildServiceProvider();
    }
}
