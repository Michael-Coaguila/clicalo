using Clicalo.Application.Coordinators;
using Clicalo.Domain.Primitives;
using Clicalo.Domain.Touch;
using Clicalo.Domain.VoiceNumbering;
using Clicalo.Presentation.Panel.Search;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Clicalo.Presentation.Panel;

/// <summary>
/// One tile of the panel (blueprint §8.2): the view binds to its name and state and forwards what happens on the tile
/// as intentions; the <see cref="PanelInteractionController"/> turns them into engine events. It decides nothing.
/// Every gesture has an equivalent without gesture (§8.6): a hold is a latched toggle when invoked (EJE-005).
/// </summary>
public sealed class TileViewModel : ObservableObject
{
    private readonly PanelInteractionController _controller;
    private readonly SearchResultViewModel? _result;
    private TileModel _model;
    private string _accessibleState = string.Empty;
    private string _accessibleHelpText = string.Empty;
    private string _badge = string.Empty;
    private bool _isLatched;
    private bool _isFlashing;
    private int? _voiceNumber;

    /// <summary>Creates the tile.</summary>
    /// <param name="model">What it shows and runs.</param>
    /// <param name="controller">Where its intentions go.</param>
    public TileViewModel(TileModel model, PanelInteractionController controller)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(controller);
        _model = model;
        _controller = controller;
    }

    /// <summary>
    /// Creates the tile of a search result (BUS-005): it looks like the others, and what happens on it goes to the
    /// search, which gives the foreground back before it runs (BUS-002 c).
    /// </summary>
    /// <param name="model">What it shows and runs.</param>
    /// <param name="controller">The panel's controller (unused by a result, kept for the shared state).</param>
    /// <param name="result">The search result.</param>
    internal TileViewModel(
        TileModel model,
        PanelInteractionController controller,
        SearchResultViewModel result
    )
        : this(model, controller) => _result = result;

    /// <summary>The shortcut id: the key of the tile.</summary>
    public ShortcutId Id => _model.Id;

    /// <summary>How the tile reacts to the finger.</summary>
    public TileBehavior Behavior => _model.Behavior;

    /// <summary>What the tile runs.</summary>
    public TileBinding Binding => _model.Binding;

    /// <summary>The Material Symbols name of the icon (CUA-007).</summary>
    public string Icon => _model.Icon.Name;

    /// <summary>The color category id (TEM-003), such as <c>edit</c> or <c>voice</c>.</summary>
    public string Category => _model.Category.Value;

    /// <summary>
    /// The line under the name (CUA-007): the combination it sends (abbreviated in S, CUA-008), or its origin.
    /// </summary>
    public string Keys => _model.Keys;

    /// <summary>The same line with full key names, for screen readers (CUA-008).</summary>
    public string SpokenKeys => _model.SpokenKeys;

    /// <summary>The type badge in words (CUA-007): «MANTENER», «ALTERNAR» or empty.</summary>
    public string Badge
    {
        get => _badge;
        private set => SetProperty(ref _badge, value);
    }

    /// <summary>The name shown on the tile and exposed to UI Automation (REG-06).</summary>
    public string AccessibleName => _model.Name;

    /// <summary>
    /// The state in words (ACC-003: never color alone): «Manteniendo» while a hold is down, the latched text while a
    /// toggle is on, empty otherwise.
    /// </summary>
    public string AccessibleState
    {
        get => _accessibleState;
        private set => SetProperty(ref _accessibleState, value);
    }

    /// <summary>The kind of the action in words («Pulsar», «Mantener», «Alternar»), as help text for screen readers.</summary>
    public string AccessibleHelpText
    {
        get => _accessibleHelpText;
        private set => SetProperty(ref _accessibleHelpText, value);
    }

    /// <summary>Whether the engine holds something for this tile (a hold under the finger or a latched toggle).</summary>
    public bool IsLatched
    {
        get => _isLatched;
        private set => SetProperty(ref _isLatched, value);
    }

    /// <summary>Whether the tile flashes its color because its action just ran (EJE-012, CUA-009).</summary>
    public bool IsFlashing
    {
        get => _isFlashing;
        private set => SetProperty(ref _isFlashing, value);
    }

    /// <summary>Starts or ends the flash of the tile (the composition times it, EJE-012).</summary>
    /// <param name="flashing">Whether it flashes.</param>
    public void Flash(bool flashing) => IsFlashing = flashing;

    /// <summary>
    /// Its voice number while «Numbers for voice» is on (ACC-009, ACC-010), shown in yellow at the top left; the UI
    /// Automation name then reads <see cref="SpokenName"/>. <see langword="null"/> when off.
    /// </summary>
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

    /// <summary>The name voice users say: «{n} {name}» with a voice number, the name otherwise (ACC-009).</summary>
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
    )
    {
        if (_result is { } result)
        {
            result.Tapped(contactId, device, summary, at);
            return;
        }

        _ = _controller.Tapped(_model.Binding, contactId, device, summary, at);
    }

    /// <summary>
    /// A hold started on the tile. Its end belongs to the contact, not to the tile
    /// (<see cref="PanelViewModel.HoldEnded"/>, INV-9).
    /// </summary>
    /// <param name="contactId">The pointer id that owns the hold.</param>
    /// <param name="device">Finger, pen or mouse.</param>
    /// <param name="at">When it started.</param>
    public void HoldStarted(uint contactId, PointerKind device, DateTimeOffset at) =>
        _ = _controller.HoldStarted(_model.Binding, contactId, device, at);

    /// <summary>A UI Automation Invoke or Toggle (voice, keyboard or switch, EJE-005).</summary>
    public void Invoke()
    {
        if (_result is { } result)
        {
            result.Invoke();
            return;
        }

        _ = _controller.Invoked(_model.Binding);
    }

    /// <summary>Takes a newer projection of the same shortcut (a rename, a language change).</summary>
    /// <param name="model">The newer projection; its <see cref="TileModel.Id"/> must match.</param>
    internal void Update(TileModel model)
    {
        if (model.Id != _model.Id)
        {
            throw new ArgumentException("A tile keeps its shortcut.", nameof(model));
        }

        var renamed = !string.Equals(model.Name, _model.Name, StringComparison.Ordinal);
        var reiconed = model.Icon != _model.Icon;
        var recolored = model.Category != _model.Category;
        var rekeyed =
            !string.Equals(model.Keys, _model.Keys, StringComparison.Ordinal)
            || !string.Equals(model.SpokenKeys, _model.SpokenKeys, StringComparison.Ordinal);
        _model = model;
        if (rekeyed)
        {
            OnPropertyChanged(nameof(Keys));
            OnPropertyChanged(nameof(SpokenKeys));
        }

        if (renamed)
        {
            OnPropertyChanged(nameof(AccessibleName));
            OnPropertyChanged(nameof(SpokenName));
        }

        if (reiconed)
        {
            OnPropertyChanged(nameof(Icon));
        }

        if (recolored)
        {
            OnPropertyChanged(nameof(Category));
        }
    }

    /// <summary>Applies its voice number, or <see langword="null"/> when the option is off.</summary>
    /// <param name="number">The number.</param>
    internal void ApplyVoiceNumber(int? number) => VoiceNumber = number;

    /// <summary>Applies the engine state and the texts of the interface language.</summary>
    /// <param name="latched">Whether the engine holds something for the tile.</param>
    /// <param name="state">The state in words.</param>
    /// <param name="helpText">The kind of the action in words.</param>
    /// <param name="badge">The type badge in words.</param>
    internal void ApplyState(bool latched, string state, string helpText, string badge)
    {
        IsLatched = latched;
        AccessibleState = state;
        AccessibleHelpText = helpText;
        Badge = badge;
    }
}
