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

        var window = _services.GetRequiredService<MainWindow>();
        window.Show();
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
        services.AddSingleton<IAutostartProvider, RegistryAutostartProvider>();
        services.AddSingleton<IProcessProvider, ProcessProvider>();
        services.AddSingleton<IScheduledTaskProvider, ScheduledTaskProvider>();
        services.AddSingleton<IWindowsServiceProvider, WindowsServiceProvider>();
        services.AddSingleton<Core.Usage.IUsageHistoryProvider, UserAssistUsageHistoryProvider>();
        services.AddSingleton<IRestorePointApi, WmiRestorePointApi>();
        services.AddSingleton<IRestorePointService, RestorePointService>();

        // Non-destructive disable/enable + end task (ADR-0005), test-driven controller + adapters
        services.AddSingleton<IStartupApprovedStore, StartupApprovedRegistryStore>();
        services.AddSingleton<IProcessTerminator, ProcessTerminator>();
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IItemController, StartupApprovedItemController>();
        services.AddSingleton<IApplyService, ApplyService>();

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
