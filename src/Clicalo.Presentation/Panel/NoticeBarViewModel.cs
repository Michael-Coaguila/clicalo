using CommunityToolkit.Mvvm.ComponentModel;

namespace Clicalo.Presentation.Panel;

/// <summary>
/// The notice bar (AVI-001, AVI-003, AVI-004): 40 px at the foot, a polite live region with an icon and the message;
/// [ready] at rest; [undo] when the notice can be undone; ↻ when there is a last action and the panel is not in edit
/// mode; [cancel] when the notice offers it (ATJ-008).
/// </summary>
public sealed class NoticeBarViewModel : ObservableObject
{
    private readonly IPanelBodyIntents _intents;
    private bool _isVisible;
    private string _message = string.Empty;
    private string _icon = string.Empty;
    private NoticeTone _tone;
    private bool _canUndo;
    private string _undoName = string.Empty;
    private bool _canRepeat;
    private string _repeatName = string.Empty;
    private bool _canCancel;
    private string _cancelName = string.Empty;

    internal NoticeBarViewModel(IPanelBodyIntents intents) => _intents = intents;

    /// <summary>Whether the bar shows.</summary>
    public bool IsVisible
    {
        get => _isVisible;
        private set => SetProperty(ref _isVisible, value);
    }

    /// <summary>The message, or [ready] at rest.</summary>
    public string Message
    {
        get => _message;
        private set => SetProperty(ref _message, value);
    }

    /// <summary>The Material Symbols icon: the notice's, or <c>info</c> at rest.</summary>
    public string Icon
    {
        get => _icon;
        private set => SetProperty(ref _icon, value);
    }

    /// <summary>At rest, a notice or a warning.</summary>
    public NoticeTone Tone
    {
        get => _tone;
        private set => SetProperty(ref _tone, value);
    }

    /// <summary>Whether [undo] shows.</summary>
    public bool CanUndo
    {
        get => _canUndo;
        private set => SetProperty(ref _canUndo, value);
    }

    /// <summary>[undo].</summary>
    public string UndoName
    {
        get => _undoName;
        private set => SetProperty(ref _undoName, value);
    }

    /// <summary>Whether ↻ shows.</summary>
    public bool CanRepeat
    {
        get => _canRepeat;
        private set => SetProperty(ref _canRepeat, value);
    }

    /// <summary>[repeat].</summary>
    public string RepeatName
    {
        get => _repeatName;
        private set => SetProperty(ref _repeatName, value);
    }

    /// <summary>Whether [cancel] shows.</summary>
    public bool CanCancel
    {
        get => _canCancel;
        private set => SetProperty(ref _canCancel, value);
    }

    /// <summary>[cancel].</summary>
    public string CancelName
    {
        get => _cancelName;
        private set => SetProperty(ref _cancelName, value);
    }

    /// <summary>[cancel] (a tap or UI Automation Invoke).</summary>
    public void Cancel() => _intents.CancelNotice();

    /// <summary>[undo] (a tap or UI Automation Invoke).</summary>
    public void Undo() => _intents.Undo();

    /// <summary>↻ (a tap or UI Automation Invoke).</summary>
    public void Repeat() => _intents.Repeat();

    internal void Apply(
        bool visible,
        string message,
        string icon,
        NoticeTone tone,
        bool canUndo,
        bool canRepeat,
        string undoName,
        string repeatName,
        bool canCancel = false,
        string cancelName = ""
    )
    {
        CanCancel = canCancel;
        CancelName = cancelName;
        Icon = icon;
        Tone = tone;
        CanUndo = canUndo;
        CanRepeat = canRepeat;
        UndoName = undoName;
        RepeatName = repeatName;
        Message = message;
        IsVisible = visible;
    }
}
