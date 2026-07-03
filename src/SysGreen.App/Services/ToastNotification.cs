using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace SysGreen.App.Services;

/// <summary>
/// One toast in the overlay (Topic C / Phase 7): a message, its severity (drives color), and a Close
/// command the host wires to remove it. Bound by the toast host's ItemsControl.
/// </summary>
public sealed partial class ToastNotification : ObservableObject
{
    private readonly Action<ToastNotification> _close;

    public string Message { get; }

    /// <summary>True for an error toast (red, persists); false for success (green, auto-dismisses).</summary>
    public bool IsError { get; }

    public ToastNotification(string message, bool isError, Action<ToastNotification> close)
    {
        Message = message;
        IsError = isError;
        _close = close;
    }

    [RelayCommand]
    private void Close() => _close(this);
}
