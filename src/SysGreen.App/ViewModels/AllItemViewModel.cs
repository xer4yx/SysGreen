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
    private readonly Action<ManageableItem> _disable;
    private readonly Action<ManageableItem> _enable;
    private readonly Action<ManageableItem, Purpose> _setPurpose;
    private readonly Action<ManageableItem> _neverRecommend;
    private readonly Action<ManageableItem> _endTask;

    public ManageableItem Item { get; }
    public string DisplayText { get; }
    public bool CanDisable => Item.CanDisable;

    /// <summary>True for an already-disabled item — the row offers Enable in the same slot (CONTEXT.md).</summary>
    public bool CanEnable => Item.CanEnable;

    /// <summary>End Task is only available for a live process (CONTEXT.md "Process").</summary>
    public bool CanEndTask => Item.IsRunning;
    public IReadOnlyList<Purpose> Purposes { get; } = Enum.GetValues<Purpose>();

    /// <summary>Bound to the Purpose combo; changing it records a user Override.</summary>
    [ObservableProperty]
    private Purpose _selectedPurpose;

    public AllItemViewModel(
        ManageableItem item,
        Action<ManageableItem> disable,
        Action<ManageableItem> enable,
        Action<ManageableItem, Purpose> setPurpose,
        Action<ManageableItem> neverRecommend,
        Action<ManageableItem> endTask)
    {
        Item = item;
        _disable = disable;
        _enable = enable;
        _setPurpose = setPurpose;
        _neverRecommend = neverRecommend;
        _endTask = endTask;
        _selectedPurpose = item.Purpose; // field assignment: don't fire OnSelectedPurposeChanged here

        var ram = item.RamEstimateBytes is { } b ? $"≈{RamEstimate.Format(b)}" : "≈ ?";
        var state = item.Autostart?.State.ToString() ?? "—";
        DisplayText = $"{item.DisplayName,-30} {state,-8} {ram,-9} {(item.IsRunning ? "running" : "stopped")}";
    }

    partial void OnSelectedPurposeChanged(Purpose value)
    {
        if (value != Item.Purpose) _setPurpose(Item, value);
    }

    [RelayCommand(CanExecute = nameof(CanDisable))]
    private void Disable() => _disable(Item);

    [RelayCommand(CanExecute = nameof(CanEnable))]
    private void Enable() => _enable(Item);

    [RelayCommand]
    private void NeverRecommend() => _neverRecommend(Item);

    [RelayCommand(CanExecute = nameof(CanEndTask))]
    private void EndTask() => _endTask(Item);
}
