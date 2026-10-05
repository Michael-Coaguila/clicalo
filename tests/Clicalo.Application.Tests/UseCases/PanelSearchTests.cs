using Clicalo.Application.Foreground;
using Clicalo.Application.Tests.Coordinators;
using Clicalo.Application.Tests.Foreground;
using Clicalo.Application.UseCases;
using Clicalo.Domain.Execution;
using Clicalo.Domain.Touch;
using static Clicalo.Application.Tests.Foreground.ForegroundWorld;

namespace Clicalo.Application.Tests.UseCases;

/// <summary>
/// The search field's keyboard (BUS-002, BUS-003, blueprint §3.6 «Ciclo de TextInput») with the real orchestrator on
/// a fake desktop: Word is in front, the panel takes a text lease only while the user types or dictates, and a result
/// runs only after Word is back in front, verified.
/// </summary>
[Trait("Req", "BUS-002")]
[Trait("Req", "REG-01")]
public sealed class PanelSearchTests : IDisposable
{
    private static readonly ContactSummary Quick = new(
        TimeSpan.FromMilliseconds(80),
        1,
        PalmLike: false
    );

    private readonly ForegroundWorld _world = new();
    private readonly RecordingEngineInbox _engine = new();
    private readonly FakeTouchKeyboard _keyboard = new();
    private readonly PanelSearch _search;
    private long _epoch = 7;

    public PanelSearchTests()
    {
        _world.Control.HasRights = true;

        // The monitor reports Word's return as a new epoch once the orchestrator gives the foreground back.
        _world.Control.DuringAttempt = window =>
        {
            if (window == Word)
            {
                Interlocked.Increment(ref _epoch);
            }
        };
        _search = new PanelSearch(
            _world.Orchestrator,
            _engine,
            () => Interlocked.Read(ref _epoch),
            _world.Time,
            _keyboard
        );
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose() => _world.Dispose();

    [Fact]
    public async Task Taking_the_keyboard_brings_the_panel_forward_under_a_text_lease()
    {
        (await TakeAsync()).ShouldBeTrue();

        _search.HasKeyboard.ShouldBeTrue();
        _world.Control.Foreground.ShouldBe(Panel);
        _world.Orchestrator.ActiveLease.ShouldNotBeNull().Kind.ShouldBe(LeaseKind.TextInput);
        _world.Orchestrator.ActiveLease!.PreviousForeground.ShouldBe(Word);
    }

    [Fact]
    public async Task A_refused_lease_leaves_the_field_without_the_keyboard()
    {
        _world.Control.HasRights = false;
        _world.Control.Script.Enqueue(false);
        _world.Control.Script.Enqueue(false);

        (await TakeAsync()).ShouldBeFalse();

        _search.HasKeyboard.ShouldBeFalse();
        _world.Control.Foreground.ShouldBe(Word);
    }

    [Fact]
    public async Task A_result_runs_only_after_the_app_is_back_and_must_still_be_in_front()
    {
        await TakeAsync();
        _world.Mark();
        var bold = TestTiles.Tap("bold");

        var outcome = await _world.CompleteAsync(
            _search.RunTappedAsync(bold, 5, PointerKind.Finger, Quick, At(), Ct).AsTask()
        );

        outcome.ShouldBe(SearchRunOutcome.Sent);
        _world.Control.Foreground.ShouldBe(Word);
        _world.Log.ShouldContain(static e =>
            string.Equals(e, "set Word", StringComparison.Ordinal)
        );
        var activation = _engine
            .Events.ShouldHaveSingleItem()
            .ShouldBeOfType<EngineEvent.Activation>();
        activation.Shortcut.ShouldBe(bold.Shortcut);
        activation.OriginProfile.ShouldBe(bold.OriginProfile);
        activation.Request.Phase.ShouldBe(ActivationPhase.ContactEnded);
        activation.Request.Origin.ShouldBe(ActivationOrigin.Touch);
        activation.Request.Contact.ShouldBe(Quick);
        activation.RequiredForeground.ShouldBe(new ForegroundWindowId((ulong)Word.Handle));
        activation.Epoch.ShouldBe(8, "the epoch of Word's return, not the one before it");
        _search.HasKeyboard.ShouldBeFalse();
    }

    [Fact]
    public async Task When_the_app_does_not_come_back_nothing_is_sent()
    {
        await TakeAsync();
        _world.Control.Script.Enqueue(false);
        _world.Control.Script.Enqueue(false);

        var outcome = await _world.CompleteAsync(
            _search.RunInvokedAsync(TestTiles.Tap(), Ct).AsTask()
        );

        outcome.ShouldBe(SearchRunOutcome.NotSent);
        _engine.Events.ShouldBeEmpty();
        _search.HasKeyboard.ShouldBeFalse();
    }

    [Fact]
    public async Task Without_the_epoch_moving_the_result_waits_the_retry_delay_and_then_goes()
    {
        await TakeAsync();
        _world.Control.DuringAttempt = null;

        var outcome = await _world.CompleteAsync(
            _search.RunInvokedAsync(TestTiles.Tap(), Ct).AsTask()
        );

        outcome.ShouldBe(SearchRunOutcome.Sent);
        _engine
            .Events.ShouldHaveSingleItem()
            .ShouldBeOfType<EngineEvent.Activation>()
            .Epoch.ShouldBe(7);
    }

    [Fact]
    [Trait("Req", "EJE-005")]
    public async Task A_hold_result_tapped_is_latched_as_when_invoked()
    {
        await TakeAsync();

        _ = await _world.CompleteAsync(
            _search.RunTappedAsync(TestTiles.Hold(), 5, PointerKind.Pen, Quick, At(), Ct).AsTask()
        );

        var request = _engine
            .Events.ShouldHaveSingleItem()
            .ShouldBeOfType<EngineEvent.Activation>()
            .Request;
        request.Phase.ShouldBe(ActivationPhase.Invoke);
        request.Origin.ShouldBe(ActivationOrigin.Pen);
        request.ContactId.ShouldBeNull();
    }

    [Fact]
    public async Task Without_the_keyboard_a_result_goes_at_once_to_the_app_in_front()
    {
        var outcome = await _search.RunInvokedAsync(TestTiles.Toggle(), Ct);

        outcome.ShouldBe(SearchRunOutcome.Sent);
        var activation = _engine
            .Events.ShouldHaveSingleItem()
            .ShouldBeOfType<EngineEvent.Activation>();
        activation.RequiredForeground.ShouldBeNull();
        activation.Request.Origin.ShouldBe(ActivationOrigin.UiaInvoke);
        _world.Control.Attempts.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_stopped_engine_is_reported()
    {
        _engine.Stop();

        (await _search.RunInvokedAsync(TestTiles.Tap(), Ct)).ShouldBe(
            SearchRunOutcome.EngineStopped
        );
    }

    [Fact]
    [Trait("Req", "BUS-003")]
    public async Task Dictation_and_the_touch_keyboard_only_go_to_the_field_while_it_holds_the_keyboard()
    {
        (await _search.DictateAsync(Ct)).ShouldBeFalse();
        (await _search.ShowTouchKeyboardAsync(Panel, Ct)).ShouldBeFalse();
        _keyboard.Calls.ShouldBeEmpty();

        await TakeAsync();
        (await _search.ShowTouchKeyboardAsync(Panel, Ct)).ShouldBeTrue();
        (await _search.DictateAsync(Ct)).ShouldBeTrue();

        _keyboard.Calls.ShouldBe(["show " + Panel, "dictate"]);
    }

    [Fact]
    [Trait("Req", "BUS-001")]
    public async Task Closing_gives_the_foreground_back_and_hides_the_touch_keyboard_it_showed()
    {
        await TakeAsync();
        await _search.ShowTouchKeyboardAsync(Panel, Ct);

        var outcome = await _search.ReleaseKeyboardAsync(Ct);

        outcome.ShouldBe(RestoreOutcome.Restored);
        _world.Control.Foreground.ShouldBe(Word);
        _keyboard.Calls.ShouldBe(["show " + Panel, "hide"]);
        _search.HasKeyboard.ShouldBeFalse();
        _engine.Events.ShouldBeEmpty();
        (await _search.ReleaseKeyboardAsync(Ct)).ShouldBeNull("nothing left to give back");
    }

    [Fact]
    [Trait("Req", "BUS-001")]
    public async Task Switching_apps_ends_the_keyboard_and_says_why()
    {
        await TakeAsync();
        var ended = _search.KeyboardEnded.ShouldNotBeNull();

        _world.Monitor.SwitchTo(Chrome);
        await _world.Orchestrator.ForegroundChangeHandled;

        (await EndOf(ended)).ShouldBe(LeaseEndReason.ForegroundChanged);
        _search.HasKeyboard.ShouldBeFalse();
    }

    private Task<bool> TakeAsync() =>
        _world.CompleteAsync(_search.TakeKeyboardAsync(Panel, LeaseOrigin.Touch, Ct).AsTask());

    private DateTimeOffset At() => _world.Time.GetUtcNow();
}
