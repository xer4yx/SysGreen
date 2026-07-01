using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SysGreen.Core.Domain;
using SysGreen.Core.Usage;

namespace SysGreen.App.ViewModels;

/// <summary>
/// A single actionable row in the (grouped) All Items view: disable it, relabel its Purpose via a
/// user Override, or flag it never-recommend (CONTEXT.md "Override"). Relabelling records the override
/// the moment the user picks a different Purpose.
/// </summary>
public sealed partial class AllItemViewModel : ObservableObject
{
    private readonly Func<ManageableItem, Task> _disable;
    private readonly Func<ManageableItem, Task> _enable;
    private readonly Action<ManageableItem, Purpose> _setPurpose;
    private readonly Action<ManageableItem> _neverRecommend;
    private readonly Action<ManageableItem> _endTask;
    private readonly ApplyBusyState _busy;

    public ManageableItem Item { get; }
    public string DisplayText { get; }

    /// <summary>Whether the item can be disabled at all — drives which button shows (Disable vs Enable).</summary>
    public bool CanDisable => Item.CanDisable;

    /// <summary>True for an already-disabled item — the row offers Enable in the same slot (CONTEXT.md).</summary>
    public bool CanEnable => Item.CanEnable;

    /// <summary>End Task is only available for a live process (CONTEXT.md "Process").</summary>
    public bool CanEndTask => Item.IsRunning;

    // Command-executability = the ability above AND no Apply in flight (Topic B busy gate / Phase 6).
    private bool CanDisableNow => CanDisable && !_busy.IsApplying;
    private bool CanEnableNow => CanEnable && !_busy.IsApplying;
    private bool CanEndTaskNow => CanEndTask && !_busy.IsApplying;

    public IReadOnlyList<Purpose> Purposes { get; } = Enum.GetValues<Purpose>();

    /// <summary>Bound to the Purpose combo; changing it records a user Override.</summary>
    [ObservableProperty]
    private Purpose _selectedPurpose;

    public AllItemViewModel(
        ManageableItem item,
        Func<ManageableItem, Task> disable,
        Func<ManageableItem, Task> enable,
        Action<ManageableItem, Purpose> setPurpose,
        Action<ManageableItem> neverRecommend,
        Action<ManageableItem> endTask,
        ApplyBusyState busy)
    {
        Item = item;
        _disable = disable;
        _enable = enable;
        _setPurpose = setPurpose;
        _neverRecommend = neverRecommend;
        _endTask = endTask;
        _busy = busy;
        _selectedPurpose = item.Purpose; // field assignment: don't fire OnSelectedPurposeChanged here

        var ram = item.RamEstimateBytes is { } b ? $"≈{RamEstimate.Format(b)}" : "≈ ?";
        var state = item.Autostart?.State.ToString() ?? "—";
        DisplayText = $"{item.DisplayName,-30} {state,-8} {ram,-9} {(item.IsRunning ? "running" : "stopped")}";
    }

    /// <summary>Re-evaluates the busy-gated commands when an Apply starts or finishes (Phase 6).</summary>
    public void RefreshCommandStates()
    {
        DisableCommand.NotifyCanExecuteChanged();
        EnableCommand.NotifyCanExecuteChanged();
        EndTaskCommand.NotifyCanExecuteChanged();
    }

    partial void OnSelectedPurposeChanged(Purpose value)
    {
        if (value != Item.Purpose) _setPurpose(Item, value);
    }

    [RelayCommand(CanExecute = nameof(CanDisableNow))]
    private Task Disable() => _disable(Item);

    [RelayCommand(CanExecute = nameof(CanEnableNow))]
    private Task Enable() => _enable(Item);

    [RelayCommand]
    private void NeverRecommend() => _neverRecommend(Item);

    [RelayCommand(CanExecute = nameof(CanEndTaskNow))]
    private void EndTask() => _endTask(Item);
}
