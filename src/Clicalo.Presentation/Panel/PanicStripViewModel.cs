using Clicalo.Application.Coordinators;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Clicalo.Presentation.Panel;

/// <summary>
/// The panic strip (SEG-002): visible while the engine holds anything, it says what is held («Pulsado: {keys}») and
/// offers «Release all» (SEG-003), a target of at least 44 announced as an assertive alert. It never dims.
/// </summary>
public sealed class PanicStripViewModel : ObservableObject
{
    private readonly PanelInteractionController _controller;
    private bool _isVisible;
    private string _heldMessage = string.Empty;
    private string _releaseAllName = string.Empty;

    /// <summary>Creates the strip.</summary>
    /// <param name="controller">Where «Release all» goes.</param>
    public PanicStripViewModel(PanelInteractionController controller)
    {
        ArgumentNullException.ThrowIfNull(controller);
        _controller = controller;
    }

    /// <summary>Whether something is held (the ledger is not empty).</summary>
    public bool IsVisible
    {
        get => _isVisible;
        private set => SetProperty(ref _isVisible, value);
    }

    /// <summary>What is held, localized («Pulsado: Copiar, Mayús fija»).</summary>
    public string HeldMessage
    {
        get => _heldMessage;
        private set => SetProperty(ref _heldMessage, value);
    }

    /// <summary>The name of the «Release all» button, localized.</summary>
    public string ReleaseAllName
    {
        get => _releaseAllName;
        private set => SetProperty(ref _releaseAllName, value);
    }

    /// <summary>«Release all» (a tap, or UI Automation Invoke).</summary>
    public void ReleaseAll() => _ = _controller.ReleaseAll();

    /// <summary>Applies what the engine holds and the texts of the interface language.</summary>
    /// <param name="visible">Whether something is held.</param>
    /// <param name="heldMessage">What is held.</param>
    /// <param name="releaseAllName">The name of the button.</param>
    internal void Apply(bool visible, string heldMessage, string releaseAllName)
    {
        HeldMessage = heldMessage;
        ReleaseAllName = releaseAllName;
        IsVisible = visible;
    }
}
