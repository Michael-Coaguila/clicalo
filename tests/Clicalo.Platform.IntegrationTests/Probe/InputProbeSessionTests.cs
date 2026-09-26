using Clicalo.Platform.IntegrationTests.Desktop;
using Clicalo.TestKit.Windows;
using Clicalo.TestKit.Windows.Input;
using Clicalo.TestKit.Windows.Probe;

namespace Clicalo.Platform.IntegrationTests.Probe;

/// <summary>The probe starts, reports ready, answers commands and reaches the foreground legitimately (S0).</summary>
[Collection(DesktopCollectionDefinition.Name)]
[Trait("Requires", "Desktop")]
public sealed class InputProbeSessionTests(DesktopProbeFixture desktop)
{
    [DesktopFact]
    public async Task The_probe_reports_ready_and_takes_the_foreground_without_injected_input()
    {
        var ready = desktop.Probe.Ready;
        ready.Protocol.ShouldBe(1);
        ForegroundWindows.Exists(ready.Window).ShouldBeTrue();
        ready.ProcessId.ShouldBe(desktop.Probe.ProcessId);
        ready.SessionId.ShouldBeGreaterThan(0, "Session 0 has no interactive desktop.");
        ready.TimestampFrequency.ShouldBeGreaterThan(0);
        ready.KeyboardLayout.ShouldNotBe(0);

        await desktop.PrepareAsync();

        desktop.Probe.IsForeground.ShouldBeTrue();

        // Other tests of the collection may already have injected keys: look at what happened before the first one.
        var beforeInjection = desktop
            .Probe.Events()
            .TakeWhile(probeEvent =>
                probeEvent
                    is not KeyMessageEvent { ExtraInfo: TestKeyboardInjector.ExtraInfoMarker }
            )
            .ToList();
        beforeInjection
            .OfType<ActivateEvent>()
            .ShouldContain(activation => activation.State != ActivationState.Inactive);
        beforeInjection.OfType<FocusEvent>().ShouldContain(focus => focus.IsGained);
        beforeInjection
            .OfType<KeyMessageEvent>()
            .ShouldBeEmpty("Bringing the probe forward must not involve key input.");
        beforeInjection.OfType<ProbeReadyEvent>().ShouldHaveSingleItem();
    }

    [DesktopFact]
    public async Task Pings_are_answered_in_order()
    {
        await desktop.PrepareAsync();
        var cursor = desktop.Probe.Cursor;

        await desktop.Probe.PingAsync(
            DesktopProbeFixture.EventTimeout,
            TestContext.Current.CancellationToken
        );
        await desktop.Probe.PingAsync(
            DesktopProbeFixture.EventTimeout,
            TestContext.Current.CancellationToken
        );

        var pongs = desktop.Probe.EventsSince(cursor).OfType<ProbePongEvent>().ToList();
        pongs.Count.ShouldBe(2);
        pongs[1].Id.ShouldNotBeNull().ShouldBeGreaterThan(pongs[0].Id.ShouldNotBeNull());
        pongs[1].Sequence.ShouldBeGreaterThan(pongs[0].Sequence);
    }
}
