using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Threading;

namespace SysGreen.App.Services;

/// <summary>
/// Hand-rolled WPF overlay toast host (Topic C / Phase 7) — the zero-dependency implementation behind
/// the <see cref="IToastService"/> seam. Holds the live <see cref="Notifications"/> collection the
/// MainWindow overlay binds to; success toasts auto-dismiss after ~5s, errors persist until closed.
/// All mutations marshal to the UI thread, so callers may report an outcome from any thread.
/// </summary>
public sealed class ToastService : IToastService
{
    private static readonly TimeSpan AutoDismiss = TimeSpan.FromSeconds(5);

    private readonly Dispatcher _dispatcher;

    /// <summary>The visible toasts, newest last; bound by the overlay's ItemsControl.</summary>
    public ObservableCollection<ToastNotification> Notifications { get; } = [];

    public ToastService() =>
        _dispatcher = Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;

    public void ShowSuccess(string message) => Add(message, isError: false, autoDismiss: true);

    public void ShowError(string message) => Add(message, isError: true, autoDismiss: false);

    private void Add(string message, bool isError, bool autoDismiss)
    {
        _dispatcher.Invoke(() =>
        {
            var toast = new ToastNotification(message, isError, Remove);
            Notifications.Add(toast);
            if (autoDismiss)
            {
                var timer = new DispatcherTimer { Interval = AutoDismiss };
                timer.Tick += (_, _) => { timer.Stop(); Remove(toast); };
                timer.Start();
            }
        });
    }

    private void Remove(ToastNotification toast) => Notifications.Remove(toast);
}
