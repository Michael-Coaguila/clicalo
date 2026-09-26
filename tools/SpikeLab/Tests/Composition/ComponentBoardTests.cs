using Clicalo.Application.Ports;
using Clicalo.Tools.SpikeLab.Composition;

namespace Clicalo.Tools.SpikeLab.Tests.Composition;

public sealed class ComponentBoardTests
{
    [Fact]
    public void A_piece_that_throws_is_a_failure_with_its_type_and_does_not_stop_the_laboratory()
    {
        var board = new ComponentBoard();

        var ok = board.Try(
            "TrayIcon",
            "Bandeja",
            () => throw new InvalidOperationException("sin bandeja")
        );

        ok.ShouldBeFalse();
        board.AllReady.ShouldBeFalse();
        board.Components[0].State.ShouldBe(LabComponentState.Failed);
        board.Components[0].Detail.ShouldBe("InvalidOperationException: sin bandeja");
    }

    [Fact]
    public async Task A_later_report_replaces_the_state_of_the_same_piece()
    {
        var board = new ComponentBoard();
        var changes = 0;
        board.Changed += (_, _) => changes++;

        board.Pending("PointerInputSource", "Espera a SysEventsThread.");
        (
            await board.TryAsync("PointerInputSource", "Punteros propios", () => Task.CompletedTask)
        ).ShouldBeTrue();

        board.Components.ShouldHaveSingleItem().State.ShouldBe(LabComponentState.Ready);
        board.AllReady.ShouldBeTrue();
        changes.ShouldBe(2);
    }

    [Fact]
    public void The_relay_leases_nothing_until_the_orchestrator_exists_and_always_tells_the_laboratory()
    {
        var relay = new ArbiterRelay();
        var reported = new List<bool>();
        relay.ViolationReported += (_, args) => reported.Add(args.Forwarded);
        var violation = new ActivationViolation(
            new SurfaceId(SurfaceKind.Panel, 0),
            new WindowToken(0x10),
            ActivationMessage.Activate,
            ActivationCause.External,
            DateTimeOffset.UnixEpoch
        );

        relay.IsActivationLeased(new WindowToken(0x10)).ShouldBeFalse();
        relay.ReportViolation(violation);

        var arbiter = new LeasingArbiter();
        relay.Target = arbiter;
        relay.IsActivationLeased(new WindowToken(0x10)).ShouldBeTrue();
        relay.ReportViolation(violation);

        reported.ShouldBe([false, true]);
        arbiter.Reported.ShouldBe(1);
    }

    private sealed class LeasingArbiter : IActivationArbiter
    {
        public int Reported { get; private set; }

        public bool IsActivationLeased(WindowToken window) => true;

        public void ReportViolation(ActivationViolation violation) => Reported++;
    }
}
