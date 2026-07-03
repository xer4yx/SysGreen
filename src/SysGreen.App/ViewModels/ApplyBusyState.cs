using CommunityToolkit.Mvvm.ComponentModel;

namespace SysGreen.App.ViewModels;

/// <summary>
/// Shared "an Apply batch is in flight" signal for the whole shell (Topic B / Phase 6). Every mutating
/// command gates on <see cref="IsApplying"/> — one mutation at a time — while navigation stays free.
/// The header progress strip binds to both properties.
/// </summary>
public sealed partial class ApplyBusyState : ObservableObject
{
    [ObservableProperty]
    private bool _isApplying;

    /// <summary>Human-readable current phase (e.g. "Applying 2 of 3…"); empty when idle.</summary>
    [ObservableProperty]
    private string _progressPhase = "";
}
