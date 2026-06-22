using System.IO;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
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

        // On first run, seed the habit store from existing Windows launch history (ADR-0008).
        SeedHabitHistory(_services);

        var window = _services.GetRequiredService<MainWindow>();
        window.Show();
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

        // Platform providers (ADR-0008 / ADR-0011)
        services.AddSingleton<IExecutablePublisherReader, AuthenticodePublisherReader>();
        // Autostart enumeration (ADR-0005/0008): Run keys + the per-user/common Startup folders +
        // logon scheduled tasks, combined, then decorated so each Run/folder entry's real enable/
        // disable state is read back from the Windows StartupApproved flags (tasks carry their own).
        services.AddSingleton<RegistryAutostartProvider>();
        services.AddSingleton<StartupFolderAutostartProvider>();
        services.AddSingleton<ScheduledTaskProvider>();
        services.AddSingleton<IScheduledTaskProvider>(sp => sp.GetRequiredService<ScheduledTaskProvider>());
        services.AddSingleton<IAutostartProvider>(sp => new StartupApprovedAutostartProvider(
            new CompositeAutostartProvider(
                sp.GetRequiredService<RegistryAutostartProvider>(),
                sp.GetRequiredService<StartupFolderAutostartProvider>(),
                sp.GetRequiredService<ScheduledTaskProvider>()),
            sp.GetRequiredService<IStartupApprovedStore>()));
        services.AddSingleton<IProcessProvider, ProcessProvider>();
        services.AddSingleton<IWindowsServiceProvider, WindowsServiceProvider>();
        services.AddSingleton<Core.Usage.IUsageHistoryProvider, UserAssistUsageHistoryProvider>();
        services.AddSingleton<IRestorePointApi, WmiRestorePointApi>();
        services.AddSingleton<IRestorePointService, RestorePointService>();

        // Non-destructive disable/enable + end task (ADR-0005), test-driven controller + adapters
        services.AddSingleton<IStartupApprovedStore, StartupApprovedRegistryStore>();
        services.AddSingleton<IProcessTerminator, ProcessTerminator>();
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IItemController, StartupApprovedItemController>();

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

        // Knowledge + recommendations (ADR-0002 / ADR-0007 / ADR-0010)
        var kbPath = Path.Combine(AppContext.BaseDirectory, "knowledge-base.json");
        services.AddSingleton<IKnowledgeBase>(_ => JsonKnowledgeBase.LoadFromFile(kbPath));
        services.AddSingleton<IClassifier, Classifier>();
        services.AddSingleton<IRecommendationEngine>(_ => new RecommendationEngine());

        // UI
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<MainWindow>();

        return services.BuildServiceProvider();
    }
}
