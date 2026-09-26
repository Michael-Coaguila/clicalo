using System.Globalization;
using Clicalo.TestKit.Windows;
using Clicalo.TestKit.Windows.Rendering;
using Clicalo.Windowing.IntegrationTests.Automation.Lab;
using Clicalo.Windowing.IntegrationTests.Automation.Rules;
using Clicalo.Windowing.IntegrationTests.Desktop;

namespace Clicalo.Windowing.IntegrationTests.Automation.OutOfProcess;

/// <summary>
/// S3 · UIA009 with the UI Automation client in ANOTHER process, as Voice access and Narrator are
/// (<see cref="UiaClientRunner"/>): with InputProbe in front, 20 invokes, 20 toggles and 20 expand and collapse pairs
/// through the UIA3 client raise the tile's events on its UI thread and never change the foreground, never deactivate
/// InputProbe and never activate the surface (<see cref="ForegroundInvariant"/>); and 20 invokes from a UIA3 client
/// whose own window is in front never activate the surface either.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="UiaInvokeTests"/> runs the same checks with the client in this process, on another thread.
/// </para>
/// <para>
/// The managed client of .NET (<c>System.Windows.Automation</c>) DOES activate the surface: in the first desktop run of
/// this class (2026-09-26, the maintainer's machine), each of its 80 calls with InputProbe in front, and each of its 20
/// invokes with its own window in front, took the foreground to the surface and counted one <c>reg01.violations</c>,
/// while the UIA3 client never did. <see cref="The_managed_UIA2_client_never_changes_the_foreground"/> reproduces it;
/// it is explicit (<c>--explicit on</c>) until the cause is found, and it is a risk for the Windows 10 rows of S3 and S4
/// (Windows Speech Recognition may be such a client).
/// </para>
/// </remarks>
[Collection(DesktopCollectionDefinition.Name)]
[Trait("Requires", "Desktop")]
[Trait("Req", "REG-01")]
[Trait("Req", "REG-06")]
public sealed class OutOfProcessUiaTests(UiaSurfaceFixture surface)
    : IClassFixture<UiaSurfaceFixture>
{
    private const int Cycles = 20;

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private static int WpfThreadId => WpfThread.Dispatcher.Thread.ManagedThreadId;

    [DesktopFact]
    [Trait("Req", "ACC-001")]
    [Trait("Req", "ACC-004")]
    public Task Invoke_Toggle_and_ExpandCollapse_from_another_process_never_change_the_foreground() =>
        EveryPatternFromAnotherProcessAsync(UiaClientKind.Uia3);

    [DesktopFact]
    [Trait("Req", "ACC-004")]
    public Task A_UIA3_client_in_front_that_invokes_a_tile_never_activates_the_surface() =>
        InvokesFromAClientInFrontAsync(UiaClientKind.Uia3);

    /// <summary>
    /// The risk of the S3 finding with the managed client, with InputProbe in front (<paramref name="clientInFront"/>
    /// false) or with the client's own window in front. Fails while the managed client activates the surface.
    /// </summary>
    [DesktopTheory(Explicit = true)]
    [InlineData(false)]
    [InlineData(true)]
    public Task The_managed_UIA2_client_never_changes_the_foreground(bool clientInFront) =>
        clientInFront
            ? InvokesFromAClientInFrontAsync(UiaClientKind.Uia2)
            : EveryPatternFromAnotherProcessAsync(UiaClientKind.Uia2);

    private static string Cycle(string action, LabTileSpec spec, int cycle) =>
        string.Create(CultureInfo.InvariantCulture, $"{action} «{spec.Name}» #{cycle + 1}");

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(Cancellation);
        deadline.CancelAfter(UiaSurfaceFixture.EventTimeout);
        while (!condition())
        {
            try
            {
                await Task.Delay(TimeSpan.FromMilliseconds(10), deadline.Token);
            }
            catch (OperationCanceledException) when (!Cancellation.IsCancellationRequested)
            {
                condition()
                    .ShouldBeTrue(
                        "The client's window never came to the front: "
                            + ForegroundWindows.Describe()
                    );
                return;
            }
        }
    }

    private async Task EveryPatternFromAnotherProcessAsync(UiaClientKind kind)
    {
        WpfThread.Invoke(surface.Lab.Reset);
        await using var client = await UiaClientProcess.StartAsync(
            kind,
            surface.SurfaceHandle,
            inFront: false,
            Cancellation
        );
        var violations = new List<UiaViolation>();
        var invokable = LabTiles.Invokable.ToList();
        LabTileSpec[] toggles = [LabTiles.Get("tile.shift"), LabTiles.Get("tile.holdCtrl")];
        var profile = LabTiles.Get("tile.profile");

        for (var cycle = 0; cycle < Cycles; cycle++)
        {
            violations.AddRange(
                await CallAsync(
                    client,
                    UiaClientProcess.Invoke,
                    invokable[cycle % invokable.Count],
                    LabInvocationKind.Invoked,
                    cycle
                )
            );
            violations.AddRange(
                await CallAsync(
                    client,
                    UiaClientProcess.Toggle,
                    toggles[cycle % toggles.Length],
                    LabInvocationKind.Toggled,
                    cycle
                )
            );
            violations.AddRange(
                await CallAsync(
                    client,
                    UiaClientProcess.Expand,
                    profile,
                    LabInvocationKind.ExpandRequested,
                    cycle
                )
            );
            violations.AddRange(
                await CallAsync(
                    client,
                    UiaClientProcess.Collapse,
                    profile,
                    LabInvocationKind.CollapseRequested,
                    cycle
                )
            );
        }

        violations.ShouldBeEmpty();
    }

    /// <summary>
    /// The case of the S3 finding: the client process owns the foreground (a window of its own, put in front by the
    /// probe) when it invokes a tile.
    /// </summary>
    private async Task InvokesFromAClientInFrontAsync(UiaClientKind kind)
    {
        WpfThread.Invoke(surface.Lab.Reset);
        await using var client = await UiaClientProcess.StartAsync(
            kind,
            surface.SurfaceHandle,
            inFront: true,
            Cancellation
        );
        client.OwnWindow.ShouldNotBe(0);
        var violations = new List<UiaViolation>();
        var invokable = LabTiles.Invokable.ToList();

        for (var cycle = 0; cycle < Cycles; cycle++)
        {
            var spec = invokable[cycle % invokable.Count];
            await surface.PrepareAsync();
            (
                await surface.Probe.RequestForegroundAsync(
                    client.OwnWindow,
                    UiaSurfaceFixture.EventTimeout,
                    Cancellation
                )
            ).Succeeded.ShouldBeTrue("the probe could not put the client's window in front");
            await WaitUntilAsync(() => ForegroundWindows.IsForeground(client.OwnWindow));
            var guard = surface.Guard.Violations;
            var reported = surface.Arbiter.Violations.Count;
            var log = surface.Lab.Log.Count;

            await client.RunAsync(UiaClientProcess.Invoke, spec.Id, Cancellation);

            (await WaitForTileEventAsync(log)).ShouldBe(
                new LabInvocation(spec.Id, LabInvocationKind.Invoked, WpfThreadId)
            );

            // An activation caused by the call reaches the surface's thread after the event: let it arrive.
            await Task.Delay(TimeSpan.FromMilliseconds(100), Cancellation);
            violations.AddRange(
                ForegroundInvariant.Check(
                    Cycle(UiaClientProcess.Invoke, spec, cycle),
                    client.OwnWindow,
                    surface.SurfaceHandle,
                    WpfThread.Invoke(() => surface.Surface.IsActive),
                    [],
                    surface.Guard.Violations - guard,
                    surface.Arbiter.Violations.Count - reported
                )
            );
        }

        violations.ShouldBeEmpty();
    }

    /// <summary>
    /// One pattern call from the client with InputProbe in front, and UIA009 for it alone: the violations counted
    /// before it (another test of this class) do not count.
    /// </summary>
    private async Task<IReadOnlyList<UiaViolation>> CallAsync(
        UiaClientProcess client,
        string operation,
        LabTileSpec spec,
        LabInvocationKind expected,
        int cycle
    )
    {
        var cursor = await surface.PrepareAsync();
        var guard = surface.Guard.Violations;
        var reported = surface.Arbiter.Violations.Count;
        var log = surface.Lab.Log.Count;

        await client.RunAsync(operation, spec.Id, Cancellation);

        (await WaitForTileEventAsync(log)).ShouldBe(
            new LabInvocation(spec.Id, expected, WpfThreadId)
        );
        await surface.Probe.PingAsync(UiaSurfaceFixture.EventTimeout, Cancellation);
        return ForegroundInvariant.Check(
            Cycle(operation, spec, cycle),
            surface.Probe.Window,
            surface.SurfaceHandle,
            WpfThread.Invoke(() => surface.Surface.IsActive),
            surface.Probe.EventsSince(cursor),
            surface.Guard.Violations - guard,
            surface.Arbiter.Violations.Count - reported
        );
    }

    private async Task<LabInvocation> WaitForTileEventAsync(int cursor)
    {
        var events = await surface.Lab.Log.WaitForAsync(
            cursor,
            1,
            UiaSurfaceFixture.EventTimeout,
            Cancellation
        );
        return events.ShouldHaveSingleItem();
    }
}
