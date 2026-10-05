using Clicalo.Domain.Keys;
using Clicalo.Domain.StickyModifiers;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Clicalo.Presentation.Panel;

/// <summary>
/// One sticky modifier (FIJ-005): released (card with a border), once (accent) or locked (accent and 🔒). It is a
/// three-state Toggle for UI Automation; a tap asks to advance it 0 → 1 → 2 → 0.
/// </summary>
public sealed class StickyKeyViewModel : ObservableObject
{
    private readonly Action<ModifierKind> _advance;
    private StickyLevel _level;
    private string _accessibleState = string.Empty;

    internal StickyKeyViewModel(ModifierKind modifier, string label, Action<ModifierKind> advance)
    {
        Modifier = modifier;
        Label = label;
        _advance = advance;
    }

    /// <summary>Ctrl, Alt, Shift or Win.</summary>
    public ModifierKind Modifier { get; }

    /// <summary>The key name as the keys catalog shows it (also its accessible name).</summary>
    public string Label { get; }

    /// <summary>Released, once or locked.</summary>
    public StickyLevel Level
    {
        get => _level;
        private set
        {
            if (SetProperty(ref _level, value))
            {
                OnPropertyChanged(nameof(IsActive));
                OnPropertyChanged(nameof(IsLocked));
            }
        }
    }

    /// <summary>Whether it is once or locked (accent fill).</summary>
    public bool IsActive => Level != StickyLevel.Off;

    /// <summary>Whether it is locked (🔒).</summary>
    public bool IsLocked => Level == StickyLevel.Locked;

    /// <summary>The state in words ([modOff], [modOnce] or [modLock]); never color alone (ACC-003).</summary>
    public string AccessibleState
    {
        get => _accessibleState;
        private set => SetProperty(ref _accessibleState, value);
    }

    /// <summary>A tap or a UI Automation Toggle.</summary>
    public void Tap() => _advance(Modifier);

    internal void Apply(StickyLevel level, string state)
    {
        Level = level;
        AccessibleState = state;
    }
}
