using CommunityToolkit.Mvvm.ComponentModel;
using SysGreen.Core.Domain;
using SysGreen.Core.Recommendations;

namespace SysGreen.App.ViewModels;

/// <summary>A single recommendation row the user can check/uncheck before Apply (ADR-0007).</summary>
public sealed partial class RecommendationViewModel : ObservableObject
{
    public Recommendation Recommendation { get; }
    public ManageableItem Item => Recommendation.Item;
    public string DisplayText { get; }

    /// <summary>Recommended items are pre-checked — "select all recommended" by default (ADR-0007).</summary>
    [ObservableProperty]
    private bool _isSelected = true;

    public RecommendationViewModel(Recommendation recommendation)
    {
        Recommendation = recommendation;
        DisplayText = $"{recommendation.Item.DisplayName}  —  {recommendation.Reason}";
    }
}
