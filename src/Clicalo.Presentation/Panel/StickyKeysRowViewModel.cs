using Clicalo.Domain.Keys;
using Clicalo.Domain.StickyModifiers;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Clicalo.Presentation.Panel;

/// <summary>The sticky modifiers row (FIJ-005): Ctrl · Alt · Shift · Win, in that order.</summary>
public sealed class StickyKeysRowViewModel : ObservableObject
{
    private bool _isVisible;
    private string _accessibleName = string.Empty;

    internal StickyKeysRowViewModel(
        Func<ModifierKind, string> labelOf,
        Action<ModifierKind> advance
    ) => Keys = [.. StickyState.Order.Select(m => new StickyKeyViewModel(m, labelOf(m), advance))];

    /// <summary>The four keys, in send order.</summary>
    public IReadOnlyList<StickyKeyViewModel> Keys { get; }

    /// <summary>Whether the row shows.</summary>
    public bool IsVisible
    {
        get => _isVisible;
        private set => SetProperty(ref _isVisible, value);
    }

    /// <summary>[stickyMods], the name of the row.</summary>
    public string AccessibleName
    {
        get => _accessibleName;
        private set => SetProperty(ref _accessibleName, value);
    }

    internal void Apply(
        bool visible,
        StickyState state,
        string name,
        Func<StickyLevel, string> stateText
    )
    {
        foreach (var key in Keys)
        {
            var level = state.LevelOf(key.Modifier);
            key.Apply(level, stateText(level));
        }

        AccessibleName = name;
        IsVisible = visible;
    }
}
