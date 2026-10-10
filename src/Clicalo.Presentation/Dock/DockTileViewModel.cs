using Clicalo.Application.Coordinators;
using Clicalo.Domain.Primitives;
using Clicalo.Domain.Touch;
using Clicalo.Domain.VoiceNumbering;
using Clicalo.Presentation.Panel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Clicalo.Presentation.Dock;

/// <summary>
/// One shortcut of the bar of the Tab view or of its «Pinned» window (PES-007, PES-010): icon and name, without keys or
/// badges, but with its accessible state, its voice number, the warn outline of an armed confirmation (EJE-002) and the
/// slight outline of an ignored touch (TAC-003). It forwards what happens on it to the
/// <see cref="PanelInteractionController"/>, like a tile of the panel, and decides nothing.
/// </summary>
public sealed class DockTileViewModel : ObservableObject, IIgnoredTouchState
{
    private readonly PanelInteractionController _controller;
    private TileModel _model;
    private bool _isLatched;
    private bool _isArmed;
    private bool _isIgnored;
    private string _accessibleState = string.Empty;
    private string _accessibleHelpText = string.Empty;
    private int? _voiceNumber;

    /// <summary>Creates the tile of <paramref name="model"/>.</summary>
    /// <param name="model">The shortcut as the panel projects it.</param>
    /// <param name="controller">Where its gestures go.</param>
    public DockTileViewModel(TileModel model, PanelInteractionController controller)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(controller);
        _model = model;
        _controller = controller;
    }

    /// <summary>The shortcut.</summary>
    public ShortcutId Id => _model.Id;

    /// <summary>How it reacts to the finger.</summary>
    public TileBehavior Behavior => _model.Behavior;

    /// <summary>What it runs.</summary>
    public TileBinding Binding => _model.Binding;

    /// <summary>The Material Symbols icon.</summary>
    public string Icon => _model.Icon.Name;

    /// <summary>The category, for the color of the icon.</summary>
    public string Category => _model.Category.Value;

    /// <summary>The name.</summary>
    public string AccessibleName => _model.Name;

    /// <summary>«Manteniendo» or «Activo» while it holds or is latched; empty otherwise.</summary>
    public string AccessibleState
    {
        get => _accessibleState;
        private set => SetProperty(ref _accessibleState, value);
    }

    /// <summary>What a tap does.</summary>
    public string AccessibleHelpText
    {
        get => _accessibleHelpText;
        private set => SetProperty(ref _accessibleHelpText, value);
    }

    /// <summary>Whether it holds or is latched.</summary>
    public bool IsLatched
    {
        get => _isLatched;
        private set => SetProperty(ref _isLatched, value);
    }

    /// <summary>
    /// Whether the first tap armed the shortcut and it waits for the confirmation tap (EJE-002): it shows a warn
    /// outline and <see cref="AccessibleState"/> says so, as on the panel.
    /// </summary>
    public bool IsArmed
    {
        get => _isArmed;
        private set => SetProperty(ref _isArmed, value);
    }

    /// <summary>
    /// Whether the touch filter just ignored a touch on the shortcut (TAC-003): a slight outline shows for
    /// <c>Timings.Touch.IgnoredTouchFeedback</c>, so the person knows nothing was sent.
    /// </summary>
    public bool IsIgnored
    {
        get => _isIgnored;
        private set => SetProperty(ref _isIgnored, value);
    }

    /// <summary>Starts or ends the outline of an ignored touch (the composition times it, TAC-003).</summary>
    /// <param name="ignored">Whether it shows.</param>
    public void ShowIgnored(bool ignored) => IsIgnored = ignored;

    /// <summary>The voice number (ACC-009); <see langword="null"/> when they are off.</summary>
    public int? VoiceNumber
    {
        get => _voiceNumber;
        private set
        {
            if (SetProperty(ref _voiceNumber, value))
            {
                OnPropertyChanged(nameof(SpokenName));
            }
        }
    }

    /// <summary>«{n} {name}» with a voice number, the name otherwise (ACC-009).</summary>
    public string SpokenName =>
        VoiceNumber is { } number ? VoiceNumbers.Prefix(number, AccessibleName) : AccessibleName;

    /// <summary>An accepted tap lifted on the tile.</summary>
    /// <param name="contactId">The pointer id.</param>
    /// <param name="device">Finger, pen or mouse.</param>
    /// <param name="summary">Duration, displacement and palm.</param>
    /// <param name="at">When it lifted.</param>
    public void Tapped(
        uint contactId,
        PointerKind device,
        ContactSummary summary,
        DateTimeOffset at
    ) => _ = _controller.Tapped(_model.Binding, contactId, device, summary, at);

    /// <summary>A Mantener starts holding under the contact.</summary>
    /// <param name="contactId">The pointer id.</param>
    /// <param name="device">Finger, pen or mouse.</param>
    /// <param name="at">When.</param>
    public void HoldStarted(uint contactId, PointerKind device, DateTimeOffset at) =>
        _ = _controller.HoldStarted(_model.Binding, contactId, device, at);

    /// <summary>UI Automation Invoke or Toggle (EJE-005): no contact, so a Mantener behaves as a toggle.</summary>
    public void Invoke() => _ = _controller.Invoked(_model.Binding);

    internal void Update(TileModel model)
    {
        var renamed = !string.Equals(model.Name, _model.Name, StringComparison.Ordinal);
        var reiconed = model.Icon != _model.Icon || model.Category != _model.Category;
        _model = model;
        if (renamed)
        {
            OnPropertyChanged(nameof(AccessibleName));
            OnPropertyChanged(nameof(SpokenName));
        }

        if (reiconed)
        {
            OnPropertyChanged(nameof(Icon));
            OnPropertyChanged(nameof(Category));
        }
    }

    internal void ApplyState(bool latched, bool armed, string state, string helpText)
    {
        IsLatched = latched;
        IsArmed = armed;
        AccessibleState = state;
        AccessibleHelpText = helpText;
    }

    internal void ApplyVoiceNumber(int? number) => VoiceNumber = number;
}
