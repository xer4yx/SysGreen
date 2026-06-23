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
    private readonly Action<IReadOnlyList<ManageableItem>> _disableAll;

    public Purpose Purpose { get; }
    public string Header { get; }
    public IReadOnlyList<AllItemViewModel> Items { get; }

    /// <summary>Enabled when the group still has at least one item that can be disabled.</summary>
    public bool CanDisableGroup => Items.Any(i => i.CanDisable);

    public PurposeGroupViewModel(
        Purpose purpose, IReadOnlyList<AllItemViewModel> items, Action<IReadOnlyList<ManageableItem>> disableAll)
    {
        Purpose = purpose;
        Items = items;
        _disableAll = disableAll;
        Header = $"{purpose}  ({items.Count})";
    }

    [RelayCommand(CanExecute = nameof(CanDisableGroup))]
    private void DisableGroup() =>
        _disableAll(Items.Where(i => i.CanDisable).Select(i => i.Item).ToList());
}
