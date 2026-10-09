using System.Collections.Immutable;
using System.Globalization;
using Clicalo.Application.Confirmation;
using Clicalo.Application.Ports;
using Clicalo.Application.UseCases.Editor;
using Clicalo.Domain.Commands;
using Clicalo.Domain.Duplicates;
using Clicalo.Domain.Keys;
using Clicalo.Domain.Library;
using Clicalo.Domain.Messages;
using Clicalo.Domain.Primitives;
using Clicalo.Domain.Privacy;
using Clicalo.Domain.Timing;
using Clicalo.Presentation.ControlCenter.Shortcuts;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Clicalo.Presentation.ControlCenter.Editor;

/// <summary>
/// State B of the editor column of «Atajos» (docs/05 §1, EDI-001 to EDI-021, PRB-001 to PRB-007): it projects the
/// shortcut of <see cref="ShortcutsWorkspace"/> into <see cref="Model"/> and forwards every intention to it. Its own
/// state is what only the view needs: what is unfolded, the icon search, the key group, the «Ver» animation and the
/// «Probar ahora» run. No product rule lives here: they are the Application's and the Domain's.
/// </summary>
/// <remarks>
/// It is the only view model that reveals the text of a Text shortcut, to put it in its field (blueprint §6.2, rule of
/// <c>SecretText</c>); the text never goes into a model.
/// </remarks>
public sealed class ShortcutEditorViewModel : ObservableObject, IDisposable
{
    private const string DraftKey = "draft";

    private static readonly ImmutableArray<KeyId> ModifierKeys =
    [
        KeyIds.Ctrl,
        KeyIds.Alt,
        KeyIds.Shift,
        KeyIds.Win,
    ];

    private static readonly ImmutableArray<KeyGroup> PickerGroups =
    [
        KeyGroup.Sides,
        KeyGroup.Letters,
        KeyGroup.Nums,
        KeyGroup.Fn,
        KeyGroup.Special,
        KeyGroup.Numpad,
        KeyGroup.Media,
    ];

    private readonly ControlCenterServices _s;
    private readonly Action _invalidate;
    private readonly List<ITimer> _playback = [];
    private EditorModel? _model;
    private string _shortcutKey = string.Empty;
    private string? _for;
    private bool _pickerOpen;
    private string _iconQuery = string.Empty;
    private KeyGroup _group = KeyGroup.Letters;
    private bool _moreOpen;
    private bool _voiceHowOpen;
    private bool _duplicatesOpen;
    private bool _testOpen;
    private PlaybackFrame? _frame;
    private ImmutableArray<OpenApp> _apps = [];
    private ProcessName? _target;
    private TestAnswer _answer;
    private bool _running;
    private CancellationTokenSource? _try;
    private ITimer? _armTimer;
    private int _textVersion;

    /// <summary>Creates the editor.</summary>
    /// <param name="services">The services of the Control Center.</param>
    /// <param name="invalidate">Asks the section to project again once the current work is done.</param>
    public ShortcutEditorViewModel(ControlCenterServices services, Action invalidate)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(invalidate);
        _s = services;
        _invalidate = invalidate;
    }

    private enum TestAnswer
    {
        None,
        Asking,
        Yes,
        No,
    }

    /// <summary>The editor of the shortcut in view, or null when the column shows something else.</summary>
    public EditorModel? Model
    {
        get => _model;
        private set => SetProperty(ref _model, value);
    }

    /// <summary>Which shortcut <see cref="Model"/> shows (its id, or «draft»); the view focuses the name of a new one.</summary>
    public string ShortcutKey
    {
        get => _shortcutKey;
        private set => SetProperty(ref _shortcutKey, value);
    }

    private ShortcutsWorkspace Workspace => _s.Shortcuts;

    private LangCode Language => new(_s.Localization.Current.Locale.Code);

    /// <summary>Projects the editor again (the section calls it once per change).</summary>
    public void Project()
    {
        var shortcut = Workspace.Selected;
        var key = Workspace.Pane switch
        {
            EditorPane.Editing editing => editing.Id.Value,
            EditorPane.Draft => DraftKey,
            _ => null,
        };
        if (!string.Equals(key, _for, StringComparison.Ordinal))
        {
            OnShortcutChanged(key);
        }

        ShortcutKey = key ?? string.Empty;
        Model = shortcut is null ? null : Build(shortcut);
    }

    /// <summary>Stops the animation, the armed timer and a running try.</summary>
    public void Dispose()
    {
        StopPlayback();
        _armTimer?.Dispose();
        _try?.Cancel();
        _try?.Dispose();
    }

    /// <summary>The text of the Text shortcut, for its field (EDI-011).</summary>
    public string RevealText() =>
        Workspace.Selected?.Action is TextAction text ? Reveal(text.Text) : string.Empty;

    /// <summary>The text of a text step, for its field (EDI-013).</summary>
    /// <param name="index">The zero-based step.</param>
    public string RevealStepText(int index) =>
        Workspace.Selected?.Action is MacroAction macro
        && index >= 0
        && index < macro.Steps.Count
        && macro.Steps[index] is TextStep step
            ? Reveal(step.Text)
            : string.Empty;

    /// <summary>Esc with a menu open (CCM-001): closes the innermost one and says whether there was one.</summary>
    public bool CloseMenu()
    {
        if (_pickerOpen)
        {
            _pickerOpen = false;
        }
        else if (_testOpen)
        {
            CloseTest();
            return true;
        }
        else if (_duplicatesOpen)
        {
            _duplicatesOpen = false;
        }
        else if (Workspace.EditingStep is not null)
        {
            Workspace.StopEditingStep();
            return true;
        }
        else
        {
            return false;
        }

        _invalidate();
        return true;
    }

    /// <summary>The preview tile: opens or closes the icon picker (EDI-001).</summary>
    public void TogglePicker() => Flip(ref _pickerOpen);

    /// <summary>The icon search (EDI-004).</summary>
    /// <param name="query">What the person typed.</param>
    public void SearchIcons(string query)
    {
        _iconQuery = query ?? string.Empty;
        _invalidate();
    }

    /// <summary>An icon of the picker or a suggestion (EDI-003, EDI-004).</summary>
    /// <param name="icon">The icon.</param>
    public void PickIcon(string icon) => Workspace.SetIcon(new(icon));

    /// <summary>The name field (EDI-001, EDI-002).</summary>
    /// <param name="text">The name.</param>
    public void Rename(string text) => Workspace.Rename(text);

    /// <summary>🎤 next to a free text field (ACC-011): Windows dictation for the focused field.</summary>
    public void Dictate() => _ = DictateAsync();

    /// <summary>A kind of the «Qué hace» grid (EDI-006).</summary>
    /// <param name="kind">The kind.</param>
    public void SetKind(ActionKind kind) => Workspace.SetKind(kind);

    /// <summary>A key of the picker or a modifier (EDI-008).</summary>
    /// <param name="key">The key.</param>
    public void TapKey(KeyId key) => Workspace.TapKey(key);

    /// <summary>× of a key chip (EDI-007).</summary>
    /// <param name="index">The zero-based key.</param>
    public void RemoveKey(int index) => Workspace.RemoveKey(index);

    /// <summary>⌫ (EDI-007).</summary>
    public void RemoveLastKey() => Workspace.RemoveLastKey();

    /// <summary>↺ Limpiar (EDI-007).</summary>
    public void ClearKeys() => Workspace.ClearKeys();

    /// <summary>A group of the key picker (EDI-008).</summary>
    /// <param name="group">The group.</param>
    public void ChooseGroup(KeyGroup group)
    {
        _group = group;
        _invalidate();
    }

    /// <summary>[keepOld] (REP-006).</summary>
    public void KeepOldCombination() => Workspace.KeepOldCombination();

    /// <summary>[done] of the step being edited (EDI-007).</summary>
    public void StopEditingStep() => Workspace.StopEditingStep();

    /// <summary>The text of a Text shortcut (EDI-011).</summary>
    /// <param name="text">The text.</param>
    public void SetText(string text)
    {
        Workspace.SetText(text);
        _textVersion++;
    }

    /// <summary>A mouse action (EDI-012).</summary>
    /// <param name="op">The action.</param>
    public void SetMouse(MouseOp op) => Workspace.SetMouse(op);

    /// <summary>A scroll speed (EDI-012).</summary>
    /// <param name="speed">The speed.</param>
    public void SetSpeed(ScrollSpeed speed) => Workspace.SetSpeed(speed);

    /// <summary>The Web or App field (EDI-014).</summary>
    /// <param name="text">The text.</param>
    public void SetTarget(string text)
    {
        if (Workspace.Selected?.Action is UrlAction)
        {
            Workspace.SetUrl(text);
        }
        else
        {
            Workspace.SetApp(text);
        }
    }

    /// <summary>A chip of «Elegir programa» (EDI-014): its executable.</summary>
    /// <param name="process">The process of the open app.</param>
    public void PickProgram(string process)
    {
        var app = _apps.FirstOrDefault(a =>
            string.Equals(a.Process.Value, process, StringComparison.OrdinalIgnoreCase)
        );
        Workspace.SetApp(app?.ExecutablePath ?? process);
    }

    /// <summary>A + button under the steps (EDI-013).</summary>
    /// <param name="kind">The kind of step.</param>
    public void AddStep(MacroStepKind kind)
    {
        if (kind == MacroStepKind.Keys)
        {
            _group = KeyGroup.Letters;
        }

        Workspace.AddStep(kind);
    }

    /// <summary>✏ of a step (EDI-013).</summary>
    /// <param name="index">The zero-based step.</param>
    public void ToggleStep(int index) => Workspace.ToggleStep(index);

    /// <summary>↑ or ↓ of a step (EDI-013).</summary>
    /// <param name="index">The zero-based step.</param>
    /// <param name="offset">-1 or 1.</param>
    public void MoveStep(int index, int offset) => Workspace.MoveStep(index, offset);

    /// <summary>− or + of a wait (EDI-013).</summary>
    /// <param name="index">The zero-based step.</param>
    /// <param name="steps">-1 or 1.</param>
    public void NudgeWait(int index, int steps) => Workspace.NudgeWait(index, steps);

    /// <summary>The text of a text step (EDI-013).</summary>
    /// <param name="index">The zero-based step.</param>
    /// <param name="text">The text.</param>
    public void SetStepText(int index, string text) => Workspace.SetStepText(index, text);

    /// <summary>The action of a mouse step (EDI-013).</summary>
    /// <param name="index">The zero-based step.</param>
    /// <param name="op">The action.</param>
    public void SetStepMouse(int index, MouseOp op) => Workspace.SetStepMouse(index, op);

    /// <summary>✕ of a step: two taps (EDI-013, REG-04).</summary>
    /// <param name="index">The zero-based step.</param>
    public void DeleteStep(int index)
    {
        if (Workspace.Pane is EditorPane.Editing editing)
        {
            Confirm(
                Subject(nameof(DeleteMacroStep), editing.Id.Value + ":" + Number(index)),
                token => Workspace.DeleteStep(index, token)
            );
        }
    }

    /// <summary>«Fijar en Siempre visible» (EDI-015).</summary>
    public void TogglePin() => Workspace.SetPinned(Workspace.List is not ListRef.AlwaysVisible);

    /// <summary>«Más opciones» (EDI-016).</summary>
    public void ToggleMore() => Flip(ref _moreOpen);

    /// <summary>A position button (EDI-016).</summary>
    /// <param name="move">The button.</param>
    public void Move(PositionMove move) => Workspace.Move(move);

    /// <summary>
    /// [autoRelease] (EDI-016): 0 «Como en General», then each of <c>Timings.KeySafety.AutoReleaseChoices</c> (30 s,
    /// 1 min, 2 min), then [never].
    /// </summary>
    /// <param name="option">The option.</param>
    public void SetHold(int option)
    {
        var choices = Timings.KeySafety.AutoReleaseChoices;
        Workspace.SetHoldLimit(
            option switch
            {
                > 0 when option <= choices.Length => new HoldLimit.After(choices[option - 1]),
                > 0 when option == choices.Length + 1 => new HoldLimit.Never(),
                _ => new HoldLimit.InheritGlobal(),
            }
        );
    }

    /// <summary>[textMethod] (EDI-016): 0 [tmType], 1 [tmPaste].</summary>
    /// <param name="option">The option.</param>
    public void SetMethod(int option) =>
        Workspace.SetTextMethod(option == 1 ? TextMethod.Paste : TextMethod.Unicode);

    /// <summary>«Pedir confirmación antes de ejecutar» (EDI-017).</summary>
    public void ToggleConfirm() =>
        Workspace.SetConfirm(!(Workspace.Selected?.Options.Confirm ?? false));

    /// <summary>«Fijado en Frecuentes» (EDI-018).</summary>
    public void ToggleFrequents() =>
        Workspace.SetPinnedInFrequents(
            Workspace.Selected is { } shortcut
                && !_s.Store.Current.Frequents.Pins.Contains(shortcut.Id)
        );

    /// <summary>«Cómo usar los números por voz» (EDI-016).</summary>
    public void ToggleVoiceHow() => Flip(ref _voiceHowOpen);

    /// <summary>[Probar] of the footer: opens or closes the card (EDI-019, PRB-005).</summary>
    public void ToggleTest()
    {
        if (_testOpen)
        {
            CloseTest();
            return;
        }

        _testOpen = true;
        _answer = TestAnswer.None;
        _frame = null;
        _ = LoadAppsAsync();
        _invalidate();
    }

    /// <summary>✕ of the card (PRB-005): the animation, the timers and the question stop.</summary>
    public void CloseTest()
    {
        _testOpen = false;
        _answer = TestAnswer.None;
        StopPlayback();
        _try?.Cancel();
        _invalidate();
    }

    /// <summary>[playSeq] «Ver» (PRB-002): plays the animation of the kind from the start.</summary>
    public void Play()
    {
        if (Workspace.Selected is not { } shortcut)
        {
            return;
        }

        StopPlayback();
        var script = TryPlayback.Script(shortcut.Action, _s.Store.Current.Settings.ReduceMotion);
        foreach (var frame in script)
        {
            _playback.Add(
                _s.Time.CreateTimer(
                    _ =>
                        _s.Post(() =>
                        {
                            _frame = frame;
                            _invalidate();
                        }),
                    null,
                    frame.At,
                    Timeout.InfiniteTimeSpan
                )
            );
        }
    }

    /// <summary>A chip of «Probar en» (PRB-003): the choice stays between shortcuts.</summary>
    /// <param name="process">The process of the app.</param>
    public void ChooseTarget(string process)
    {
        _target = new ProcessName(process);
        _invalidate();
    }

    /// <summary>«Probar ahora en {app}» (PRB-004).</summary>
    public void TryLive() => _ = TryLiveAsync();

    /// <summary>The answer to «¿Hizo lo esperado?» (PRB-004).</summary>
    /// <param name="yes">Whether it worked.</param>
    public void Answer(bool yes)
    {
        _answer = yes ? TestAnswer.Yes : TestAnswer.No;
        _invalidate();
    }

    /// <summary>[Duplicar] (EDI-019).</summary>
    public void Duplicate() => Workspace.Duplicate();

    /// <summary>[Eliminar]: two taps (EDI-019, REG-04).</summary>
    public void Delete()
    {
        if (Workspace.Pane is EditorPane.Editing editing)
        {
            Confirm(Subject(nameof(DeleteShortcut), editing.Id.Value), Workspace.Delete);
        }
    }

    /// <summary>▾ of the repeated card (REP-004).</summary>
    public void ToggleDuplicates() => Flip(ref _duplicatesOpen);

    /// <summary>‹ or › of the repeated card (REP-004).</summary>
    /// <param name="step">-1 or 1.</param>
    public void ShowRepeated(int step) => Workspace.ShowRepeated(step);

    /// <summary>A row of the repeated card (REP-005): that appearance opens.</summary>
    /// <param name="id">The appearance.</param>
    public void OpenAppearance(ShortcutId id) => Workspace.Select(id);

    /// <summary>🗑 of a row of the repeated card: two taps (REP-005, REG-04).</summary>
    /// <param name="id">The appearance.</param>
    public void DeleteAppearance(ShortcutId id) =>
        Confirm(
            Subject(nameof(DeleteDuplicate), id.Value),
            token => Workspace.DeleteAppearance(id, token)
        );

    /// <summary>«[moveAlways]»: two taps, because it deletes shortcuts (REP-005, REG-04).</summary>
    public void KeepOnlyInAlwaysVisible()
    {
        if (Workspace.Pane is EditorPane.Editing editing)
        {
            Confirm(
                Subject(nameof(Domain.Commands.KeepOnlyInAlwaysVisible), editing.Id.Value),
                Workspace.KeepOnlyInAlwaysVisible
            );
        }
    }

    /// <summary>«[itsFine2]» (REP-005).</summary>
    public void AcceptRepeated() => Workspace.AcceptRepeated();

    /// <summary>«[useOther]» (REP-006).</summary>
    public void UseOtherCombination()
    {
        _duplicatesOpen = false;
        _group = KeyGroup.Letters;
        Workspace.UseOtherCombination();
    }

    /// <summary>The open apps were read again: the chips of «Probar en» and «Elegir programa» follow.</summary>
    /// <param name="apps">The open apps.</param>
    public void ApplyApps(ImmutableArray<OpenApp> apps)
    {
        _apps = apps;
        _invalidate();
    }

    private static string Reveal(SecretText text)
    {
        var result = string.Empty;
        text.WithRevealed(0, (span, _) => result = new string(span));
        return result;
    }

    private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);

    private static ConfirmationSubject Subject(string operation, string target) =>
        new(operation, target);

    private static string KindIcon(ActionKind kind) =>
        kind switch
        {
            ActionKind.Tap => "touch_app",
            ActionKind.Hold => "pan_tool",
            ActionKind.Toggle => "toggle_on",
            ActionKind.Text => "text_fields",
            ActionKind.Mouse => "mouse",
            ActionKind.Macro => "playlist_play",
            ActionKind.Url => "public",
            ActionKind.App => "open_in_new",
            _ => "settings",
        };

    private static Message KindLabel(ActionKind kind) =>
        kind switch
        {
            ActionKind.Tap => L.TTap,
            ActionKind.Hold => L.THold,
            ActionKind.Toggle => L.TToggle,
            ActionKind.Text => L.TText,
            ActionKind.Mouse => L.TMouse,
            ActionKind.Macro => L.TMacro,
            ActionKind.Url => L.TUrl,
            _ => L.TApp,
        };

    private static Message? KindDescription(ActionKind kind) =>
        kind switch
        {
            ActionKind.Tap => L.DTap,
            ActionKind.Hold => L.DHold,
            ActionKind.Toggle => L.DToggle,
            ActionKind.Text => L.DText,
            ActionKind.Mouse => L.DMouse,
            ActionKind.Macro => L.DMacro,
            ActionKind.Url => L.DUrl,
            ActionKind.App => L.DApp,
            _ => null,
        };

    private static Message GroupLabel(KeyGroup group) =>
        group switch
        {
            KeyGroup.Sides => L.KgSides,
            KeyGroup.Letters => L.KgLetters,
            KeyGroup.Nums => L.KgNums,
            KeyGroup.Fn => L.KgFn,
            KeyGroup.Special => L.KgSpecial,
            KeyGroup.Numpad => L.KgNumpad,
            KeyGroup.Media => L.KgMedia,
            _ => L.KgMods,
        };

    private static string StepIcon(MacroStep step) =>
        step switch
        {
            KeysStep => "keyboard",
            WaitStep => "timer",
            TextStep => "text_fields",
            _ => "mouse",
        };

    private string T(Message message) => _s.Localization.Current.Format(message);

    private void Flip(ref bool flag)
    {
        flag = !flag;
        _invalidate();
    }

    private void OnShortcutChanged(string? key)
    {
        _for = key;
        _pickerOpen = false;
        _iconQuery = string.Empty;
        _duplicatesOpen = false;
        _voiceHowOpen = false;
        _testOpen = false;
        _answer = TestAnswer.None;
        _frame = null;
        StopPlayback();
        _try?.Cancel();
        _s.Confirm.Disarm();
        _textVersion++;
    }

    private void StopPlayback()
    {
        foreach (var timer in _playback)
        {
            timer.Dispose();
        }

        _playback.Clear();
        _frame = null;
    }

    private void Confirm(ConfirmationSubject subject, Action<ConfirmationToken> run)
    {
        switch (_s.Confirm.Tap(subject))
        {
            case TwoStepResult.Confirmed confirmed:
                run(confirmed.Token);
                break;
            case TwoStepResult.Armed armed:
                _armTimer?.Dispose();
                _armTimer = _s.Time.CreateTimer(
                    _ => _s.Post(_invalidate),
                    null,
                    armed.Until - _s.Time.GetUtcNow(),
                    Timeout.InfiniteTimeSpan
                );
                break;
        }

        _invalidate();
    }

    private bool IsArmed(string operation, string target) =>
        _s.Confirm.ArmedSubject is { } armed
        && string.Equals(armed.Operation, operation, StringComparison.Ordinal)
        && string.Equals(armed.Target, target, StringComparison.Ordinal);

    private async Task DictateAsync()
    {
        try
        {
            _ = await _s.Dictate(CancellationToken.None).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            // Dictation is best effort.
        }
    }

    private async Task LoadAppsAsync()
    {
        var apps = await _s.OpenApps(CancellationToken.None).ConfigureAwait(true);
        _s.Post(() => ApplyApps(apps));
    }

    private async Task TryLiveAsync()
    {
        if (
            _running
            || Workspace.Selected is not { } shortcut
            || Workspace.Pane is not EditorPane.Editing
            || ChosenTarget() is not { } target
        )
        {
            return;
        }

        _running = true;
        _answer = TestAnswer.None;
        _try?.Dispose();
        _try = new CancellationTokenSource();
        _invalidate();
        try
        {
            var outcome = await _s.TryNow(shortcut, target, _try.Token).ConfigureAwait(true);
            _answer = outcome is TryNowOutcome.Asked or TryNowOutcome.AskedFlashed
                ? TestAnswer.Asking
                : TestAnswer.None;
        }
        catch (OperationCanceledException)
        {
            _answer = TestAnswer.None;
        }
        finally
        {
            _running = false;
            _invalidate();
        }
    }

    private OpenApp? ChosenTarget()
    {
        if (_apps.IsEmpty)
        {
            return null;
        }

        var wanted = _target ?? _s.LastApp();
        return _apps.FirstOrDefault(a => wanted is { } w && a.Process == w) ?? _apps[0];
    }

    private EditorModel Build(Shortcut shortcut)
    {
        var library = _s.Store.Current.Library;
        var saved = Workspace.Pane is EditorPane.Editing;
        var index = saved && library.TryLocate(shortcut.Id, out var location) ? location.Index : -1;
        return new EditorModel(
            saved ? DuplicateCard(shortcut) : null,
            Identity(shortcut),
            _pickerOpen ? Picker(shortcut) : null,
            Kinds(shortcut),
            Combo(shortcut),
            shortcut.Action is TextAction
                ? new TextFieldModel(
                    T(L.TextLabel),
                    T(L.TextHint),
                    T(L.SearchDictate),
                    T(L.TextEnc),
                    _textVersion
                )
                : null,
            Mouse(shortcut),
            Steps(shortcut),
            Target(shortcut),
            Pin(),
            More(shortcut, index),
            _testOpen ? Test(shortcut) : null,
            Footer(shortcut, saved)
        );
    }

    private DuplicateCardModel? DuplicateCard(Shortcut shortcut)
    {
        var index = Workspace.Duplicates();
        var appearances = index.AppearancesOf(shortcut.Id);
        if (appearances.IsEmpty || !DuplicateIndex.TryGetKey(shortcut, out var key))
        {
            return null;
        }

        var advice = index.AdviceFor(shortcut.Id);
        var moveArmed = IsArmed(nameof(Domain.Commands.KeepOnlyInAlwaysVisible), shortcut.Id.Value);
        return new DuplicateCardModel(
            T(
                L.DupHead(
                    appearances.Length,
                    index.RepeatedCombinations.IndexOf(key) + 1,
                    index.RepeatedCombinationCount
                )
            ),
            T(L.PrevDup),
            T(L.NextDup),
            _duplicatesOpen,
            [
                .. appearances.Select(a =>
                {
                    var where = Workspace.ListName(a.Location.List);
                    return new DuplicateRow(
                        a.Shortcut.Id,
                        a.Shortcut.Icon.Name,
                        a.Shortcut.Category.Value,
                        a.Shortcut.Name.Get(Language, LangCode.Es),
                        where,
                        a.Shortcut.Id == shortcut.Id,
                        T(L.DelFrom(profile: where)),
                        IsArmed(nameof(DeleteDuplicate), a.Shortcut.Id.Value)
                    );
                }),
            ],
            T(L.EditingNow),
            T(L.DelConfirm),
            T(
                advice switch
                {
                    DuplicateAdvice.AlwaysVisible => L.DupAdvG,
                    DuplicateAdvice.SameName => L.DupAdvSame,
                    _ => L.DupAdvDiff,
                }
            ),
            advice is DuplicateAdvice.AlwaysVisible or DuplicateAdvice.SameName
                ? T(moveArmed ? L.DelConfirm : L.MoveAlways)
                : null,
            moveArmed,
            T(L.ItsFine2),
            T(L.UseOther)
        );
    }

    private IdentityModel Identity(Shortcut shortcut)
    {
        var name = shortcut.Name.Get(Language, LangCode.Es);
        return new IdentityModel(
            shortcut.Icon.Name,
            shortcut.Category.Value,
            name.Length == 0 ? T(L.NamePh2) : name,
            T(L.ChangeIcon),
            T(L.Name),
            name,
            T(L.NamePh),
            T(L.DictName),
            shortcut.AutoIcon ? "auto_awesome" : "edit",
            T(shortcut.AutoIcon ? L.IconAuto : L.IconManual),
            _pickerOpen
        );
    }

    private IconPickerModel Picker(Shortcut shortcut)
    {
        var current = shortcut.Icon.Name;
        return new IconPickerModel(
            [
                .. Workspace
                    .SuggestedIcons()
                    .Select(icon => new IconOption(
                        icon.Name,
                        string.Equals(icon.Name, current, StringComparison.Ordinal)
                    )),
            ],
            T(L.IconSearch),
            [
                .. _s.Catalogs()
                    .Icons.Search(_iconQuery)
                    .Select(icon => new IconOption(
                        icon.Name,
                        string.Equals(icon.Name, current, StringComparison.Ordinal)
                    )),
            ]
        );
    }

    private KindsModel Kinds(Shortcut shortcut) =>
        new(
            T(L.Type),
            [
                .. ActionKinds.Grid.Select(kind => new KindOption(
                    kind,
                    KindIcon(kind),
                    T(KindLabel(kind)),
                    shortcut.Action.Kind == kind
                )),
            ],
            KindDescription(shortcut.Action.Kind) is { } description ? T(description) : string.Empty
        );

    private ComboModel? Combo(Shortcut shortcut)
    {
        var editingStep =
            shortcut.Action is MacroAction macro && Workspace.EditingStep is { } step
                ? MacroSteps.KeysOf(macro, step) is null
                    ? (int?)null
                    : step
                : null;
        if (!ActionKinds.HasKeys(shortcut.Action.Kind) && editingStep is null)
        {
            return null;
        }

        var chord = Workspace.ChordInBox() ?? KeyChord.Empty;
        var labels = _s.Catalogs().KeyLabels;
        var warning = ComboWarnings.Of(chord);
        return new ComboModel(
            T(L.Keys),
            Workspace.ReplacedChord is { } replaced
                ? T(L.ReplaceMsg(keys: Format(replaced, KeyLabelStyle.Full)))
                : null,
            T(L.KeepOld),
            chord.IsEmpty ? T(L.ComboEmpty) : null,
            [
                .. chord.Strokes.Select(
                    (stroke, i) =>
                    {
                        var label = KeyChordFormatter.KeyText(
                            stroke,
                            labels,
                            KeyLabelStyle.Full,
                            Language,
                            LangCode.Es
                        );
                        return new KeyChip(i, label, T(L.RemoveKeyN(key: label)));
                    }
                ),
            ],
            chord.IsEmpty ? T(L.ComboNone) : T(L.ComboN(chord.Strokes.Count)),
            !chord.IsEmpty,
            T(L.BackKey),
            T(L.ClearKeys),
            warning switch
            {
                ComboWarning.Blocked => WarningTone.Danger,
                ComboWarning.Special => WarningTone.Warn,
                _ => WarningTone.None,
            },
            warning switch
            {
                ComboWarning.Blocked => T(L.BlockedB),
                ComboWarning.Special => T(L.BlockedS),
                _ => null,
            },
            editingStep is { } open ? T(L.StepEditMsg(index: open + 1)) : null,
            T(L.Done),
            [.. ModifierKeys.Select(key => Cell(key, chord))],
            [
                .. PickerGroups.Select(group => new KeyGroupTab(
                    group,
                    T(GroupLabel(group)),
                    group == _group
                )),
            ],
            [
                .. KeyDefinitions
                    .All.Where(definition => definition.Group == _group)
                    .Select(definition => Cell(definition.Id, chord)),
            ],
            _group switch
            {
                KeyGroup.Letters or KeyGroup.Nums => 7,
                KeyGroup.Fn => 6,
                _ => 0,
            },
            T(L.OrderHint2)
        );
    }

    private KeyCell Cell(KeyId key, KeyChord chord)
    {
        var labels = _s.Catalogs().KeyLabels;
        var stroke = new KeyStroke(key);
        return new KeyCell(
            key,
            KeyChordFormatter.KeyText(stroke, labels, KeyLabelStyle.Full, Language, LangCode.Es),
            KeyChordFormatter.KeyText(stroke, labels, KeyLabelStyle.Spoken, Language, LangCode.Es),
            ChordEdits.IsChosen(chord, key)
        );
    }

    private string Format(KeyChord chord, KeyLabelStyle style) =>
        KeyChordFormatter.Format(chord, _s.Catalogs().KeyLabels, style, Language, LangCode.Es);

    private ValueList<MouseOption> MouseOptions(MouseOp selected) =>
        [
            .. _s.Catalogs()
                .MouseActions.Select(info => new MouseOption(
                    info.Op,
                    info.Icon.Name,
                    info.Label.Get(Language, LangCode.Es),
                    info.Spoken.Get(Language, LangCode.Es),
                    info.Op == selected
                )),
        ];

    private string MouseName(MouseOp op) =>
        _s.Catalogs()
            .MouseActions.Items.FirstOrDefault(m => m.Op == op)
            ?.Label.Get(Language, LangCode.Es)
        ?? string.Empty;

    private MouseModel? Mouse(Shortcut shortcut)
    {
        if (shortcut.Action is not MouseAction mouse)
        {
            return null;
        }

        var scrolls =
            mouse.Op
            is MouseOp.ScrollUp
                or MouseOp.ScrollDown
                or MouseOp.ScrollLeft
                or MouseOp.ScrollRight;
        return new MouseModel(
            T(L.MouseLabel),
            MouseOptions(mouse.Op),
            T(L.Speed),
            scrolls
                ?
                [
                    new SpeedOption(ScrollSpeed.Slow, T(L.SpSlow), mouse.Speed == ScrollSpeed.Slow),
                    new SpeedOption(
                        ScrollSpeed.Normal,
                        T(L.SpNormal),
                        mouse.Speed == ScrollSpeed.Normal
                    ),
                    new SpeedOption(ScrollSpeed.Fast, T(L.SpFast), mouse.Speed == ScrollSpeed.Fast),
                ]
                : []
        );
    }

    private StepsModel? Steps(Shortcut shortcut)
    {
        if (shortcut.Action is not MacroAction macro)
        {
            return null;
        }

        var id = Workspace.Pane is EditorPane.Editing editing ? editing.Id.Value : DraftKey;
        var count = macro.Steps.Count;
        return new StepsModel(
            T(L.Steps),
            [
                .. macro.Steps.Select(
                    (step, i) =>
                        new StepRow(
                            i,
                            Number(i + 1),
                            StepIcon(step),
                            StepText(step),
                            Workspace.EditingStep == i
                                ? step switch
                                {
                                    KeysStep => StepEditorKind.Keys,
                                    WaitStep => StepEditorKind.Wait,
                                    TextStep => StepEditorKind.Text,
                                    _ => StepEditorKind.Mouse,
                                }
                                : StepEditorKind.None,
                            i > 0,
                            i < count - 1,
                            IsArmed(nameof(DeleteMacroStep), id + ":" + Number(i)),
                            step is WaitStep wait ? Seconds(wait.Duration) : string.Empty,
                            step is MouseStep mouse && Workspace.EditingStep == i
                                ? MouseOptions(mouse.Op)
                                : [],
                            _textVersion
                        )
                ),
            ],
            [
                new StepAdd(MacroStepKind.Keys, "keyboard", T(L.StKeys)),
                new StepAdd(MacroStepKind.Wait, "timer", T(L.StWait)),
                new StepAdd(MacroStepKind.Text, "text_fields", T(L.StText)),
                new StepAdd(MacroStepKind.Mouse, "mouse", T(L.StMouse)),
            ],
            T(L.Edit),
            T(L.Before),
            T(L.After),
            T(L.Delete),
            T(L.DelConfirm),
            T(L.WaitShorter),
            T(L.WaitLonger),
            T(L.SearchDictate)
        );
    }

    private string Seconds(TimeSpan duration) =>
        T(L.StepWait(seconds: (decimal)duration.TotalMilliseconds / 1000m));

    private string StepText(MacroStep step) =>
        step switch
        {
            KeysStep keys => keys.Chord.IsEmpty
                ? T(L.ComboNone)
                : T(L.StepPress(keys: Format(keys.Chord, KeyLabelStyle.Full))),
            WaitStep wait => Seconds(wait.Duration),
            TextStep text => T(L.StepType(name: Preview(text.Text))),
            MouseStep mouse => MouseName(mouse.Op),
            _ => string.Empty,
        };

    private static string Preview(SecretText text)
    {
        var revealed = Reveal(text);
        var max = Timings.Text.TextPreviewLength;
        return revealed.Length <= max ? revealed : revealed[..max] + "…";
    }

    private TargetModel? Target(Shortcut shortcut) =>
        shortcut.Action switch
        {
            UrlAction url => new TargetModel(
                T(L.Url),
                Targets.Text(url.Target),
                url.Target is UrlTarget.Raw { Text.Length: > 0 },
                T(L.BadUrl),
                string.Empty,
                [],
                T(L.SearchDictate)
            ),
            AppAction app => new TargetModel(
                T(L.AppPath),
                Targets.Text(app.Target),
                false,
                string.Empty,
                T(L.PickProgram),
                [.. _apps.Select(a => new AppChip(a.Process.Value, a.Name, false))],
                T(L.SearchDictate)
            ),
            _ => null,
        };

    private PinModel Pin()
    {
        var pinned = Workspace.List is ListRef.AlwaysVisible;
        var origin = Workspace.List is ListRef.InProfile profile
            ? Workspace.ListName(profile)
            : Workspace.ListName(new ListRef.InProfile(ProfileId.General));
        return new PinModel(
            T(L.PinAll2),
            pinned ? T(L.PinAllOn2) : T(L.PinAllOff2(profile: origin)),
            pinned
        );
    }

    private MoreModel More(Shortcut shortcut, int index)
    {
        var count = Workspace.Shortcuts.Count;
        var holds = shortcut.Action is HoldAction or ToggleAction;
        var text = shortcut.Action as TextAction;
        var number = index >= 0 ? index + 1 : count + 1;
        var name = shortcut.Name.Get(Language, LangCode.Es);
        var settings = _s.Store.Current.Settings;
        return new MoreModel(
            T(L.MoreOpts),
            index >= 0 ? T(L.PositionOf(index: index + 1, total: count)) : string.Empty,
            _moreOpen,
            T(L.Position),
            [
                Position(PositionMove.First, "first_page", L.First, index, count),
                Position(PositionMove.Before, "chevron_left", L.Before, index, count),
                Position(PositionMove.After, "chevron_right", L.After, index, count),
                Position(PositionMove.Last, "last_page", L.LastPos, index, count),
            ],
            holds ? T(L.AutoRelease) : null,
            holds ? Holds(shortcut.Options.MaxHold) : [],
            T(L.AutoReleaseD),
            text is null ? null : T(L.TextMethod),
            text is null
                ? []
                :
                [
                    new ChoiceOption(0, T(L.TmType), text.Method == TextMethod.Unicode),
                    new ChoiceOption(1, T(L.TmPaste), text.Method == TextMethod.Paste),
                ],
            T(L.TextEnc),
            T(L.ConfirmRun),
            T(L.ConfirmRunD),
            shortcut.Options.Confirm,
            T(L.CtxPin),
            _s.Store.Current.Frequents.Pins.Contains(shortcut.Id),
            Number(number),
            settings.VoiceNumbers ? T(L.VoiceSay(index: number, name: name)) : T(L.VoiceOff),
            T(L.VoiceHowT),
            _voiceHowOpen,
            [T(L.Vh1), T(L.Vh2(index: number, name: name)), T(L.Vh3)]
        );
    }

    private PositionOption Position(
        PositionMove move,
        string icon,
        Message label,
        int index,
        int count
    ) => new(move, icon, T(label), Positions.Target(move, index, count) is not null);

    private ValueList<ChoiceOption> Holds(HoldLimit limit)
    {
        var choices = Timings.KeySafety.AutoReleaseChoices;
        var selected = limit switch
        {
            HoldLimit.After after when choices.IndexOf(after.Limit) is var at and >= 0 => at + 1,
            HoldLimit.Never => choices.Length + 1,
            _ => 0,
        };
        var options = new List<ChoiceOption> { new(0, T(L.HoldAsGeneral), selected == 0) };
        for (var i = 0; i < choices.Length; i++)
        {
            var choice = choices[i];
            var label =
                choice.TotalSeconds % 60 != 0
                    ? L.HoldSeconds((long)choice.TotalSeconds)
                    : L.HoldMinutes((long)choice.TotalMinutes);
            options.Add(new ChoiceOption(i + 1, T(label), selected == i + 1));
        }

        options.Add(
            new ChoiceOption(choices.Length + 1, T(L.Never), selected == choices.Length + 1)
        );
        return [.. options];
    }

    private TestModel Test(Shortcut shortcut)
    {
        var target = ChosenTarget();
        var complete = ShortcutCompleteness.Evaluate(shortcut) == CompletenessIssue.None;
        var blocked =
            ComboWarnings.Of(ActionKinds.ChordOf(shortcut.Action)) == ComboWarning.Blocked;
        var (phaseIcon, phase) = Phase(shortcut);
        return new TestModel(
            T(L.TestTitle),
            T(L.Close),
            Sequence(shortcut),
            T(L.PlaySeq),
            What(shortcut),
            phaseIcon,
            phase,
            T(L.TestIn),
            [
                .. _apps.Select(a => new AppChip(
                    a.Process.Value,
                    a.Name,
                    target is not null && a.Process == target.Process
                )),
            ],
            _apps.IsEmpty ? T(L.NoOpenApps) : null,
            T(L.TestLive2(app: target?.Name ?? string.Empty)),
            target is not null
                && complete
                && !blocked
                && !_running
                && Workspace.Pane is EditorPane.Editing,
            T(L.TestHow),
            _answer == TestAnswer.Asking && target is not null
                ? T(L.TestAskQ(app: target.Name))
                : null,
            T(L.YesWorked),
            T(L.NoWorked),
            _answer == TestAnswer.Yes ? T(L.TestOk) : null,
            _answer == TestAnswer.No ? T(L.TipsTitle) : null,
            _answer == TestAnswer.No ? [T(L.Tip1), T(L.Tip2), T(L.Tip3), T(L.Tip4)] : [],
            _running
        );
    }

    private ValueList<SequenceItem> Sequence(Shortcut shortcut)
    {
        var down = _frame?.Down ?? 0;
        switch (shortcut.Action)
        {
            case MacroAction macro:
                return
                [
                    .. macro.Steps.Select(
                        (step, i) =>
                            new SequenceItem(
                                StepText(step),
                                StepIcon(step),
                                i > 0 ? "→" : null,
                                i < down
                            )
                    ),
                ];
            case { } action when ActionKinds.ChordOf(action) is { } chord:
                var labels = _s.Catalogs().KeyLabels;
                return
                [
                    .. chord.Strokes.Select(
                        (stroke, i) =>
                            new SequenceItem(
                                KeyChordFormatter.KeyText(
                                    stroke,
                                    labels,
                                    KeyLabelStyle.Full,
                                    Language,
                                    LangCode.Es
                                ),
                                null,
                                i > 0 ? "+" : null,
                                i < down
                            )
                    ),
                ];
            case MouseAction mouse:
                return [new SequenceItem(MouseName(mouse.Op), "mouse", null, down > 0)];
            case TextAction text:
                return [new SequenceItem(Preview(text.Text), "text_fields", null, down > 0)];
            case UrlAction url:
                return [new SequenceItem(Targets.Text(url.Target), "public", null, down > 0)];
            case AppAction app:
                return [new SequenceItem(Targets.Text(app.Target), "open_in_new", null, down > 0)];
            default:
                return
                [
                    new SequenceItem(
                        shortcut.Name.Get(Language, LangCode.Es),
                        "bolt",
                        null,
                        down > 0
                    ),
                ];
        }
    }

    private string What(Shortcut shortcut) =>
        shortcut.Action switch
        {
            TapAction => T(L.TwTap),
            HoldAction => T(L.TwHold),
            ToggleAction => T(L.TwToggle),
            TextAction => T(L.TwText),
            MouseAction => T(L.TwMouse),
            UrlAction => T(L.TwUrl),
            AppAction => T(L.TwApp),
            MacroAction macro => T(L.TwMacro(macro.Steps.Count)),
            _ => string.Empty,
        };

    private (string? Icon, string? Text) Phase(Shortcut shortcut) =>
        _frame?.Phase switch
        {
            PlaybackPhase.ReleasedAll => ("check_circle", T(L.PhReleasedAll)),
            PlaybackPhase.Touch => ("touch_app", T(L.PhTouch)),
            PlaybackPhase.Holding => ("pan_tool", T(L.PhHolding)),
            PlaybackPhase.Lift => ("back_hand", T(L.PhLift)),
            PlaybackPhase.FirstTap => ("touch_app", T(L.PhTap1)),
            PlaybackPhase.Latched => ("lock", T(L.PhLatched)),
            PlaybackPhase.SecondTap => ("touch_app", T(L.PhTap2)),
            PlaybackPhase.Released => ("lock_open", T(L.PhReleased)),
            PlaybackPhase.Step when shortcut.Action is MacroAction macro => (
                "play_arrow",
                T(L.PhStep(index: _frame.Step + 1, total: macro.Steps.Count))
            ),
            PlaybackPhase.Sentence => ("info", What(shortcut)),
            PlaybackPhase.Done => ("check_circle", T(L.PhDone)),
            _ => (null, null),
        };

    private FooterModel Footer(Shortcut shortcut, bool saved) =>
        new(
            T(L.Test),
            _testOpen,
            T(L.Duplicate),
            T(IsArmed(nameof(DeleteShortcut), shortcut.Id.Value) ? L.DelConfirm : L.Delete),
            IsArmed(nameof(DeleteShortcut), shortcut.Id.Value),
            saved
        );
}
