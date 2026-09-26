using System.Collections.ObjectModel;
using Clicalo.Application.Coordinators;
using Clicalo.Application.Engine;
using Clicalo.Application.Localization;
using Clicalo.Application.Session;
using Clicalo.Domain.KeySafety;
using Clicalo.Domain.Messages;
using Clicalo.Domain.Primitives;
using Clicalo.Domain.Touch;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Clicalo.Presentation.Panel;

/// <summary>
/// The minimal panel of M2 (blueprint §8.2): the tiles of one profile, the panic strip and the presence of the panel,
/// all applied from immutable inputs (<see cref="PanelModel"/>, <see cref="PanelSession"/>, <see cref="EngineSnapshot"/>)
/// on the UI thread of the Surfaces role. It keeps no rule of its own: tiles forward intentions to the
/// <see cref="PanelInteractionController"/>, and every visible text is formatted from <c>data/i18n</c> when applied,
/// so a language change only needs <see cref="Relocalize"/> (IDI-001).
/// </summary>
public sealed class PanelViewModel : ObservableObject
{
    private readonly PanelInteractionController _controller;
    private readonly ILocalizationContext _localization;
    private readonly Func<ShortcutId, string?> _nameOfShortcut;
    private PanelModel _model = PanelModel.Empty;
    private EngineSnapshot _engine = EngineSnapshot.Empty;
    private TouchSettings _touch;
    private string _accessibleName = string.Empty;
    private bool _isVisible;

    /// <summary>Creates the panel.</summary>
    /// <param name="controller">Where the intentions of the tiles go.</param>
    /// <param name="localization">The interface language.</param>
    /// <param name="touch">The touch filter of the surfaces (TAC-001, TAC-002).</param>
    /// <param name="nameOfShortcut">
    /// The name of a shortcut that is not on the panel (the panic strip names every held shortcut, SEG-002), in the
    /// interface language, or <see langword="null"/> when it no longer exists.
    /// </param>
    public PanelViewModel(
        PanelInteractionController controller,
        ILocalizationContext localization,
        TouchSettings touch,
        Func<ShortcutId, string?> nameOfShortcut
    )
    {
        ArgumentNullException.ThrowIfNull(controller);
        ArgumentNullException.ThrowIfNull(localization);
        ArgumentNullException.ThrowIfNull(nameOfShortcut);
        _controller = controller;
        _localization = localization;
        _touch = touch;
        _nameOfShortcut = nameOfShortcut;
        Panic = new PanicStripViewModel(controller);
        Relocalize();
    }

    /// <summary>The tiles in display order; a tile keeps its view model while its shortcut stays on the panel.</summary>
    public ObservableCollection<TileViewModel> Tiles { get; } = [];

    /// <summary>The panic strip (SEG-002).</summary>
    public PanicStripViewModel Panic { get; }

    /// <summary>The profile in view.</summary>
    public ProfileId Profile => _model.Profile;

    /// <summary>The panel's own name for UI Automation.</summary>
    public string AccessibleName
    {
        get => _accessibleName;
        private set => SetProperty(ref _accessibleName, value);
    }

    /// <summary>Whether the panel is on screen.</summary>
    public bool IsVisible
    {
        get => _isVisible;
        private set => SetProperty(ref _isVisible, value);
    }

    /// <summary>The touch filter the surface's gesture recognizer uses.</summary>
    public TouchSettings Touch
    {
        get => _touch;
        private set => SetProperty(ref _touch, value);
    }

    /// <summary>The latest engine state applied.</summary>
    public EngineSnapshot Engine => _engine;

    /// <summary>
    /// Applies a projection: tiles whose shortcut stays keep their view model (and its UI Automation element), new
    /// ones are added and removed ones dropped, in the new order.
    /// </summary>
    /// <param name="model">The projection.</param>
    public void Apply(PanelModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        _model = model;
        var existing = Tiles.ToDictionary(static tile => tile.Id);
        var sameOrder =
            existing.Count == model.Tiles.Length
            && model
                .Tiles.Select(static tile => tile.Id)
                .SequenceEqual(Tiles.Select(static tile => tile.Id));
        if (sameOrder)
        {
            foreach (var tile in model.Tiles)
            {
                existing[tile.Id].Update(tile);
            }
        }
        else
        {
            var next = model
                .Tiles.Select(tile =>
                {
                    if (existing.TryGetValue(tile.Id, out var kept))
                    {
                        kept.Update(tile);
                        return kept;
                    }

                    return new TileViewModel(tile, _controller);
                })
                .ToList();
            Tiles.Clear();
            foreach (var tile in next)
            {
                Tiles.Add(tile);
            }
        }

        OnPropertyChanged(nameof(Profile));
        ApplyEngine(_engine);
    }

    /// <summary>Applies the session: the presence of the panel.</summary>
    /// <param name="session">The session.</param>
    public void ApplySession(PanelSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        IsVisible = session.Presence == PanelPresence.Visible;
    }

    /// <summary>Applies the touch filter settings.</summary>
    /// <param name="touch">The touch filter.</param>
    public void ApplyTouch(TouchSettings touch) => Touch = touch;

    /// <summary>
    /// Applies what the engine holds: the panic strip appears while anything is held (SEG-002) and names every held
    /// shortcut; each tile shows whether it holds («Manteniendo») or is latched.
    /// </summary>
    /// <param name="snapshot">The engine snapshot.</param>
    public void ApplyEngine(EngineSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        _engine = snapshot;
        var localizer = _localization.Current;
        var held = new Dictionary<ShortcutId, PressedItem>();
        foreach (var item in snapshot.Held)
        {
            if (item.Shortcut is { } shortcut)
            {
                held.TryAdd(shortcut, item);
            }
        }

        foreach (var tile in Tiles)
        {
            var isHeld = held.TryGetValue(tile.Id, out var item);
            tile.ApplyState(
                isHeld,
                isHeld ? localizer.Format(StateOf(item!)) : string.Empty,
                localizer.Format(HelpOf(tile.Behavior))
            );
        }

        var names = held.Keys.Select(NameOf).OfType<string>().Where(static name => name.Length > 0);
        Panic.Apply(
            visible: !snapshot.Held.IsEmpty,
            localizer.Format(L.PanicMsg(keys: string.Join(", ", names))),
            localizer.Format(L.ReleaseAll)
        );
    }

    /// <summary>Formats every text again in the current language (IDI-001).</summary>
    public void Relocalize()
    {
        AccessibleName = _localization.Current.Format(L.AppName);
        ApplyEngine(_engine);
    }

    private static Message StateOf(PressedItem item) =>
        item.ContactId is null ? L.Latched : L.Holding;

    private static Message HelpOf(TileBehavior behavior) =>
        behavior switch
        {
            TileBehavior.Hold => L.THold,
            TileBehavior.Toggle => L.TToggle,
            _ => L.TTap,
        };

    private string? NameOf(ShortcutId shortcut) =>
        Tiles.FirstOrDefault(tile => tile.Id == shortcut)?.AccessibleName
        ?? _nameOfShortcut(shortcut);
}
