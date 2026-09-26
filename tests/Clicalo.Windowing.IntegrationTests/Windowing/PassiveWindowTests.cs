using System.Globalization;
using Clicalo.Application.Ports;
using Clicalo.Domain.Geometry;
using Clicalo.TestKit.Windows;
using Clicalo.TestKit.Windows.Rendering;
using Clicalo.Windowing.IntegrationTests.Desktop;
using Clicalo.Windowing.IntegrationTests.Windowing.Support;

namespace Clicalo.Windowing.IntegrationTests.Windowing;

/// <summary>
/// Spike S1: <c>ShowPassive</c>, <c>HidePassive</c> and <c>MovePassive</c> never activate, never move the focus and
/// leave the surface on top; and the non-activation styles are there before the first show (blueprint §3.5, lesson
/// L-WIN-1 of v1, where they were applied after it).
/// </summary>
[Collection(DesktopCollectionDefinition.Name)]
[Trait("Requires", "Desktop")]
[Trait("Req", "REG-01")]
public sealed class PassiveWindowTests(SurfaceDesktopFixture desktop)
    : IClassFixture<SurfaceDesktopFixture>
{
    private const int Cycles = 20;
    private const int Offset = 8;

    public static TheoryData<SurfaceKind> Kinds { get; } = new(Enum.GetValues<SurfaceKind>());

    [DesktopTheory]
    [InlineData(LabSurface.Panel)]
    [InlineData(LabSurface.TabWithSide)]
    [InlineData(LabSurface.Bubble)]
    public async Task Show_hide_and_move_are_passive(LabSurface surface)
    {
        var windows = desktop.WindowsOf(surface);
        var cursor = await desktop.PrepareAsync();
        var violations = desktop.Lab.Guard.Violations;

        foreach (var window in windows)
        {
            var home = NativeSurface.Bounds(window.Handle);
            for (var cycle = 1; cycle <= Cycles; cycle++)
            {
                WpfThread.Invoke(window.HidePassive);
                NativeSurface.IsWindowVisible(window.Handle).ShouldBeFalse();
                ShouldStillBeInFront(Say($"after HidePassive {cycle} of {window.Id}"), violations);

                WpfThread.Invoke(window.ShowPassive);
                NativeSurface.IsWindowVisible(window.Handle).ShouldBeTrue();
                NativeSurface
                    .HasExStyle(window.Handle, NativeSurface.ExNoActivate | NativeSurface.ExTopmost)
                    .ShouldBeTrue();
                NativeSurface
                    .IsAbove(window.Handle, desktop.Probe.Window)
                    .ShouldBeTrue(Say($"{window.Id} is not above the application in front."));
                ShouldStillBeInFront(Say($"after ShowPassive {cycle} of {window.Id}"), violations);

                var shift = cycle % 2 == 0 ? 0 : Offset;
                var target = new PhysicalRect(
                    home.Left + shift,
                    home.Top + shift,
                    home.Width,
                    home.Height
                );
                WpfThread.Invoke(() => window.MovePassive(target));
                var bounds = NativeSurface.Bounds(window.Handle);
                (bounds.Left, bounds.Top, bounds.Width, bounds.Height).ShouldBe(
                    (target.Left, target.Top, target.Width, target.Height)
                );
                ShouldStillBeInFront(Say($"after MovePassive {cycle} of {window.Id}"), violations);
            }

            window.Activations.ShouldBeEmpty(
                Say($"{window.Id} received activation or focus messages.")
            );
        }

        await desktop.ShouldHaveKeptTheForegroundAsync(cursor);
    }

    [DesktopTheory]
    [MemberData(nameof(Kinds))]
    public async Task The_non_activation_styles_exist_before_the_first_show(SurfaceKind kind)
    {
        var cursor = await desktop.PrepareAsync();
        var (work, dpi) = NativeSurface.PrimaryWorkArea();
        var size = (int)Math.Round(120 * dpi / 96.0);
        var surface = desktop.Lab.CreateSurface(kind, 100, 120, 120);
        try
        {
            WpfThread.Invoke(() =>
            {
                surface.MovePassive(
                    new PhysicalRect(work.CenterX - (size / 2), work.Top + size, size, size)
                );
                surface.ShowPassive();
            });

            var style = surface.StyleAtFirstShow.ShouldNotBeNull(
                "The surface received no WM_SHOWWINDOW."
            );
            (style & (NativeSurface.ExNoActivate | NativeSurface.ExTopmost)).ShouldBe(
                NativeSurface.ExNoActivate | NativeSurface.ExTopmost
            );
            (style & (NativeSurface.ExAppWindow | NativeSurface.ExToolWindow)).ShouldBe(0u);
            surface.OwnerAtFirstShow.ShouldBe(desktop.Lab.Anchor.Window.Handle);
            ForegroundWindows
                .IsForeground(desktop.Probe.Window)
                .ShouldBeTrue(ForegroundWindows.Describe());
        }
        finally
        {
            desktop.Lab.Close(surface);
        }

        surface.Activations.ShouldBeEmpty();
        await desktop.ShouldHaveKeptTheForegroundAsync(cursor);
    }

    private static string Say(FormattableString text) =>
        text.ToString(CultureInfo.InvariantCulture);

    private void ShouldStillBeInFront(string when, long violations)
    {
        ForegroundWindows
            .IsForeground(desktop.Probe.Window)
            .ShouldBeTrue("The foreground changed " + when + ": " + ForegroundWindows.Describe());
        desktop.Lab.Guard.Violations.ShouldBe(
            violations,
            "ActivationGuard saw a surface activated " + when + "."
        );
    }
}
