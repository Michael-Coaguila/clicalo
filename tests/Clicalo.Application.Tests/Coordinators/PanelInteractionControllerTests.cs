using Clicalo.Application.Coordinators;
using Clicalo.Domain.Execution;
using Clicalo.Domain.Keys;
using Clicalo.Domain.Touch;
using Clicalo.TestKit.Time;

namespace Clicalo.Application.Tests.Coordinators;

/// <summary>
/// The panel controller builds the engine events of blueprint §7.1 and decides nothing: every activation of a tile,
/// whatever its origin, becomes one <see cref="EngineEvent.Activation"/> for the single policy of the engine (EJE-001).
/// </summary>
[Trait("Req", "EJE-001")]
public sealed class PanelInteractionControllerTests
{
    private static readonly ContactSummary Quick = new(
        TimeSpan.FromMilliseconds(90),
        MaxDisplacementPx: 2,
        PalmLike: false
    );

    private readonly RecordingEngineInbox _engine = new();
    private readonly Microsoft.Extensions.Time.Testing.FakeTimeProvider _time =
        TestTime.CreateProvider();
    private long _epoch = 7;
    private readonly PanelInteractionController _controller;

    public PanelInteractionControllerTests() =>
        _controller = new PanelInteractionController(_engine, () => _epoch, _time);

    [Fact]
    [Trait("Req", "EJE-003")]
    public void A_tap_becomes_an_activation_at_contact_end_with_the_summary_profile_mode_and_epoch()
    {
        var tile = TestTiles.Tap();
        var at = TestTime.Epoch.AddSeconds(3);

        _controller.Tapped(tile, contactId: 17, PointerKind.Finger, Quick, at).ShouldBeTrue();

        var activation = _engine
            .Events.ShouldHaveSingleItem()
            .ShouldBeOfType<EngineEvent.Activation>();
        activation.Request.ShouldBe(
            new ActivationRequest(
                ActivationPhase.ContactEnded,
                ActivationOrigin.Touch,
                17,
                Quick,
                at
            )
        );
        activation.Shortcut.ShouldBeSameAs(tile.Shortcut);
        activation.OriginProfile.ShouldBe(TestTiles.Word);
        activation.Injection.ShouldBe(InjectionMode.VirtualKey);
        activation.Epoch.ShouldBe(7);
        activation.EditMode.ShouldBeFalse();
        activation.RequiredForeground.ShouldBeNull(
            "a panel tap goes to the foreground app, whatever it is"
        );
        activation.Lane.ShouldBe(EngineLane.Normal);
    }

    [Theory]
    [InlineData(PointerKind.Finger, ActivationOrigin.Touch)]
    [InlineData(PointerKind.Pen, ActivationOrigin.Pen)]
    [InlineData(PointerKind.Mouse, ActivationOrigin.Mouse)]
    [Trait("Req", "TAC-002")]
    public void The_device_of_the_contact_is_the_origin_of_the_activation(
        PointerKind device,
        ActivationOrigin origin
    )
    {
        _controller.Tapped(TestTiles.Tap(), 1, device, Quick, TestTime.Epoch);

        _engine
            .Events.ShouldHaveSingleItem()
            .ShouldBeOfType<EngineEvent.Activation>()
            .Request.Origin.ShouldBe(origin);
    }

    [Fact]
    [Trait("Req", "EJE-004")]
    public void A_hold_presses_at_contact_start_and_keeps_the_mode_of_its_profile()
    {
        var tile = TestTiles.Hold();

        _controller
            .HoldStarted(tile, contactId: 4, PointerKind.Finger, TestTime.Epoch)
            .ShouldBeTrue();

        var activation = _engine
            .Events.ShouldHaveSingleItem()
            .ShouldBeOfType<EngineEvent.Activation>();
        activation.Request.Phase.ShouldBe(ActivationPhase.ContactStarted);
        activation.Request.ContactId.ShouldBe(4);
        activation.Request.Contact.ShouldBeNull("the contact has not ended yet");
        activation.Injection.ShouldBe(InjectionMode.ScanCode);
    }

    [Theory]
    [InlineData(HoldEndReason.Lifted, false)]
    [InlineData(HoldEndReason.Canceled, true)]
    [InlineData(HoldEndReason.LeftTarget, true)]
    [InlineData(HoldEndReason.Reset, true)]
    [Trait("Req", "EJE-004")]
    [Trait("Req", "EJE-006")]
    [Trait("Req", "SEG-007")]
    public void The_end_of_a_hold_releases_its_contact_in_the_priority_lane(
        HoldEndReason reason,
        bool cancelled
    )
    {
        _controller.HoldEnded(contactId: 4, Quick, reason).ShouldBeTrue();

        var ended = _engine
            .Events.ShouldHaveSingleItem()
            .ShouldBeOfType<EngineEvent.ContactEnded>();
        ended.ShouldBe(new EngineEvent.ContactEnded(4, Quick, cancelled));
        ended.Lane.ShouldBe(EngineLane.Priority, "releasing never waits behind a macro (§3.2)");
    }

    [Fact]
    [Trait("Req", "EJE-005")]
    [Trait("Req", "ACC-004")]
    public void A_ui_automation_invocation_has_no_contact_and_no_duration()
    {
        _time.Advance(TimeSpan.FromSeconds(2));

        _controller.Invoked(TestTiles.Hold()).ShouldBeTrue();

        var activation = _engine
            .Events.ShouldHaveSingleItem()
            .ShouldBeOfType<EngineEvent.Activation>();
        activation.Request.ShouldBe(
            new ActivationRequest(
                ActivationPhase.Invoke,
                ActivationOrigin.UiaInvoke,
                null,
                null,
                TestTime.Epoch.AddSeconds(2)
            )
        );
    }

    [Fact]
    [Trait("Req", "SEG-003")]
    public void Release_all_asks_the_engine_to_release_everything_for_the_user()
    {
        _controller.ReleaseAll().ShouldBeTrue();

        var release = _engine
            .Events.ShouldHaveSingleItem()
            .ShouldBeOfType<EngineEvent.ReleaseAll>();
        release.Reason.ShouldBe(ReleaseReason.User);
        release.Lane.ShouldBe(EngineLane.Priority);
    }

    [Fact]
    [Trait("Req", "EJE-006")]
    public void Every_activation_reads_the_foreground_epoch_of_its_own_moment()
    {
        _controller.Tapped(TestTiles.Tap(), 1, PointerKind.Finger, Quick, TestTime.Epoch);
        _epoch = 8;
        _controller.Invoked(TestTiles.Toggle());

        _engine
            .Events.OfType<EngineEvent.Activation>()
            .Select(activation => activation.Epoch)
            .ShouldBe([7L, 8L]);
    }

    [Fact]
    public void A_stopped_engine_refuses_and_the_controller_says_so()
    {
        _engine.Stop();

        _controller
            .Tapped(TestTiles.Tap(), 1, PointerKind.Finger, Quick, TestTime.Epoch)
            .ShouldBeFalse();
        _controller.ReleaseAll().ShouldBeFalse();
    }
}
