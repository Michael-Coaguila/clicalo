using System.Globalization;
using Clicalo.TestKit.Windows.Rendering;
using Clicalo.Windowing.IntegrationTests.Automation.Lab;
using Clicalo.Windowing.IntegrationTests.Automation.Rules;
using Clicalo.Windowing.IntegrationTests.Desktop;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;

namespace Clicalo.Windowing.IntegrationTests.Automation;

/// <summary>
/// S3 · UIA009: invoking, toggling, expanding and collapsing a tile through UI Automation, with InputProbe in the
/// foreground, raises the tile's event on its UI thread and never changes the foreground, never deactivates
/// InputProbe and never activates the surface (<c>reg01.violations</c> = 0). 20 cycles of each.
/// </summary>
[Collection(DesktopCollectionDefinition.Name)]
[Trait("Requires", "Desktop")]
[Trait("Req", "REG-01")]
[Trait("Req", "REG-06")]
public sealed class UiaInvokeTests(UiaSurfaceFixture surface) : IClassFixture<UiaSurfaceFixture>
{
    private const int Cycles = 20;

    [DesktopFact]
    [Trait("Req", "ACC-004")]
    public async Task Invoke_by_UIA_never_changes_the_foreground()
    {
        WpfThread.Invoke(surface.Lab.Reset);
        var tiles = LabTiles.Invokable.ToList();
        var violations = new List<UiaViolation>();
        for (var cycle = 0; cycle < Cycles; cycle++)
        {
            var spec = tiles[cycle % tiles.Count];
            var element = surface.Element(spec.Id);
            var cursor = await surface.PrepareAsync();
            var log = surface.Lab.Log.Count;

            element.Patterns.Invoke.Pattern.Invoke();

            (await WaitForTileEventAsync(log)).ShouldBe(
                new LabInvocation(spec.Id, LabInvocationKind.Invoked, WpfThreadId)
            );
            violations.AddRange(
                await surface.ForegroundViolationsAsync(cursor, Cycle("Invoke", spec, cycle))
            );
        }

        violations.ShouldBeEmpty();
    }

    [DesktopFact]
    [Trait("Req", "ACC-001")]
    [Trait("Req", "ACC-003")]
    public async Task Toggle_and_ExpandCollapse_never_change_the_foreground()
    {
        WpfThread.Invoke(surface.Lab.Reset);
        var violations = new List<UiaViolation>();
        foreach (var spec in new[] { LabTiles.Get("tile.shift"), LabTiles.Get("tile.holdCtrl") })
        {
            var element = surface.Element(spec.Id);
            var expected = ToggleState.Off;
            for (var cycle = 0; cycle < Cycles; cycle++)
            {
                var cursor = await surface.PrepareAsync();
                var log = surface.Lab.Log.Count;

                element.Patterns.Toggle.Pattern.Toggle();

                expected = LabExpectations.Next(expected, spec.ThreeStates);
                (await WaitForTileEventAsync(log)).Kind.ShouldBe(LabInvocationKind.Toggled);
                element.Patterns.Toggle.Pattern.ToggleState.Value.ShouldBe(expected);
                element.ItemStatus.ShouldBe(LabExpectations.StateText(expected));
                violations.AddRange(
                    await surface.ForegroundViolationsAsync(cursor, Cycle("Toggle", spec, cycle))
                );
            }
        }

        var profile = LabTiles.Get("tile.profile");
        var expandable = surface.Element(profile.Id);
        for (var cycle = 0; cycle < Cycles; cycle++)
        {
            violations.AddRange(
                await ExpandCollapseAsync(expandable, profile, cycle, expand: true)
            );
            violations.AddRange(
                await ExpandCollapseAsync(expandable, profile, cycle, expand: false)
            );
        }

        violations.ShouldBeEmpty();
    }

    private static int WpfThreadId => WpfThread.Dispatcher.Thread.ManagedThreadId;

    private static string Cycle(string action, LabTileSpec spec, int cycle) =>
        string.Create(CultureInfo.InvariantCulture, $"{action} «{spec.Name}» #{cycle + 1}");

    private async Task<IReadOnlyList<UiaViolation>> ExpandCollapseAsync(
        AutomationElement element,
        LabTileSpec spec,
        int cycle,
        bool expand
    )
    {
        var cursor = await surface.PrepareAsync();
        var log = surface.Lab.Log.Count;
        var pattern = element.Patterns.ExpandCollapse.Pattern;
        if (expand)
        {
            pattern.Expand();
        }
        else
        {
            pattern.Collapse();
        }

        (await WaitForTileEventAsync(log)).Kind.ShouldBe(
            expand ? LabInvocationKind.ExpandRequested : LabInvocationKind.CollapseRequested
        );
        pattern.ExpandCollapseState.Value.ShouldBe(
            expand ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed
        );
        return await surface.ForegroundViolationsAsync(
            cursor,
            Cycle(expand ? "Expand" : "Collapse", spec, cycle)
        );
    }

    private async Task<LabInvocation> WaitForTileEventAsync(int cursor)
    {
        var events = await surface.Lab.Log.WaitForAsync(
            cursor,
            1,
            UiaSurfaceFixture.EventTimeout,
            TestContext.Current.CancellationToken
        );
        return events.ShouldHaveSingleItem();
    }
}
