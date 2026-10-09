using System.Collections.ObjectModel;
using Clicalo.Application.Localization;
using Clicalo.Application.Store;
using Clicalo.Domain.Catalog;
using Clicalo.Domain.Commands;
using Clicalo.Domain.Frequents;
using Clicalo.Domain.Messages;
using Clicalo.Domain.Primitives;
using Clicalo.Domain.Timing;
using Clicalo.Presentation.Panel.EditMode;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Clicalo.Presentation.Panel.ContextMenu;

/// <summary>
/// The context menu of a tile (CUA-014, CUA-015, FRE-001), opened inside the panel over the grid by a long press of
/// 600 ms (never on a Hold tile and never in edit mode), a right click, the Menu key or the accessible secondary action.
/// A header with the tile's icon and name and 44 px rows: [ctxPin] or [ctxUnpin]; [ctxHide] only in Frequents and when
/// not pinned; [edit]; [cancel]. Pinning, unpinning and hiding are document commands with [ctxPinT], [ctxUnpinT] or
/// [ctxHideT] and undo (AVI-005); pinning past the tiles Frequents shows warns with «pinLimit». Esc, a tap outside or a
/// change of context closes it (<see cref="Close"/>).
/// </summary>
/// <remarks>
/// The rows are <see cref="TileMenu.Items"/>; the pin limit is <see cref="FrequentsProjection.IsPinLimitReached"/>. The
/// tap that opened the menu never runs the tile (the recognizer reports a long press instead of a tap).
/// </remarks>
public sealed class TileContextMenuViewModel : ObservableObject
{
    private const string PinIcon = "keep";
    private const string UnpinIcon = "keep_off";
    private const string HideIcon = "visibility_off";
    private const string EditIcon = "edit";
    private const string CancelIcon = "close";

    private readonly DocumentStore _store;
    private readonly ILocalizationContext _localization;
    private readonly IPanelNoticeSink _notices;
    private readonly IControlCenterIntents _controlCenter;
    private bool _isOpen;
    private ShortcutId? _shortcut;
    private bool _inFrequents;
    private string _title = string.Empty;
    private string _icon = string.Empty;
    private string _accessibleName = string.Empty;

    /// <summary>Creates the menu, closed.</summary>
    /// <param name="store">The document: pins and hidden are commands with undo.</param>
    /// <param name="localization">The interface language.</param>
    /// <param name="notices">The notice bar.</param>
    /// <param name="controlCenter">Where [edit] opens the editor.</param>
    public TileContextMenuViewModel(
        DocumentStore store,
        ILocalizationContext localization,
        IPanelNoticeSink notices,
        IControlCenterIntents controlCenter
    )
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(localization);
        ArgumentNullException.ThrowIfNull(notices);
        ArgumentNullException.ThrowIfNull(controlCenter);
        _store = store;
        _localization = localization;
        _notices = notices;
        _controlCenter = controlCenter;
        Relocalize();
    }

    /// <summary>Whether the menu is open (the panel does not dim, docs/04 «Opacidad y atenuado»).</summary>
    public bool IsOpen
    {
        get => _isOpen;
        private set => SetProperty(ref _isOpen, value);
    }

    /// <summary>The tile the menu is about, or <see langword="null"/> while closed.</summary>
    public ShortcutId? Shortcut
    {
        get => _shortcut;
        private set => SetProperty(ref _shortcut, value);
    }

    /// <summary>The header: the tile's name.</summary>
    public string Title
    {
        get => _title;
        private set => SetProperty(ref _title, value);
    }

    /// <summary>The header's icon: the tile's Material Symbols icon.</summary>
    public string Icon
    {
        get => _icon;
        private set => SetProperty(ref _icon, value);
    }

    /// <summary>The menu's accessible name, [moreOpts] «Más opciones».</summary>
    public string AccessibleName
    {
        get => _accessibleName;
        private set => SetProperty(ref _accessibleName, value);
    }

    /// <summary>The rows, in order.</summary>
    public ObservableCollection<TileMenuRowViewModel> Rows { get; } = [];

    /// <summary>Opens the menu of a tile (another open menu is replaced).</summary>
    /// <param name="shortcut">The tile's shortcut.</param>
    /// <param name="name">The tile's name, as shown.</param>
    /// <param name="icon">The tile's icon.</param>
    /// <param name="inFrequents">Whether the tile is in the Frequents view.</param>
    public void Open(ShortcutId shortcut, string name, string icon, bool inFrequents)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(icon);
        _inFrequents = inFrequents;
        Shortcut = shortcut;
        Title = name;
        Icon = icon;
        BuildRows();
        IsOpen = true;
    }

    /// <summary>[cancel], Esc, a tap outside or a change of context (CUA-014).</summary>
    public void Close()
    {
        if (!IsOpen)
        {
            return;
        }

        IsOpen = false;
        Shortcut = null;
        Rows.Clear();
    }

    /// <summary>Formats every text again in the current language (IDI-001).</summary>
    public void Relocalize()
    {
        AccessibleName = _localization.Current.Format(L.MoreOpts);
        if (IsOpen)
        {
            BuildRows();
        }
    }

    private void BuildRows()
    {
        Rows.Clear();
        if (Shortcut is not { } shortcut)
        {
            return;
        }

        var localizer = _localization.Current;
        foreach (var item in TileMenu.Items(_store.Current.Frequents, shortcut, _inFrequents))
        {
            var (icon, label) = item switch
            {
                TileMenuItem.Pin => (PinIcon, L.CtxPin),
                TileMenuItem.Unpin => (UnpinIcon, L.CtxUnpin),
                TileMenuItem.Hide => (HideIcon, L.CtxHide),
                TileMenuItem.Edit => (EditIcon, L.Edit),
                _ => (CancelIcon, L.Cancel),
            };
            Rows.Add(
                new TileMenuRowViewModel(item, icon, localizer.Format(label), () => Run(item))
            );
        }
    }

    private void Run(TileMenuItem item)
    {
        if (Shortcut is not { } shortcut)
        {
            return;
        }

        Close();
        switch (item)
        {
            case TileMenuItem.Pin:
                // FRE-001: pins past the tiles shown are kept, and the menu says Frequents will not show it.
                var full = FrequentsProjection.IsPinLimitReached(
                    _store.Current.Library,
                    _store.Current.Frequents
                );
                Dispatch(
                    new PinToFrequents(shortcut),
                    full ? L.PinLimit(count: Timings.Frequents.MaxShown) : L.CtxPinT,
                    PinIcon
                );
                break;
            case TileMenuItem.Unpin:
                Dispatch(new UnpinFromFrequents(shortcut), L.CtxUnpinT, UnpinIcon);
                break;
            case TileMenuItem.Hide:
                Dispatch(new HideFromFrequents(shortcut), L.CtxHideT, HideIcon);
                break;
            case TileMenuItem.Edit:
                _controlCenter.OpenEditor(shortcut);
                break;
        }
    }

    private void Dispatch(IDocumentCommand command, Message notice, string icon)
    {
        if (_store.Dispatch(command).IsSuccess)
        {
            _notices.Notify(
                new PanelNotice(
                    notice,
                    new IconRef(icon),
                    NoticeTone.Notice,
                    CanUndo: _store.CanUndo
                )
            );
        }
    }
}
