using System.Globalization;
using System.Windows.Automation.Peers;
using Clicalo.TestKit.Windows.Rendering;
using Clicalo.Windowing.IntegrationTests.Automation.Lab;
using Clicalo.Windowing.IntegrationTests.Automation.Rules;
using Clicalo.Windowing.IntegrationTests.Desktop;
using ControlType = FlaUI.Core.Definitions.ControlType;
using TreeScope = FlaUI.Core.Definitions.TreeScope;

namespace Clicalo.Windowing.IntegrationTests.Automation;

/// <summary>
/// S3 · the UI Automation tree of a real <c>NonActivatingWindow</c> with nine tiles, read by a UIA client (FlaUI
/// UIA3) as Voice access reads it: the surface is a Window, each tile a Button with its localized name, pattern and
/// state, and turning «Numbers for voice» on and off changes the names and tells the clients (UIA001–UIA006,
/// UIA008, UIA010).
/// </summary>
[Collection(DesktopCollectionDefinition.Name)]
[Trait("Requires", "Desktop")]
[Trait("Req", "REG-06")]
public sealed class UiaTreeTests(UiaSurfaceFixture surface) : IClassFixture<UiaSurfaceFixture>
{
    [DesktopFact]
    [Trait("Req", "ACC-001")]
    [Trait("Req", "ACC-009")]
    [Trait("Req", "REG-02")]
    [Trait("Req", "REG-01")]
    public async Task The_surface_and_its_tiles_are_in_the_UIA_tree()
    {
        WpfThread.Invoke(surface.Lab.Reset);
        var cursor = await surface.PrepareAsync();
        var window = surface.Window();

        window.ControlType.ShouldBe(
            ControlType.Window,
            "a surface is a window for UIA, not a pane of another"
        );
        window.Name.ShouldBe(LabSurface.SurfaceTitle);
        UiaVerifier.ShouldPass(
            FlaUiSnapshot.Capture(surface.Automation, window, surface.Scale),
            LabExpectations.Create(voiceNumbers: false)
        );

        WpfThread.Invoke(() => surface.Lab.SetVoiceNumbers(true));
        UiaVerifier.ShouldPass(
            FlaUiSnapshot.Capture(surface.Automation, window, surface.Scale),
            LabExpectations.Create(voiceNumbers: true)
        );

        (await surface.ForegroundViolationsAsync(cursor, "reading the tree")).ShouldBeEmpty();
    }

    [DesktopFact]
    [Trait("Req", "ACC-009")]
    [Trait("Req", "ACC-010")]
    public async Task Voice_numbers_change_the_names_and_notify_the_clients()
    {
        WpfThread.Invoke(surface.Lab.Reset);
        var cursor = await surface.PrepareAsync();
        var names = new EventLog<string>();
        var tiles = LabTiles.All.Select(spec => surface.Element(spec.Id)).ToList();
        var handlers = tiles
            .Select(tile =>
                tile.RegisterPropertyChangedEvent(
                    TreeScope.Element,
                    (_, _, value) => names.Record(value as string ?? string.Empty),
                    surface.Automation.PropertyLibrary.Element.Name
                )
            )
            .ToList();
        try
        {
            await WaitForListenerAsync(AutomationEvents.PropertyChanged);
            var start = names.Count;

            var expected = LabTiles.All.Select((spec, index) => Numbered(index, spec)).ToList();

            WpfThread.Invoke(() => surface.Lab.SetVoiceNumbers(true));

            // An in-process client may get a change twice: wait until every new name has arrived, then accept no
            // other name.
            while (true)
            {
                var seen = names.Count;
                var received = names.Since(start).ToHashSet(StringComparer.Ordinal);
                if (expected.All(received.Contains))
                {
                    break;
                }

                await names.WaitForAsync(
                    seen,
                    1,
                    UiaSurfaceFixture.EventTimeout,
                    TestContext.Current.CancellationToken
                );
            }

            names
                .Since(start)
                .Distinct(StringComparer.Ordinal)
                .ShouldBe(expected, ignoreOrder: true);
            tiles
                .Select(tile => tile.Name)
                .ShouldBe(LabTiles.All.Select((spec, index) => Numbered(index, spec)));

            WpfThread.Invoke(() => surface.Lab.SetVoiceNumbers(false));
            tiles.Select(tile => tile.Name).ShouldBe(LabTiles.All.Select(spec => spec.Name));
        }
        finally
        {
            foreach (var handler in handlers)
            {
                handler.Dispose();
            }
        }

        (await surface.ForegroundViolationsAsync(cursor, "voice numbers")).ShouldBeEmpty();
    }

    private static string Numbered(int index, LabTileSpec spec) =>
        string.Create(CultureInfo.InvariantCulture, $"{index + 1} {spec.Name}");

    /// <summary>UIA registers a client's handler with the provider asynchronously: wait until WPF sees it.</summary>
    internal static async Task WaitForListenerAsync(params AutomationEvents[] events)
    {
        var deadline = DateTime.UtcNow + UiaSurfaceFixture.EventTimeout;
        while (!WpfThread.Invoke(() => events.All(AutomationPeer.ListenerExists)))
        {
            if (DateTime.UtcNow > deadline)
            {
                throw new TimeoutException(
                    "The UI Automation client's event handlers never reached the WPF provider: "
                        + string.Join(", ", events)
                );
            }

            await Task.Delay(50, TestContext.Current.CancellationToken);
        }
    }
}
