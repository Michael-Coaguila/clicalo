using Clicalo.Application.Ports;
using Clicalo.Domain.Catalog;
using Clicalo.Domain.Execution;
using Clicalo.Domain.Geometry;
using Clicalo.Domain.Keys;
using Clicalo.Domain.Library;
using Clicalo.Domain.Primitives;

namespace Clicalo.Application.Tests.Engine;

/// <summary>
/// What the host adds around the reducer for the actions: the last pointer position outside Clícalo for every tap
/// (EJE-009) and the soft sound after an action (EJE-012), never on the engine's own time.
/// </summary>
public sealed class EngineHostFeedbackTests
{
    private static readonly PhysicalPoint Outside = new(640, 480);

    private static EngineEvent.Activation Tap(PhysicalPoint? pointer) =>
        new(
            new ActivationRequest(
                ActivationPhase.Invoke,
                ActivationOrigin.UiaInvoke,
                ContactId: null,
                Contact: null,
                new DateTimeOffset(2026, 9, 26, 10, 0, 0, TimeSpan.Zero)
            ),
            new Shortcut(
                new ShortcutId("right"),
                LocalizedText.Same("right", LangCode.Es, LangCode.En),
                new IconRef("mouse"),
                AutoIcon: false,
                new CategoryId("general"),
                new MouseAction(MouseOp.RightClick, ScrollSpeed.Normal),
                new ShortcutOptions(
                    Confirm: false,
                    new HoldLimit.InheritGlobal(),
                    IsPrivate: false
                ),
                Origin: null,
                PinnedFrom: null
            ),
            OriginProfile: null,
            InjectionMode.VirtualKey,
            pointer,
            EditMode: false,
            Epoch: 3,
            RequiredForeground: null
        );

    [Fact]
    [Trait("Req", "EJE-009")]
    public void A_tap_without_a_pointer_gets_the_last_position_outside_clicalo()
    {
        using var world = new HostWorld(ports: ports =>
            ports with
            {
                PointerPosition = new FixedPointer(Outside),
            }
        );

        world.Handle(Tap(pointer: null));

        world.Seen.OfType<EngineEvent.Activation>().Single().LastExternalPointer.ShouldBe(Outside);
    }

    [Fact]
    [Trait("Req", "EJE-009")]
    public void A_tap_that_brings_its_own_pointer_keeps_it()
    {
        var own = new PhysicalPoint(1, 2);
        using var world = new HostWorld(ports: ports =>
            ports with
            {
                PointerPosition = new FixedPointer(Outside),
            }
        );

        world.Handle(Tap(own));

        world.Seen.OfType<EngineEvent.Activation>().Single().LastExternalPointer.ShouldBe(own);
    }

    [Fact]
    [Trait("Req", "EJE-009")]
    public void The_real_reducer_clicks_at_the_tracked_position()
    {
        using var world = new HostWorld(
            realReducer: true,
            ports: ports => ports with { PointerPosition = new FixedPointer(Outside) }
        );

        world.Host.Post(Tap(pointer: null));
        world.Host.Pump();

        world.Injector.MouseActions.ShouldHaveSingleItem().Target.ShouldBe(Outside);
    }

    [Fact]
    [Trait("Req", "EJE-012")]
    public void The_sound_effect_plays_through_its_port()
    {
        var sound = new CountingSound();
        using var world = new HostWorld(ports: ports => ports with { Sound = sound });

        world.Handle(new EngineEvent.SessionResumed(), new EngineEffect.PlayFeedbackSound());

        sound.Played.ShouldBe(1);
    }

    [Fact]
    [Trait("Req", "EJE-012")]
    public void Without_a_sound_port_nothing_plays_and_nothing_fails()
    {
        using var world = new HostWorld();

        world.Handle(new EngineEvent.SessionResumed(), new EngineEffect.PlayFeedbackSound());

        world.Observer.Notices.ShouldBeEmpty();
    }

    private sealed class FixedPointer(PhysicalPoint point) : IPointerPositionSource
    {
        public PhysicalPoint? LastExternal => point;
    }

    private sealed class CountingSound : IFeedbackSound
    {
        public int Played { get; private set; }

        public void Play() => Played++;
    }
}
