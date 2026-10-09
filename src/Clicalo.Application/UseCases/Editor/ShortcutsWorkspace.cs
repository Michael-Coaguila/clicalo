using Clicalo.Application.Confirmation;
using Clicalo.Application.Localization;
using Clicalo.Application.Store;
using Clicalo.Application.UseCases.Library;
using Clicalo.Domain.Catalog;
using Clicalo.Domain.Commands;
using Clicalo.Domain.Duplicates;
using Clicalo.Domain.Icons;
using Clicalo.Domain.Keys;
using Clicalo.Domain.Library;
using Clicalo.Domain.Messages;
using Clicalo.Domain.Primitives;
using Clicalo.Domain.Privacy;
using Clicalo.Domain.Templates;

namespace Clicalo.Application.UseCases.Editor;

/// <summary>
/// The «Atajos» section of the Control Center as a use case (docs/05 §1, blueprint §6.4 <c>WorkspaceSession</c>): the
/// list in view, what the editor column shows and every edit of a shortcut, each one a document command, so all of it
/// saves itself and can be undone (REG-07). It owns no view state: the view models project it.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>Draft without trace (ATJ-011): «Crear el mío» opens a draft that is not in the document; its first meaningful
/// edit creates it (<see cref="CreateShortcut"/>), and a shortcut that is a blank draft again is discarded when the
/// editor leaves it (<see cref="DiscardDraft"/>), which joins its own undo entry and leaves nothing.</item>
/// <item>One undo step per shortcut (EDI-021): the edits of a shortcut coalesce by its id until the editor moves to
/// another shortcut or profile, which seals the entry.</item>
/// <item>Used by the Workspace role only, one call at a time, on its thread.</item>
/// </list>
/// </remarks>
public sealed class ShortcutsWorkspace
{
    private static readonly CategoryId OwnCategory = new("edit");

    private readonly DocumentStore _store;
    private readonly ILocalizationContext _localization;
    private readonly Func<EditorCatalogs> _catalogs;
    private readonly Func<ProfileId> _shownProfile;
    private readonly Dictionary<ActionKind, ShortcutAction> _remembered = [];
    private EditorPane _beforeLibrary = new EditorPane.Empty();

    /// <summary>Creates the section.</summary>
    /// <param name="store">The document.</param>
    /// <param name="localization">The interface language (names in notices, the copy suffix).</param>
    /// <param name="catalogs">Icons, library and templates, once loaded.</param>
    /// <param name="shownProfile">The profile the panel shows, where an unpinned shortcut goes when its own is gone.</param>
    public ShortcutsWorkspace(
        DocumentStore store,
        ILocalizationContext localization,
        Func<EditorCatalogs> catalogs,
        Func<ProfileId> shownProfile
    )
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(localization);
        ArgumentNullException.ThrowIfNull(catalogs);
        ArgumentNullException.ThrowIfNull(shownProfile);
        _store = store;
        _localization = localization;
        _catalogs = catalogs;
        _shownProfile = shownProfile;
    }

    /// <summary>Raised after the list, the pane or a transient state changed.</summary>
    public event EventHandler? Changed;

    /// <summary>A message for the status bar (CCM-003).</summary>
    public event EventHandler<WorkspaceNoticeEventArgs>? Noticed;

    /// <summary>The list in view: Always visible or a profile (ATJ-002).</summary>
    public ListRef List { get; private set; } = new ListRef.InProfile(ProfileId.General);

    /// <summary>What the editor column shows.</summary>
    public EditorPane Pane { get; private set; } = new EditorPane.Empty();

    /// <summary>The chip chosen in «Añadir atajo»; null for the first one.</summary>
    public string? LibraryCategory { get; private set; }

    /// <summary>
    /// The combination being replaced from a repeated card (REP-006): the strip «[replaceMsg]» with [keepOld] shows
    /// while it is set.
    /// </summary>
    public KeyChord? ReplacedChord { get; private set; }

    /// <summary>The zero-based macro step whose keys, wait, text or mouse action is open (EDI-013).</summary>
    public int? EditingStep { get; private set; }

    /// <summary>The shortcut of the editor: the one of the document, or the draft; null otherwise.</summary>
    public Shortcut? Selected =>
        Pane switch
        {
            EditorPane.Draft draft => draft.Shortcut,
            EditorPane.Editing editing
                when _store.Current.Library.TryGetShortcut(editing.Id, out var shortcut) =>
                shortcut,
            _ => null,
        };

    /// <summary>The shortcuts of the list in view.</summary>
    public ValueList<Shortcut> Shortcuts =>
        _store.Current.Library.TryGetList(List, out var shortcuts) ? shortcuts : [];

    private EditorCatalogs Catalogs => _catalogs();

    private LangCode AppsLanguage => _store.Current.Settings.Keyboard.AppsLanguage;

    /// <summary>
    /// Opens the section on <paramref name="list"/> with <paramref name="shortcut"/> in the editor (the panel's edit
    /// mode, «Editar»), with the library («+ Añadir»), or with the first shortcut of the list.
    /// </summary>
    /// <param name="list">The list; null keeps the current one.</param>
    /// <param name="shortcut">The shortcut to edit; its own list wins.</param>
    /// <param name="library">Whether to open «Añadir atajo».</param>
    public void Open(ListRef? list, ShortcutId? shortcut, bool library)
    {
        Leave();
        var document = _store.Current.Library;
        if (shortcut is { } id && document.TryLocate(id, out var location))
        {
            List = location.List;
            SetPane(new EditorPane.Editing(id));
            return;
        }

        if (list is not null && document.TryGetList(list, out _))
        {
            List = list;
        }

        if (library)
        {
            OpenLibrary();
            return;
        }

        SetPane(FirstOf(List));
    }

    /// <summary>A row of the profiles column: the list and its first shortcut (ATJ-002).</summary>
    /// <param name="list">The list.</param>
    public void SelectList(ListRef list)
    {
        ArgumentNullException.ThrowIfNull(list);
        if (!_store.Current.Library.TryGetList(list, out _))
        {
            return;
        }

        Leave();
        List = list;
        SetPane(FirstOf(list));
    }

    /// <summary>A tile of the grid, a row of a repeated card or a search: the shortcut in the editor.</summary>
    /// <param name="id">The shortcut.</param>
    public void Select(ShortcutId id)
    {
        if (Pane is EditorPane.Editing current && current.Id == id)
        {
            return;
        }

        if (!_store.Current.Library.TryLocate(id, out var location))
        {
            return;
        }

        Leave();
        List = location.List;
        SetPane(new EditorPane.Editing(id));
    }

    /// <summary>«+ Añadir» and the «Biblioteca» tile: «Añadir atajo» (ATJ-010); a blank draft is discarded (ATJ-011).</summary>
    public void OpenLibrary()
    {
        if (Pane is EditorPane.Library)
        {
            return;
        }

        var before = Pane;
        Leave();
        _beforeLibrary = before is EditorPane.Draft ? new EditorPane.Empty() : before;
        SetPane(new EditorPane.Library());
    }

    /// <summary>✕ of «Añadir atajo»: back to the shortcut that was open, if it still exists.</summary>
    public void CloseLibrary()
    {
        if (Pane is not EditorPane.Library)
        {
            return;
        }

        SetPane(
            _beforeLibrary is EditorPane.Editing editing
            && _store.Current.Library.TryGetShortcut(editing.Id, out _)
                ? editing
                : new EditorPane.Empty()
        );
    }

    /// <summary>A category chip of «Añadir atajo».</summary>
    /// <param name="id">The category.</param>
    public void ChooseCategory(string id)
    {
        LibraryCategory = id;
        Raise();
    }

    /// <summary>The categories of «Añadir atajo» for the list in view (ATJ-010).</summary>
    public IReadOnlyList<LibraryCategory> Categories() =>
        LibraryCategories.For(
            _store.Current.Library,
            List,
            Catalogs.Library,
            Catalogs.Starter,
            UiLanguage()
        );

    /// <summary>
    /// «Crear el mío» (ATJ-010): a blank draft (bolt, icon from the name) in the editor, not in the document yet
    /// (ATJ-011), with the notice [newCreated].
    /// </summary>
    public void CreateOwn()
    {
        Leave();
        var draft = new Shortcut(
            new ShortcutId("draft"),
            LocalizedText.Same(string.Empty, LangCode.Es, LangCode.En),
            IconCatalog.ShortcutDefault,
            true,
            OwnCategory,
            new TapAction(KeyChord.Empty, []),
            new ShortcutOptions(false, new HoldLimit.InheritGlobal(), false),
            null,
            null
        );
        SetPane(new EditorPane.Draft(draft));
        Notify(L.NewCreated, "edit_square", undo: false);
    }

    /// <summary>
    /// A row of «Añadir atajo» (ATJ-010): adds the ready action at the end with a new id and its catalog reference,
    /// keeps the library open and says «[addedTo] {perfil}: {nombre}». A row already added does nothing.
    /// </summary>
    /// <param name="category">Its category.</param>
    /// <param name="item">The ready action.</param>
    public void AddFromLibrary(LibraryCategory category, TemplateShortcut item)
    {
        ArgumentNullException.ThrowIfNull(category);
        ArgumentNullException.ThrowIfNull(item);
        if (LibraryMatching.IsAdded(Shortcuts, item, category.Source))
        {
            return;
        }

        var installed = TemplateInstaller.Install(
            item,
            category.Source,
            category.Version,
            AppsLanguage,
            PlaceholderIds.Instance
        );
        if (Dispatch(new CreateShortcut(List, installed, ListPosition.End)))
        {
            _store.SealCoalescing();
            Notify(
                L.AddedToProf(
                    profile: ListName(List),
                    name: item.Name.Get(UiLanguage(), LangCode.Es)
                ),
                "add",
                undo: true
            );
        }
    }

    /// <summary>
    /// The Control Center closes (CCM-001): a blank draft is discarded (ATJ-011) and the open undo entry sealed.
    /// </summary>
    public void Close() => Leave();

    /// <summary>
    /// The document changed (an undo, the panel, another section): a list that is gone becomes General (PER-008) and a
    /// shortcut that is gone leaves the editor; a shortcut that moved is followed to its list.
    /// </summary>
    public void OnDocumentChanged()
    {
        var library = _store.Current.Library;
        var changed = false;
        if (Pane is EditorPane.Editing editing)
        {
            if (library.TryLocate(editing.Id, out var location))
            {
                if (!location.List.Equals(List))
                {
                    List = location.List;
                    changed = true;
                }
            }
            else
            {
                Pane = FirstOf(library.TryGetList(List, out _) ? List : General);
                ClearTransient();
                changed = true;
            }
        }

        if (!library.TryGetList(List, out _))
        {
            List = General;
            Pane = Pane is EditorPane.Library ? Pane : FirstOf(List);
            changed = true;
        }

        if (
            EditingStep is { } step
            && (Selected?.Action is not MacroAction macro || step >= macro.Steps.Count)
        )
        {
            EditingStep = null;
            changed = true;
        }

        if (changed)
        {
            Raise();
        }
    }

    /// <summary>The name field (EDI-001, EDI-002, EDI-003): both languages, and the icon follows while it is automatic.</summary>
    /// <param name="text">What the person typed or dictated.</param>
    public void Rename(string text)
    {
        var name = text ?? string.Empty;
        Edit(shortcut =>
        {
            var renamed = shortcut with { Name = SameInEveryLanguage(name, shortcut.Name) };
            if (!shortcut.AutoIcon)
            {
                return renamed;
            }

            var suggested = IconSuggestions.Suggest(
                name,
                ActionKinds.ChordOf(shortcut.Action),
                AppsLanguage,
                Catalogs.Icons,
                Catalogs.Combos
            );
            return suggested.IsEmpty ? renamed : renamed with { Icon = suggested[0] };
        });
    }

    /// <summary>The icons suggested for the shortcut in the editor (EDI-004, EDI-005).</summary>
    public ValueList<IconRef> SuggestedIcons() =>
        Selected is { } shortcut
            ? IconSuggestions.Suggest(
                Display(shortcut.Name),
                ActionKinds.ChordOf(shortcut.Action),
                AppsLanguage,
                Catalogs.Icons,
                Catalogs.Combos
            )
            : [];

    /// <summary>An icon of the picker: chosen by hand, so it no longer follows the name (EDI-003).</summary>
    /// <param name="icon">The icon.</param>
    public void SetIcon(IconRef icon) =>
        Edit(shortcut => shortcut with { Icon = icon, AutoIcon = false });

    /// <summary>The «Qué hace» grid (EDI-006).</summary>
    /// <param name="kind">The kind chosen.</param>
    public void SetKind(ActionKind kind) =>
        Edit(shortcut =>
        {
            if (shortcut.Action.Kind == kind)
            {
                return shortcut;
            }

            _remembered[shortcut.Action.Kind] = shortcut.Action;
            _remembered.TryGetValue(kind, out var remembered);
            EditingStep = null;
            return shortcut with { Action = ActionKinds.Switch(shortcut.Action, kind, remembered) };
        });

    /// <summary>
    /// A key of the picker or a modifier button (EDI-008, EDI-009): in the combination box of the shortcut, or of the
    /// macro step being edited.
    /// </summary>
    /// <param name="key">The catalog key.</param>
    public void TapKey(KeyId key) => EditChord(chord => ChordEdits.Tap(chord, key));

    /// <summary>× of a key chip (EDI-007).</summary>
    /// <param name="index">The zero-based key.</param>
    public void RemoveKey(int index) => EditChord(chord => ChordEdits.RemoveAt(chord, index));

    /// <summary>⌫ [backKey] (EDI-007).</summary>
    public void RemoveLastKey() => EditChord(ChordEdits.RemoveLast);

    /// <summary>↺ [clearKeys] (EDI-007): its own undo step, with [comboCleared].</summary>
    public void ClearKeys()
    {
        if (ChordInBox() is not { IsEmpty: false })
        {
            return;
        }

        _store.SealCoalescing();
        EditChord(static _ => KeyChord.Empty);
        _store.SealCoalescing();
        Notify(L.ComboCleared, "restart_alt", undo: true);
    }

    /// <summary>The combination of the box: the macro step being edited, or the shortcut's.</summary>
    public KeyChord? ChordInBox() =>
        Selected?.Action switch
        {
            MacroAction macro when EditingStep is { } step => MacroSteps.KeysOf(macro, step),
            { } action => ActionKinds.ChordOf(action),
            _ => null,
        };

    /// <summary>«Usar otra combinación» of a repeated card (REP-006): its own undo step; the box empties.</summary>
    public void UseOtherCombination()
    {
        if (
            Selected?.Action is not { } action
            || ActionKinds.ChordOf(action) is not { IsEmpty: false } chord
        )
        {
            return;
        }

        _store.SealCoalescing();
        ReplacedChord = chord;
        EditChord(static _ => KeyChord.Empty);
        Notify(L.DupChangeT, "swap_horiz", undo: true);
        Raise();
    }

    /// <summary>[keepOld] (REP-006): the combination comes back, with [keptOld].</summary>
    public void KeepOldCombination()
    {
        if (ReplacedChord is not { } old)
        {
            return;
        }

        ReplacedChord = null;
        EditChord(_ => old);
        _store.SealCoalescing();
        Notify(L.KeptOld, "undo", undo: false);
        Raise();
    }

    /// <summary>The text of a Text shortcut (EDI-011), encrypted on disk.</summary>
    /// <param name="text">The text.</param>
    public void SetText(string text) =>
        Edit(shortcut =>
            shortcut.Action is TextAction action
                ? shortcut with
                {
                    Action = action with { Text = SecretText.From(text ?? string.Empty) },
                }
                : shortcut
        );

    /// <summary>[textMethod]: [tmType] or [tmPaste] (EDI-016).</summary>
    /// <param name="method">The method.</param>
    public void SetTextMethod(TextMethod method) =>
        Edit(shortcut =>
            shortcut.Action is TextAction action
                ? shortcut with
                {
                    Action = action with { Method = method },
                }
                : shortcut
        );

    /// <summary>One of the eight mouse actions (EDI-012).</summary>
    /// <param name="op">The action.</param>
    public void SetMouse(MouseOp op) =>
        Edit(shortcut =>
            shortcut.Action is MouseAction action
                ? shortcut with
                {
                    Action = action with { Op = op },
                }
                : shortcut
        );

    /// <summary>The scroll speed (EDI-012).</summary>
    /// <param name="speed">The speed.</param>
    public void SetSpeed(ScrollSpeed speed) =>
        Edit(shortcut =>
            shortcut.Action is MouseAction action
                ? shortcut with
                {
                    Action = action with { Speed = speed },
                }
                : shortcut
        );

    /// <summary>The address of a Web shortcut (EDI-014).</summary>
    /// <param name="text">What the person typed.</param>
    public void SetUrl(string text) =>
        Edit(shortcut =>
            shortcut.Action is UrlAction
                ? shortcut with
                {
                    Action = new UrlAction(Targets.ParseUrl(text)),
                }
                : shortcut
        );

    /// <summary>The target of an App shortcut (EDI-014).</summary>
    /// <param name="text">What the person typed or chose.</param>
    public void SetApp(string text) =>
        Edit(shortcut =>
            shortcut.Action is AppAction
                ? shortcut with
                {
                    Action = new AppAction(Targets.ParseApp(text)),
                }
                : shortcut
        );

    /// <summary>[autoRelease] of a Hold or a Toggle (EDI-016, SEG-004).</summary>
    /// <param name="limit">The limit.</param>
    public void SetHoldLimit(HoldLimit limit) =>
        Edit(shortcut => shortcut with { Options = shortcut.Options with { MaxHold = limit } });

    /// <summary>«Pedir confirmación antes de ejecutar» (EDI-017, EJE-002).</summary>
    /// <param name="on">Whether the first tap only arms.</param>
    public void SetConfirm(bool on) =>
        Edit(shortcut => shortcut with { Options = shortcut.Options with { Confirm = on } });

    /// <summary>«Fijado en Frecuentes» (EDI-018): pins or unpins the shortcut in Frequents.</summary>
    /// <param name="on">The switch.</param>
    public void SetPinnedInFrequents(bool on)
    {
        if (Pane is not EditorPane.Editing editing)
        {
            return;
        }

        IDocumentCommand command = on
            ? new PinToFrequents(editing.Id)
            : new UnpinFromFrequents(editing.Id);
        if (Dispatch(command))
        {
            Notify(on ? L.CtxPinT : L.Saved, "keep", undo: true);
        }
    }

    /// <summary>A «+» button under the steps (EDI-013): the new step opens.</summary>
    /// <param name="kind">The kind of step.</param>
    public void AddStep(MacroStepKind kind)
    {
        if (Selected?.Action is not MacroAction macro)
        {
            return;
        }

        var index = macro.Steps.Count;
        EditMacro(m => MacroSteps.Add(m, kind));
        EditingStep =
            Selected?.Action is MacroAction { Steps.Count: var n } && n > index ? index : null;
        Raise();
    }

    /// <summary>✏ of a step: opens or closes its edition.</summary>
    /// <param name="index">The zero-based step.</param>
    public void ToggleStep(int index)
    {
        EditingStep = EditingStep == index ? null : index;
        Raise();
    }

    /// <summary>[done] of «Editando las teclas del paso N».</summary>
    public void StopEditingStep()
    {
        EditingStep = null;
        Raise();
    }

    /// <summary>↑ or ↓ of a step.</summary>
    /// <param name="index">The zero-based step.</param>
    /// <param name="offset">-1 or 1.</param>
    public void MoveStep(int index, int offset)
    {
        EditMacro(macro => MacroSteps.Move(macro, index, offset));
        if (EditingStep == index)
        {
            EditingStep = index + offset;
            Raise();
        }
    }

    /// <summary>− or + of a wait step.</summary>
    /// <param name="index">The zero-based step.</param>
    /// <param name="steps">How many 100 ms steps.</param>
    public void NudgeWait(int index, int steps) =>
        EditMacro(macro => MacroSteps.Nudge(macro, index, steps));

    /// <summary>The text of a text step.</summary>
    /// <param name="index">The zero-based step.</param>
    /// <param name="text">The text.</param>
    public void SetStepText(int index, string text) =>
        EditMacro(macro => MacroSteps.SetText(macro, index, SecretText.From(text ?? string.Empty)));

    /// <summary>The action of a mouse step.</summary>
    /// <param name="index">The zero-based step.</param>
    /// <param name="op">The action.</param>
    public void SetStepMouse(int index, MouseOp op) =>
        EditMacro(macro => MacroSteps.SetMouse(macro, index, op));

    /// <summary>✕ of a step, confirmed with two taps (EDI-013, REG-04): <see cref="DeleteMacroStep"/>.</summary>
    /// <param name="index">The zero-based step.</param>
    /// <param name="token">The second tap.</param>
    public void DeleteStep(int index, ConfirmationToken token)
    {
        if (Pane is not EditorPane.Editing editing)
        {
            return;
        }

        if (DispatchConfirmed(new DeleteMacroStep(editing.Id, index), token))
        {
            EditingStep = EditingStep switch
            {
                { } open when open == index => null,
                { } open when open > index => open - 1,
                var open => open,
            };
            Notify(L.StepDeleted, "delete", undo: true);
            Raise();
        }
    }

    /// <summary>The position buttons of «Más opciones» (EDI-016).</summary>
    /// <param name="move">The button.</param>
    public void Move(PositionMove move)
    {
        if (
            Pane is not EditorPane.Editing editing
            || !_store.Current.Library.TryLocate(editing.Id, out var location)
            || Positions.Target(move, location.Index, Shortcuts.Count) is not { } target
        )
        {
            return;
        }

        if (Dispatch(new MoveShortcut(editing.Id, List, ListPosition.At(target))))
        {
            Notify(L.Saved, "swap_horiz", undo: true);
        }
    }

    /// <summary>
    /// A tile dropped in the grid (ATJ-009): <paramref name="moved"/> goes before <paramref name="before"/>, or to the
    /// end when it is null. One undo step.
    /// </summary>
    /// <param name="moved">The dragged shortcut.</param>
    /// <param name="before">The tile it was dropped on, or null for the end.</param>
    public void Reorder(ShortcutId moved, ShortcutId? before)
    {
        var others = Shortcuts.Items.Where(s => s.Id != moved).ToList();
        var position =
            before is { } target && others.FindIndex(s => s.Id == target) is var index and >= 0
                ? ListPosition.At(index)
                : ListPosition.End;
        _store.SealCoalescing();
        if (Dispatch(new MoveShortcut(moved, List, position)))
        {
            _store.SealCoalescing();
            Notify(L.Saved, "swap_horiz", undo: true);
        }
    }

    /// <summary>
    /// «Fijar en Siempre visible» (EDI-015): on moves the shortcut to the end of Always visible; off returns it to its
    /// profile, the profile shown, or General. The editor follows it.
    /// </summary>
    /// <param name="on">The switch.</param>
    public void SetPinned(bool on)
    {
        if (Pane is not EditorPane.Editing editing)
        {
            return;
        }

        IDocumentCommand command = on
            ? new PinToAlwaysVisible(editing.Id)
            : new UnpinFromAlwaysVisible(editing.Id, _shownProfile());
        if (Dispatch(command) && _store.Current.Library.TryLocate(editing.Id, out var location))
        {
            List = location.List;
            Notify(L.Saved, "push_pin", undo: true);
            Raise();
        }
    }

    /// <summary>[Duplicar] (EDI-019): the copy goes right after it, selected, with an undo notice.</summary>
    public void Duplicate()
    {
        if (
            Pane is not EditorPane.Editing editing
            || !_store.Current.Library.TryGetShortcut(editing.Id, out var source)
            || !_store.Current.Library.TryLocate(editing.Id, out var location)
        )
        {
            return;
        }

        var suffix = _localization.Current.Format(L.CopySuffix);
        var name = new LocalizedText(
            source.Name.Values.Select(pair => KeyValuePair.Create(pair.Key, pair.Value + suffix))
        );
        _store.SealCoalescing();
        if (Dispatch(new DuplicateShortcut(editing.Id, name)))
        {
            _store.SealCoalescing();
            var list = Shortcuts;
            if (location.Index + 1 < list.Count)
            {
                ClearTransient();
                SetPane(new EditorPane.Editing(list[location.Index + 1].Id));
            }

            Notify(L.NewCreated, "content_copy", undo: true);
        }
    }

    /// <summary>[Eliminar], confirmed with two taps (EDI-019, REG-04): then the first of the list, or nothing.</summary>
    /// <param name="token">The second tap.</param>
    public void Delete(ConfirmationToken token)
    {
        if (Pane is not EditorPane.Editing editing)
        {
            return;
        }

        _store.SealCoalescing();
        if (DispatchConfirmed(new DeleteShortcut(editing.Id), token))
        {
            ClearTransient();
            SetPane(FirstOf(List));
            Notify(L.Deleted, "delete", undo: true);
        }
    }

    /// <summary>The repeated combinations of the document (REP-001 to REP-005).</summary>
    public DuplicateIndex Duplicates() =>
        DuplicateIndex.Build(_store.Current.Library, _store.Current.Duplicates);

    /// <summary>The chip «N combinaciones repetidas · Revisar» (REP-003): the first repetition.</summary>
    public void ReviewDuplicates()
    {
        if (Duplicates().FirstToReview is { } id)
        {
            Select(id);
        }
    }

    /// <summary>‹ or › of the repeated card (REP-004): the previous or next repeated combination, in a circle.</summary>
    /// <param name="step">-1 or 1.</param>
    public void ShowRepeated(int step)
    {
        var index = Duplicates();
        var combos = index.RepeatedCombinations;
        if (
            combos.IsEmpty
            || Selected is not { } shortcut
            || !DuplicateIndex.TryGetKey(shortcut, out var key)
        )
        {
            return;
        }

        var at = combos.IndexOf(key);
        var next = combos[
            (((at < 0 ? 0 : at) + step) % combos.Length + combos.Length) % combos.Length
        ];
        if (FirstAppearance(index, next) is { } target)
        {
            Select(target);
        }
    }

    /// <summary>
    /// 🗑 of a row of the repeated card, confirmed with two taps (REP-005): deletes that appearance; when it was the
    /// one in the editor, another one opens, preferably outside Always visible.
    /// </summary>
    /// <param name="id">The appearance.</param>
    /// <param name="token">The second tap.</param>
    public void DeleteAppearance(ShortcutId id, ConfirmationToken token)
    {
        var next = Duplicates().NextAfterDeleting(id);
        var current = Pane is EditorPane.Editing editing && editing.Id == id;
        _store.SealCoalescing();
        if (DispatchConfirmed(new DeleteDuplicate(id), token))
        {
            if (current)
            {
                ClearTransient();
                if (next is { } other && _store.Current.Library.TryLocate(other, out var location))
                {
                    List = location.List;
                    SetPane(new EditorPane.Editing(other));
                }
                else
                {
                    SetPane(FirstOf(List));
                }
            }

            Notify(L.Deleted, "delete", undo: true);
        }
    }

    /// <summary>
    /// «[moveAlways]» (REP-005), confirmed with two taps because it deletes shortcuts (REG-04): one undo step, [moved],
    /// and the one kept opens.
    /// </summary>
    /// <param name="token">The second tap.</param>
    public void KeepOnlyInAlwaysVisible(ConfirmationToken token)
    {
        if (Pane is not EditorPane.Editing editing)
        {
            return;
        }

        var kept = Domain.Commands.KeepOnlyInAlwaysVisible.KeptIn(
            _store.Current.Library,
            editing.Id
        );
        _store.SealCoalescing();
        if (
            DispatchConfirmed(new Domain.Commands.KeepOnlyInAlwaysVisible(editing.Id), token)
            && kept is { } id
        )
        {
            ClearTransient();
            List = new ListRef.AlwaysVisible();
            SetPane(new EditorPane.Editing(id));
            Notify(L.Moved, "done_all", undo: true);
        }
    }

    /// <summary>«[itsFine2]» (REP-005): the combination is no longer flagged, [dupKept], and the next one opens.</summary>
    public void AcceptRepeated()
    {
        if (Selected is not { } shortcut || !DuplicateIndex.TryGetKey(shortcut, out var key))
        {
            return;
        }

        var before = Duplicates().RepeatedCombinations;
        var at = before.IndexOf(key);
        _store.SealCoalescing();
        if (!Dispatch(new MarkDuplicateAccepted(key)))
        {
            return;
        }

        Notify(L.DupKept, "thumb_up", undo: true);
        var after = Duplicates();
        if (!after.RepeatedCombinations.IsEmpty && at >= 0)
        {
            var next = after.RepeatedCombinations[at % after.RepeatedCombinations.Length];
            if (FirstAppearance(after, next) is { } target)
            {
                Select(target);
            }
        }

        Raise();
    }

    /// <summary>The name of a list as the notices write it.</summary>
    /// <param name="list">The list.</param>
    public string ListName(ListRef list) =>
        list is ListRef.InProfile inProfile
        && _store.Current.Library.TryGetProfile(inProfile.Id, out var profile)
            ? profile.Name.Get(UiLanguage(), LangCode.Es)
            : _localization.Current.Format(L.Always);

    private static ListRef General => new ListRef.InProfile(ProfileId.General);

    private static LocalizedText SameInEveryLanguage(string text, LocalizedText current)
    {
        var languages = current.Values.Keys.Concat([LangCode.Es, LangCode.En]).Distinct();
        return new LocalizedText(languages.Select(language => KeyValuePair.Create(language, text)));
    }

    private ShortcutId? FirstAppearance(DuplicateIndex index, CanonicalChord key)
    {
        ShortcutId? fallback = null;
        foreach (var located in _store.Current.Library.EnumerateShortcuts())
        {
            if (
                !index.IsRepeated(located.Shortcut.Id)
                || !DuplicateIndex.TryGetKey(located.Shortcut, out var other)
                || other != key
            )
            {
                continue;
            }

            if (located.Location.List is not ListRef.AlwaysVisible)
            {
                return located.Shortcut.Id;
            }

            fallback ??= located.Shortcut.Id;
        }

        return fallback;
    }

    private string Display(LocalizedText name) => name.Get(UiLanguage(), LangCode.Es);

    private LangCode UiLanguage() => new(_localization.Current.Locale.Code);

    private EditorPane FirstOf(ListRef list) =>
        _store.Current.Library.TryGetList(list, out var shortcuts) && !shortcuts.IsEmpty
            ? new EditorPane.Editing(shortcuts[0].Id)
            : new EditorPane.Empty();

    /// <summary>
    /// The editor leaves what it shows: a blank draft is discarded without a trace (ATJ-011) and the undo entry of
    /// the shortcut is sealed (EDI-021).
    /// </summary>
    private void Leave()
    {
        if (
            Pane is EditorPane.Editing editing
            && _store.Current.Library.TryGetShortcut(editing.Id, out var shortcut)
            && ShortcutCompleteness.IsBlankDraft(shortcut)
        )
        {
            _ = _store.Dispatch(new DiscardDraft(editing.Id));
        }

        _store.SealCoalescing();
        ClearTransient();
    }

    private void ClearTransient()
    {
        _remembered.Clear();
        ReplacedChord = null;
        EditingStep = null;
    }

    private void SetPane(EditorPane pane)
    {
        Pane = pane;
        Raise();
    }

    private void Edit(Func<Shortcut, Shortcut> change)
    {
        switch (Pane)
        {
            case EditorPane.Draft draft:
            {
                var next = change(draft.Shortcut);
                if (next.Equals(draft.Shortcut))
                {
                    return;
                }

                if (ShortcutCompleteness.IsBlankDraft(next))
                {
                    SetPane(new EditorPane.Draft(next));
                    return;
                }

                if (Dispatch(new CreateShortcut(List, next, ListPosition.End)))
                {
                    var created = Shortcuts;
                    SetPane(
                        created.IsEmpty
                            ? new EditorPane.Empty()
                            : new EditorPane.Editing(created[^1].Id)
                    );
                }

                return;
            }

            case EditorPane.Editing editing
                when _store.Current.Library.TryGetShortcut(editing.Id, out var current):
            {
                var next = change(current);
                if (!next.Equals(current) && Dispatch(new EditShortcut(next)))
                {
                    // EDI-021: the edits of this shortcut are one step, «Deshacer cambios en {nombre}».
                    Noticed?.Invoke(
                        this,
                        new WorkspaceNoticeEventArgs(
                            new WorkspaceNotice(
                                L.Saved,
                                "check",
                                CanUndo: true,
                                IsWarning: false,
                                L.UndoEditsIn(name: Display(next.Name))
                            )
                        )
                    );
                }

                return;
            }
        }
    }

    private void EditChord(Func<KeyChord, KeyChord> change)
    {
        if (EditingStep is { } step && Selected?.Action is MacroAction)
        {
            EditMacro(macro => MacroSteps.EditKeys(macro, step, change));
            return;
        }

        Edit(shortcut =>
            ActionKinds.ChordOf(shortcut.Action) is { } chord
                ? shortcut with
                {
                    Action = ActionKinds.WithChord(shortcut.Action, change(chord)),
                }
                : shortcut
        );
    }

    private void EditMacro(Func<MacroAction, MacroAction> change) =>
        Edit(shortcut =>
            shortcut.Action is MacroAction macro
                ? shortcut with
                {
                    Action = change(macro),
                }
                : shortcut
        );

    private bool Dispatch(IDocumentCommand command)
    {
        var result = _store.Dispatch(command);
        if (result.IsFailure)
        {
            Warn(result.Failure.Message);
        }

        return result.IsSuccess;
    }

    private bool DispatchConfirmed(IDestructiveCommand command, ConfirmationToken token)
    {
        ArgumentNullException.ThrowIfNull(token);
        var result = _store.Dispatch(command, token);
        if (result.IsFailure)
        {
            Warn(result.Failure.Message);
        }

        return result.IsSuccess;
    }

    private void Notify(Message text, string icon, bool undo) =>
        Noticed?.Invoke(
            this,
            new WorkspaceNoticeEventArgs(new WorkspaceNotice(text, icon, undo, IsWarning: false))
        );

    private void Warn(Message text) =>
        Noticed?.Invoke(
            this,
            new WorkspaceNoticeEventArgs(
                new WorkspaceNotice(text, "warning", CanUndo: false, IsWarning: true)
            )
        );

    private void Raise() => Changed?.Invoke(this, EventArgs.Empty);
}
