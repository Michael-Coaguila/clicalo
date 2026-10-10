using Clicalo.Application.Localization;
using Clicalo.Application.Ports;
using Clicalo.Domain.Catalog;
using Clicalo.Domain.Execution;
using Clicalo.Domain.Messages;
using Clicalo.Domain.Primitives;
using Clicalo.Domain.Timing;
using Clicalo.Domain.Touch;
using Clicalo.Presentation.Panel.EditMode;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Clicalo.Presentation.Panel.TestMode;

/// <summary>
/// Test mode (TAC-008), switched on from Quick settings: for 30 s every touch on a tile passes the filter of TAC-002
/// and is only marked, ✓ or ⊘ for 700 ms, with [tmOk], [tShort] or [tDouble]; nothing is sent. It tells the engine
/// (<see cref="EngineEvent.SetTestMode"/>), so Hold, Toggle, the sticky modifiers, ↻ Repeat and voice send nothing
/// either (INV-7). While on, the sticky notice [tmStart] shows and the indicator «Modo prueba · {s} s» stays even when
/// another notice replaces it; it ends by itself with [tmEnd], and switching it off by hand removes the notice.
/// </summary>
/// <remarks>
/// Lives on the UI thread of the Surfaces role. Its timers fire on the <see cref="TimeProvider"/>'s thread and come back
/// through <c>post</c> (the dispatcher's <c>BeginInvoke</c>). The duration and the countdown are
/// <see cref="TestModeState"/>'s; which touches are marked is <see cref="TestModeMark"/>'s.
/// </remarks>
public sealed class TestModeViewModel : ObservableObject
{
    private const string ModeIcon = "science";
    private const string CountedIcon = "check";
    private const string IgnoredIcon = "block";

    private readonly IEngineInbox _engine;
    private readonly ILocalizationContext _localization;
    private readonly IPanelNoticeSink _notices;
    private readonly TimeProvider _time;
    private readonly Action<Action> _post;
    private readonly Dictionary<ShortcutId, (TestModeMark Mark, long Token, ITimer Timer)> _marks =
    [];
    private TestModeState _state = TestModeState.Off;
    private ITimer? _ticker;
    private long _run;
    private long _nextMark;
    private bool _isOn;
    private string _indicatorText = string.Empty;

    /// <summary>Creates test mode, off.</summary>
    /// <param name="engine">The engine mailbox: it sends nothing while test mode is on.</param>
    /// <param name="localization">The interface language.</param>
    /// <param name="notices">The notice bar.</param>
    /// <param name="time">The clock of the 30 s and of the marks.</param>
    /// <param name="post">Runs an action on the UI thread without waiting (the dispatcher's <c>BeginInvoke</c>).</param>
    public TestModeViewModel(
        IEngineInbox engine,
        ILocalizationContext localization,
        IPanelNoticeSink notices,
        TimeProvider time,
        Action<Action> post
    )
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(localization);
        ArgumentNullException.ThrowIfNull(notices);
        ArgumentNullException.ThrowIfNull(time);
        ArgumentNullException.ThrowIfNull(post);
        _engine = engine;
        _localization = localization;
        _notices = notices;
        _time = time;
        _post = post;
    }

    /// <summary>A tile's mark appeared or went away: the tile repaints its ✓ or ⊘.</summary>
    public event EventHandler<TestMarkChangedEventArgs>? MarkChanged;

    /// <summary>Whether test mode is on (the switch of Quick settings and the indicator).</summary>
    public bool IsOn
    {
        get => _isOn;
        private set => SetProperty(ref _isOn, value);
    }

    /// <summary>«Modo prueba · {s} s» while on; empty while off.</summary>
    public string IndicatorText
    {
        get => _indicatorText;
        private set => SetProperty(ref _indicatorText, value);
    }

    /// <summary>The mark a tile shows now, or <see langword="null"/>.</summary>
    /// <param name="shortcut">The tile's shortcut.</param>
    public TestModeMark? MarkOf(ShortcutId shortcut) =>
        _marks.TryGetValue(shortcut, out var marked) ? marked.Mark : null;

    /// <summary>What a mark says in words, its accessible state (CUA-009: never color alone).</summary>
    /// <param name="mark">The mark.</param>
    public string MarkText(TestModeMark mark) => _localization.Current.Format(NoticeOf(mark));

    /// <summary>The Material Symbols glyph of a mark: <c>check</c> or <c>block</c>.</summary>
    /// <param name="mark">The mark.</param>
    public static string MarkIcon(TestModeMark mark) => mark.Accepted ? CountedIcon : IgnoredIcon;

    /// <summary>The switch of Quick settings.</summary>
    public void Toggle()
    {
        if (IsOn)
        {
            Stop();
        }
        else
        {
            Start();
        }
    }

    /// <summary>Switches test mode on for 30 s (again from the start when it was on).</summary>
    public void Start()
    {
        var now = _time.GetUtcNow();
        _state = TestModeState.StartedAt(now);
        _run++;
        _ = _engine.Post(new EngineEvent.SetTestMode(true));
        IsOn = true;
        _notices.ShowSticky(
            this,
            new PanelNotice(L.TmStart, new IconRef(ModeIcon), NoticeTone.Notice)
        );
        Tick(_run);
    }

    /// <summary>Switches test mode off by hand: the engine sends again and [tmStart] goes away.</summary>
    public void Stop()
    {
        if (!IsOn)
        {
            return;
        }

        End();
        _notices.ClearSticky(this);
    }

    /// <summary>
    /// An accepted tap, or a hold that started, on a tile: in test mode it is marked ✓ with [tmOk] and goes no further.
    /// </summary>
    /// <param name="shortcut">The tile's shortcut.</param>
    /// <returns>Whether test mode took it (the tile must not run).</returns>
    public bool OnCounted(ShortcutId shortcut)
    {
        if (!IsOn)
        {
            return false;
        }

        Mark(shortcut, TestModeMark.Counted);
        return true;
    }

    /// <summary>A touch the filter ignored on a tile: in test mode a short or a debounced touch is marked ⊘.</summary>
    /// <param name="shortcut">The tile's shortcut.</param>
    /// <param name="reason">Why the recognizer ignored it.</param>
    public void OnIgnored(ShortcutId shortcut, IgnoreReason reason)
    {
        if (IsOn && TestModeMark.ForIgnored(reason) is { } mark)
        {
            Mark(shortcut, mark);
        }
    }

    /// <summary>Formats the indicator again in the current language (IDI-001).</summary>
    public void Relocalize() => UpdateIndicator(_time.GetUtcNow());

    private static Message NoticeOf(TestModeMark mark) =>
        mark.Accepted ? L.TmOk
        : mark.Reason == IgnoreReason.Debounced ? L.TDouble
        : L.TShort;

    private void Mark(ShortcutId shortcut, TestModeMark mark)
    {
        var token = ++_nextMark;
        if (_marks.TryGetValue(shortcut, out var previous))
        {
            previous.Timer.Dispose();
        }

        var timer = _time.CreateTimer(
            _ => _post(() => Unmark(shortcut, token)),
            null,
            Timings.TestMode.TestMarkDuration,
            Timeout.InfiniteTimeSpan
        );
        _marks[shortcut] = (mark, token, timer);
        MarkChanged?.Invoke(this, new TestMarkChangedEventArgs(shortcut, mark));
        _notices.Notify(
            new PanelNotice(
                NoticeOf(mark),
                new IconRef(MarkIcon(mark)),
                mark.Accepted ? NoticeTone.Notice : NoticeTone.Warning
            )
        );
    }

    private void Unmark(ShortcutId shortcut, long token)
    {
        if (_marks.TryGetValue(shortcut, out var marked) && marked.Token == token)
        {
            marked.Timer.Dispose();
            _ = _marks.Remove(shortcut);
            MarkChanged?.Invoke(this, new TestMarkChangedEventArgs(shortcut, null));
        }
    }

    /// <summary>Repaints the countdown and waits for its next second, or ends when the 30 s are over.</summary>
    private void Tick(long run)
    {
        if (run != _run || !IsOn)
        {
            return;
        }

        var now = _time.GetUtcNow();
        if (_state.UntilNextChange(now) is not { } wait)
        {
            End();
            // AVI-002: the fixed notice lasts while the mode lasts; [tmEnd] says it ended.
            _notices.ClearSticky(this);
            _notices.Notify(new PanelNotice(L.TmEnd, new IconRef(ModeIcon), NoticeTone.Notice));
            return;
        }

        UpdateIndicator(now);
        _ticker?.Dispose();
        _ticker = _time.CreateTimer(
            _ => _post(() => Tick(run)),
            null,
            wait,
            Timeout.InfiniteTimeSpan
        );
    }

    private void End()
    {
        _run++;
        _ticker?.Dispose();
        _ticker = null;
        _state = TestModeState.Off;
        _ = _engine.Post(new EngineEvent.SetTestMode(false));
        IsOn = false;
        IndicatorText = string.Empty;
        var shown = _marks.Keys.ToList();
        foreach (var marked in _marks.Values)
        {
            marked.Timer.Dispose();
        }

        _marks.Clear();
        foreach (var shortcut in shown)
        {
            MarkChanged?.Invoke(this, new TestMarkChangedEventArgs(shortcut, null));
        }
    }

    private void UpdateIndicator(DateTimeOffset now) =>
        IndicatorText = IsOn
            ? _localization.Current.Format(L.TmLeft(count: _state.SecondsLeft(now)))
            : string.Empty;
}
