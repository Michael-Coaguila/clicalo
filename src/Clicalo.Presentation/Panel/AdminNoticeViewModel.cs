using CommunityToolkit.Mvvm.ComponentModel;

namespace Clicalo.Presentation.Panel;

/// <summary>
/// The administrator notice (EJE-013): a warnWash card with <c>admin_panel_settings</c>, [adminMsg] with the app and
/// the [adminBtn] button, while the foreground app is elevated and Clícalo is not. It never dims.
/// </summary>
public sealed class AdminNoticeViewModel : ObservableObject
{
    private readonly IPanelBodyIntents _intents;
    private bool _isVisible;
    private string _message = string.Empty;
    private string _buttonName = string.Empty;

    internal AdminNoticeViewModel(IPanelBodyIntents intents) => _intents = intents;

    /// <summary>Whether the card shows.</summary>
    public bool IsVisible
    {
        get => _isVisible;
        private set => SetProperty(ref _isVisible, value);
    }

    /// <summary>[adminMsg] with the app.</summary>
    public string Message
    {
        get => _message;
        private set => SetProperty(ref _message, value);
    }

    /// <summary>[adminBtn].</summary>
    public string ButtonName
    {
        get => _buttonName;
        private set => SetProperty(ref _buttonName, value);
    }

    /// <summary>[adminBtn] (a tap or UI Automation Invoke): relaunch elevated.</summary>
    public void Relaunch() => _intents.RelaunchElevated();

    internal void Apply(bool visible, string message, string buttonName)
    {
        Message = message;
        ButtonName = buttonName;
        IsVisible = visible;
    }
}
