using Clicalo.Application.Engine;
using Clicalo.Application.Ports;
using Clicalo.Domain.Execution;
using Clicalo.Domain.Timing;
using Microsoft.Extensions.Time.Testing;

namespace Clicalo.Application.Tests.Engine;

/// <summary>
/// Clícalo's own chords go through the engine (blueprint §3.6, D-14, D-22): a request to the mailbox, sent by the host
/// with its generation under the gate (INV-11), in test mode and pause too (INV-7), and answered to the requester; a
/// hung or replaced engine answers «not sent» after <c>Timings.Engine.InternalChordWait</c>.
/// </summary>
[Trait("Req", "REG-03")]
[Trait("Req", "BUS-003")]
public sealed class EngineKeyEffectsTests
{
    private sealed class Hotkey : IInternalRightsHotkey
    {
        public bool IsRegistered { get; set; } = true;

        public ValueTask<bool> WaitForRightsAsync(CancellationToken cancellationToken) =>
            ValueTask.FromResult(true);

        public ValueTask<bool> WaitForChordReleaseAsync(CancellationToken cancellationToken) =>
            ValueTask.FromResult(true);
    }

    private sealed class Inbox(Func<EngineEvent, bool> post) : IEngineInbox
    {
        public List<EngineEvent> Posted { get; } = [];

        public bool Post(EngineEvent engineEvent)
        {
            Posted.Add(engineEvent);
            return post(engineEvent);
        }
    }

    [Fact]
    public async Task An_unregistered_rights_chord_is_never_asked_for()
    {
        var inbox = new Inbox(static _ => true);
        var effects = new EngineKeyEffects(
            inbox,
            new InternalChordReplies(),
            new Hotkey { IsRegistered = false },
            TimeProvider.System
        );

        (await effects.SendRightsHotkeyAsync(TestContext.Current.CancellationToken)).ShouldBeFalse();

        inbox.Posted.ShouldBeEmpty();
    }

    [Fact]
    public async Task The_engine_sends_the_chord_with_its_generation_and_answers()
    {
        using var world = new HostWorld(realReducer: true);
        var replies = new InternalChordReplies();
        using var host = new EngineHost(
            world.Ports with
            {
                ChordReplies = replies,
            },
            HostWorld.Generation,
            HostWorld.Config,
            world.Time,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<EngineHost>.Instance
        );
        var effects = new EngineKeyEffects(
            new Inbox(e => host.Post(e)),
            replies,
            new Hotkey(),
            world.Time
        );

        var rights = effects.SendRightsHotkeyAsync(TestContext.Current.CancellationToken);
        host.Pump();
        var dictation = effects.SendDictationChordAsync(TestContext.Current.CancellationToken);
        host.Pump();

        (await rights).ShouldBeTrue();
        (await dictation).ShouldBeTrue();
        world.Injector.Chords.ShouldBe([
            (HostWorld.Generation, InternalChord.Rights),
            (HostWorld.Generation, InternalChord.Dictation),
        ]);
        replies.Pending.ShouldBe(0);
    }

    [Fact]
    [Trait("Req", "TAC-008")]
    public void An_internal_chord_goes_in_test_mode_and_pause_too()
    {
        var state = EngineState.Empty with { TestMode = true, Paused = true };

        var transition = EngineReducer.Reduce(
            state,
            new EngineEvent.InternalChordRequested(InternalChord.Dictation, 5),
            HostWorld.Config,
            0
        );

        transition
            .Effects.OfType<EngineEffect.SendInternalChord>()
            .ShouldHaveSingleItem()
            .ShouldBe(new EngineEffect.SendInternalChord(InternalChord.Dictation, 5));
    }

    [Fact]
    public async Task A_fenced_engine_answers_that_the_chord_did_not_go()
    {
        using var world = new HostWorld(realReducer: true);
        var replies = new InternalChordReplies();
        using var host = new EngineHost(
            world.Ports with
            {
                ChordReplies = replies,
            },
            HostWorld.Generation,
            HostWorld.Config,
            world.Time,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<EngineHost>.Instance
        );
        world.Injector.NextStatus = InjectionStatus.Fenced;
        var effects = new EngineKeyEffects(
            new Inbox(e => host.Post(e)),
            replies,
            new Hotkey(),
            world.Time
        );

        var sent = effects.SendDictationChordAsync(TestContext.Current.CancellationToken);
        host.Pump();

        (await sent).ShouldBeFalse();
        host.IsStopped.ShouldBeTrue();
    }

    [Fact]
    public async Task An_engine_that_never_answers_means_not_sent_after_the_wait()
    {
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 9, 26, 10, 0, 0, TimeSpan.Zero));
        var replies = new InternalChordReplies();
        var effects = new EngineKeyEffects(
            new Inbox(static _ => true),
            replies,
            new Hotkey(),
            time
        );

        var sent = effects
            .SendRightsHotkeyAsync(TestContext.Current.CancellationToken)
            .AsTask();
        time.Advance(Timings.Engine.InternalChordWait - TimeSpan.FromTicks(1));
        sent.IsCompleted.ShouldBeFalse();
        time.Advance(TimeSpan.FromTicks(1));

        (await sent).ShouldBeFalse();
        replies.Pending.ShouldBe(0);
    }

    [Fact]
    public async Task A_refused_request_is_not_sent()
    {
        var replies = new InternalChordReplies();
        var effects = new EngineKeyEffects(
            new Inbox(static _ => false),
            replies,
            new Hotkey(),
            TimeProvider.System
        );

        (await effects.SendDictationChordAsync(TestContext.Current.CancellationToken)).ShouldBeFalse();
        replies.Pending.ShouldBe(0);
    }
}
