using Clicalo.Application.Confirmation;
using Clicalo.Application.Localization;
using Clicalo.Application.Store;
using Clicalo.Domain.Catalog;
using Clicalo.Domain.Commands;
using Clicalo.Domain.Frequents;
using Clicalo.Domain.Messages;
using Clicalo.Domain.Primitives;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Clicalo.Presentation.Panel.EditMode;

/// <summary>
/// Edit mode of the panel (CUA-012, CUA-013, docs/04 «Anatomía de un botón»): ✏ turns it on with the sticky notice
/// [editHint]; a tap on a tile (the Always visible row and the search included) opens it in the editor of the control
/// center; the red × of every grid tile arms «[delConfirm]» for 3.5 s on the first tap and deletes on the second, with
/// [deleted] and undo (REG-04, REG-07); in Frequents the × removes the tile from Frequents instead (CUA-013) and in the
/// search there is none; the dashed «+ [add]» tile at the end opens the library, except in Frequents and the search.
/// ✓ leaves and removes the notice.
/// </summary>
/// <remarks>
/// It decides no rule: what the × does and whether «+ Añadir» shows are <see cref="EditModeRules"/>'s, the two taps are
/// <see cref="TwoStepConfirm"/>'s (the only issuer of a <see cref="ConfirmationToken"/>, CLC0010) and the deletion is a
/// document command. Lives on the UI thread of the Surfaces role; the timer that disarms the × comes back through
/// <c>post</c>. While it is on there is no Hold, long press or Repeat (the surface asks
/// <see cref="TileInteractionModes"/>; the body hides ↻ through <see cref="PanelBodyContext.EditMode"/>).
/// </remarks>
public sealed class EditModeViewModel : ObservableObject
{
    private const string EditIcon = "edit";
    private const string DeletedIcon = "delete";
    private const string HiddenIcon = "visibility_off";

    private readonly DocumentStore _store;
    private readonly TwoStepConfirm _confirm;
    private readonly ILocalizationContext _localization;
    private readonly IPanelNoticeSink _notices;
    private readonly IControlCenterIntents _controlCenter;
    private readonly TimeProvider _time;
    private readonly Action<Action> _post;
    private ITimer? _disarm;
    private ProfileId _profile = ProfileId.General;
    private bool _isOn;
    private TileRemoval _removal = TileRemoval.Delete;
    private bool _showsAdd = true;
    private ShortcutId? _armed;
    private string _addText = string.Empty;
    private string _confirmText = string.Empty;

    /// <summary>Creates edit mode, off.</summary>
    /// <param name="store">The document: deletions and Frequents removals are commands with undo.</param>
    /// <param name="confirm">The two taps of destructive operations (one per role).</param>
    /// <param name="localization">The interface language.</param>
    /// <param name="notices">The notice bar.</param>
    /// <param name="controlCenter">Where the editor and the library open.</param>
    /// <param name="time">The clock of the armed ×.</param>
    /// <param name="post">Runs an action on the UI thread without waiting (the dispatcher's <c>BeginInvoke</c>).</param>
    public EditModeViewModel(
        DocumentStore store,
        TwoStepConfirm confirm,
        ILocalizationContext localization,
        IPanelNoticeSink notices,
        IControlCenterIntents controlCenter,
        TimeProvider time,
        Action<Action> post
    )
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(confirm);
        ArgumentNullException.ThrowIfNull(localization);
        ArgumentNullException.ThrowIfNull(notices);
        ArgumentNullException.ThrowIfNull(controlCenter);
        ArgumentNullException.ThrowIfNull(time);
        ArgumentNullException.ThrowIfNull(post);
        _store = store;
        _confirm = confirm;
        _localization = localization;
        _notices = notices;
        _controlCenter = controlCenter;
        _time = time;
        _post = post;
        Relocalize();
    }

    /// <summary>Whether edit mode is on (✏ shows ✓, the tiles show their ×).</summary>
    public bool IsOn
    {
        get => _isOn;
        private set => SetProperty(ref _isOn, value);
    }

    /// <summary>What the × of the grid tiles does in the view in front.</summary>
    public TileRemoval Removal
    {
        get => _removal;
        private set
        {
            if (SetProperty(ref _removal, value))
            {
                OnPropertyChanged(nameof(ShowsRemove));
            }
        }
    }

    /// <summary>Whether the grid tiles show their red × (on, and not in the search).</summary>
    public bool ShowsRemove => _isOn && _removal != TileRemoval.None;

    /// <summary>Whether the dashed «+ [add]» tile ends the grid.</summary>
    public bool ShowsAdd => _isOn && _showsAdd;

    /// <summary>[add] «Añadir», the text of the dashed tile (its icon is <c>add</c>).</summary>
    public string AddText
    {
        get => _addText;
        private set => SetProperty(ref _addText, value);
    }

    /// <summary>[delConfirm] «Confirmar», what an armed × says.</summary>
    public string ConfirmText
    {
        get => _confirmText;
        private set => SetProperty(ref _confirmText, value);
    }

    /// <summary>The tile whose × is armed, or <see langword="null"/>.</summary>
    public ShortcutId? Armed
    {
        get => _armed;
        private set => SetProperty(ref _armed, value);
    }

    /// <summary>✏ / ✓ of the header.</summary>
    public void Toggle()
    {
        if (IsOn)
        {
            Exit();
        }
        else
        {
            Enter();
        }
    }

    /// <summary>
    /// ✏: turns edit mode on and shows [editHint] until it ends. The composition also closes Quick settings (AJR-001).
    /// </summary>
    public void Enter()
    {
        if (IsOn)
        {
            return;
        }

        IsOn = true;
        RaiseShown();
        _notices.ShowSticky(
            this,
            new PanelNotice(L.EditHint, new IconRef(EditIcon), NoticeTone.Notice)
        );
    }

    /// <summary>✓: leaves edit mode, disarms the × and removes [editHint].</summary>
    public void Exit()
    {
        if (!IsOn)
        {
            return;
        }

        Disarm();
        IsOn = false;
        RaiseShown();
        _notices.ClearSticky(this);
    }

    /// <summary>The view in front changed: the × and «+ Añadir» follow it.</summary>
    /// <param name="frequents">Whether Frequents is in view.</param>
    /// <param name="searching">Whether the search shows results in place of the list.</param>
    /// <param name="profile">The profile in view (where «+ Añadir» adds).</param>
    public void ApplyView(bool frequents, bool searching, ProfileId profile)
    {
        _profile = profile;
        var removal = EditModeRules.RemovalIn(frequents, searching);
        var showsAdd = EditModeRules.OffersAdd(frequents, searching);
        if (removal != _removal)
        {
            Disarm();
        }

        Removal = removal;
        if (showsAdd != _showsAdd)
        {
            _showsAdd = showsAdd;
            OnPropertyChanged(nameof(ShowsAdd));
        }
    }

    /// <summary>
    /// A tap on a tile (or its UI Automation Invoke): in edit mode it opens the tile in the editor and goes no further
    /// (EJE-001 step 2).
    /// </summary>
    /// <param name="shortcut">The tile's shortcut.</param>
    /// <returns>Whether edit mode took it (the tile must not run).</returns>
    public bool OnTap(ShortcutId shortcut)
    {
        if (!IsOn)
        {
            return false;
        }

        Disarm();
        _controlCenter.OpenEditor(shortcut);
        return true;
    }

    /// <summary>
    /// The × of a tile: the first tap arms it («[delConfirm]» for 3.5 s), the second deletes the shortcut, or removes it
    /// from Frequents, with undo.
    /// </summary>
    /// <param name="shortcut">The tile's shortcut.</param>
    public void Remove(ShortcutId shortcut)
    {
        if (!ShowsRemove)
        {
            return;
        }

        var operation =
            _removal == TileRemoval.HideFromFrequents
                ? nameof(HideFromFrequents)
                : nameof(DeleteShortcut);
        switch (_confirm.Tap(new ConfirmationSubject(operation, shortcut.Value)))
        {
            case TwoStepResult.Armed armed:
                Arm(shortcut, armed.Until);
                break;
            case TwoStepResult.Confirmed confirmed:
                Disarm();
                Removed(shortcut, confirmed.Token);
                break;
        }
    }

    /// <summary>«+ [add]»: the library for the profile in view.</summary>
    public void Add()
    {
        if (ShowsAdd)
        {
            _controlCenter.OpenLibrary(_profile);
        }
    }

    /// <summary>The accessible name of a tile's ×: «Eliminar {name}», or «Quitar {name} de Frecuentes» in Frequents.</summary>
    /// <param name="tileName">The tile's name.</param>
    public string RemoveNameOf(string tileName) =>
        _localization.Current.Format(
            _removal == TileRemoval.HideFromFrequents
                ? L.HideA(name: tileName)
                : L.DelA(name: tileName)
        );

    /// <summary>Formats every text again in the current language (IDI-001).</summary>
    public void Relocalize()
    {
        var localizer = _localization.Current;
        AddText = localizer.Format(L.Add);
        ConfirmText = localizer.Format(L.DelConfirm);
    }

    private void Removed(ShortcutId shortcut, ConfirmationToken token)
    {
        if (_removal == TileRemoval.HideFromFrequents)
        {
            // CUA-013: the shortcut stays in its profile; removing from Frequents is a value edit with undo.
            if (_store.Dispatch(new HideFromFrequents(shortcut)).IsSuccess)
            {
                Notify(L.CtxHideT, HiddenIcon);
            }

            return;
        }

        if (_store.Dispatch(new DeleteShortcut(shortcut), token).IsSuccess)
        {
            Notify(L.Deleted, DeletedIcon);
        }
    }

    private void Notify(Message text, string icon) =>
        _notices.Notify(
            new PanelNotice(text, new IconRef(icon), NoticeTone.Notice, CanUndo: _store.CanUndo)
        );

    private void Arm(ShortcutId shortcut, DateTimeOffset until)
    {
        Armed = shortcut;
        _disarm?.Dispose();
        var wait = until - _time.GetUtcNow();
        _disarm = _time.CreateTimer(
            _ => _post(() => Expire(shortcut)),
            null,
            wait > TimeSpan.Zero ? wait : TimeSpan.Zero,
            Timeout.InfiniteTimeSpan
        );
    }

    private void Expire(ShortcutId shortcut)
    {
        // The window of TwoStepConfirm is over: a third tap arms again instead of deleting.
        if (Armed == shortcut)
        {
            Armed = null;
        }
    }

    private void Disarm()
    {
        _disarm?.Dispose();
        _disarm = null;
        if (Armed is not null)
        {
            _confirm.Disarm();
            Armed = null;
        }
    }

    private void RaiseShown()
    {
        OnPropertyChanged(nameof(ShowsRemove));
        OnPropertyChanged(nameof(ShowsAdd));
    }
}
