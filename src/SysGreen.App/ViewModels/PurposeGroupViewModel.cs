using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SysGreen.Core.Domain;

namespace SysGreen.App.ViewModels;

/// <summary>
/// One Purpose group in the All Items view (Q15): its rows plus a "Disable all" action that disables
/// every still-disable-able item in the group in a single batch (one restore point / UAC prompt).
/// </summary>
public sealed partial class PurposeGroupViewModel : ObservableObject
{
    private readonly Func<IReadOnlyList<ManageableItem>, Task> _disableAll;
    private readonly ApplyBusyState _busy;

    public Purpose Purpose { get; }
    public string Header { get; }
    public IReadOnlyList<AllItemViewModel> Items { get; }

    /// <summary>True when the group still has at least one item that can be disabled.</summary>
    public bool CanDisableGroup => Items.Any(i => i.CanDisable);

    // Executable only when there is something to disable AND no Apply is in flight (busy gate / Phase 6).
    private bool CanDisableGroupNow => CanDisableGroup && !_busy.IsApplying;

    public PurposeGroupViewModel(
        Purpose purpose, IReadOnlyList<AllItemViewModel> items,
        Func<IReadOnlyList<ManageableItem>, Task> disableAll, ApplyBusyState busy)
    {
        Purpose = purpose;
        Items = items;
        _disableAll = disableAll;
        _busy = busy;
        Header = $"{purpose}  ({items.Count})";
    }

    /// <summary>Re-evaluates the busy-gated group command (and its rows) when an Apply starts/finishes.</summary>
    public void RefreshCommandStates()
    {
        DisableGroupCommand.NotifyCanExecuteChanged();
        foreach (var item in Items) item.RefreshCommandStates();
    }

    [RelayCommand(CanExecute = nameof(CanDisableGroupNow))]
    private Task DisableGroup() =>
        _disableAll(Items.Where(i => i.CanDisable).Select(i => i.Item).ToList());
}
