using System.Globalization;
using System.Windows;
using Clicalo.Application.Ports;
using Clicalo.Domain.Geometry;
using Clicalo.TestKit.Windows.Rendering;
using Clicalo.Tools.SpikeLab.Scripting;
using Clicalo.Tools.SpikeLab.Session;
using Clicalo.Tools.SpikeLab.Surfaces;

namespace Clicalo.Tools.SpikeLab.Tests.Views;

/// <summary>
/// Where the surfaces start, from the sizes the real surfaces measure: inside the work area, outside the free zone of
/// the app under test, and without covering each other among the surfaces a script shows together. The maintainer's
/// screen is 2400 × 1600 at 175 % (work area 2400 × 1516 physical pixels).
/// </summary>
public sealed class LabLayoutTests
{
    /// <summary>
    /// The bubble window is larger than its tile (measured on the maintainer's screen: from 70 × 95 to 132 × 95
    /// logical pixels); the layout is checked with the larger one.
    /// </summary>
    private static readonly Size BubbleWindow = new(133, 96);

    public static TheoryData<double, double> Screens =>
        new()
        {
            { 2400 / 1.75, 1516 / 1.75 },
            { 1920, 1040 },
            { 2560 / 1.5, 1392 / 1.5 },
        };

    [Theory]
    [MemberData(nameof(Screens))]
    public void Every_surface_starts_inside_the_work_area_and_outside_the_free_zone(
        double width,
        double height
    ) =>
        WpfThread.Invoke(() =>
        {
            var area = new Rect(0, 0, width, height);
            var layout = LabLayout.Arrange(area, Sizes(area));
            var zone = LabLayout.TargetZone(area);

            foreach (var (id, rect) in layout)
            {
                area.Contains(rect).ShouldBeTrue(id + " at " + rect + " leaves " + area);
                rect.IntersectsWith(zone)
                    .ShouldBeFalse(id + " at " + rect + " covers the free zone " + zone);
            }
        });

    [Theory]
    [MemberData(nameof(Screens))]
    public void The_surfaces_shown_together_never_cover_each_other(double width, double height) =>
        WpfThread.Invoke(() =>
        {
            var area = new Rect(0, 0, width, height);
            var layout = LabLayout.Arrange(area, Sizes(area));
            SurfaceId[][] together =
            [
                // S1 and S3: the surfaces under test, the side windows and the strip.
                [
                    LabSurfaceIds.Guide,
                    LabSurfaceIds.Panel,
                    LabSurfaceIds.Dock,
                    LabSurfaceIds.DockSide,
                    LabSurfaceIds.Bubble,
                    LabSurfaceIds.Profiles,
                ],
                // S4: the search instead of the side windows.
                [
                    LabSurfaceIds.Guide,
                    LabSurfaceIds.Panel,
                    LabSurfaceIds.Dock,
                    LabSurfaceIds.Bubble,
                    LabSurfaceIds.Search,
                ],
            ];

            foreach (var group in together)
            {
                foreach (var first in group)
                {
                    foreach (var second in group.Where(id => id != first))
                    {
                        var overlap = Rect.Intersect(layout[first], layout[second]);
                        (overlap.IsEmpty || overlap.Width * overlap.Height < 1).ShouldBeTrue(
                            first + " covers " + second
                        );
                    }
                }
            }
        });

    [Fact]
    public void The_strip_fills_the_bottom_next_to_the_panel_within_its_limits()
    {
        var panel = new Size(506, 364);

        LabLayout
            .StripWidth(new Rect(0, 0, 2400 / 1.75, 1516 / 1.75), panel)
            .ShouldBe(LabLayout.StripPreferredWidth);
        LabLayout
            .StripWidth(new Rect(0, 0, 1024, 700), panel)
            .ShouldBe(LabLayout.StripMinimumWidth);
        LabLayout.StripWidth(new Rect(0, 0, 400, 700), panel).ShouldBe(400 - (2 * LabLayout.Gap));
    }

    [Fact]
    public void Keeping_a_surface_inside_moves_it_and_never_resizes_it()
    {
        var area = new PhysicalRect(0, 0, 2400, 1516);

        LabLayout
            .Inside(new PhysicalRect(1511, 907, 865, 637), area)
            .ShouldBe(new PhysicalRect(1511, 879, 865, 637));
        LabLayout
            .Inside(new PhysicalRect(2232, 233, 242, 544), area)
            .ShouldBe(new PhysicalRect(2158, 233, 242, 544));
        LabLayout
            .Inside(new PhysicalRect(-50, -20, 300, 200), area)
            .ShouldBe(new PhysicalRect(0, 0, 300, 200));
        LabLayout
            .Inside(new PhysicalRect(308, 308, 1820, 1700), area)
            .ShouldBe(new PhysicalRect(308, 0, 1820, 1700), "too tall: its top goes to the top");
        LabLayout
            .Inside(new Rect(10, 10, 20, 20), new Rect(0, 0, 100, 100))
            .ShouldBe(new Rect(10, 10, 20, 20));
    }

    [Fact]
    public void The_strip_keeps_its_bottom_edge_while_it_grows_and_folds()
    {
        // The maintainer's screen (S3, measured with UI Automation): the strip starts 571 px tall, 853 with the whole
        // instruction and 336 folded. WPF resizes it keeping its top edge; the strip then puts its bottom edge back.
        var area = new PhysicalRect(0, 0, 2400, 1516);
        const int Bottom = 1516 - 14;
        var strip = new PhysicalRect(28, Bottom - 571, 1334, 571);

        foreach (var height in (ReadOnlySpan<int>)[853, 336, 571, 853])
        {
            var resized = strip with { Height = height };
            strip = LabLayout.KeepInside(resized, area, Bottom);

            strip.Bottom.ShouldBe(
                Bottom,
                height.ToString(CultureInfo.InvariantCulture) + " px tall"
            );
            strip.Left.ShouldBe(28);
            strip.Height.ShouldBe(height, "it is moved, never resized");
        }

        LabLayout
            .KeepInside(strip with { Height = 1600 }, area, Bottom)
            .Top.ShouldBe(0, "taller than the work area: its top goes to the top");
        LabLayout
            .KeepInside(new PhysicalRect(28, 14, 1334, 853), area, bottom: null)
            .ShouldBe(
                new PhysicalRect(28, 14, 1334, 853),
                "in the upper half it keeps its top edge"
            );
    }

    [Theory]
    [MemberData(nameof(Screens))]
    public void The_strip_is_never_taller_than_the_work_area(double width, double height)
    {
        var area = new Rect(0, 0, width, height);

        var limit = LabLayout.StripMaximumHeight(area.Height);

        limit.ShouldBe(area.Height - (2 * LabLayout.StripBottomGap));
        LabLayout
            .Inside(
                new Rect(LabLayout.Gap, area.Bottom - LabLayout.StripBottomGap - limit, 760, limit),
                area
            )
            .Bottom.ShouldBeLessThanOrEqualTo(area.Bottom);
    }

    [Fact]
    public void The_lower_half_decides_which_edge_the_strip_keeps()
    {
        var area = new PhysicalRect(0, 0, 2400, 1516);

        LabLayout.IsInLowerHalf(new PhysicalRect(0, 900, 1300, 600), area).ShouldBeTrue();
        LabLayout.IsInLowerHalf(new PhysicalRect(0, 14, 1300, 600), area).ShouldBeFalse();
    }

    private static Dictionary<SurfaceId, Size> Sizes(Rect area)
    {
        var surfaces = SpikeLabNames.Surfaces();
        surfaces.Guide.View.Width = LabLayout.StripWidth(area, surfaces.Panel.MeasureSize());
        var longest = SpikeScripts
            .All.SelectMany(script => script.Steps)
            .MaxBy(step => step.Instruction.Length)!;
        surfaces.Guide.View.Update(
            new GuideModel(
                "S1 · paso 32 de 32 (fila 32)",
                longest.Title,
                longest.Instruction,
                "Repeticiones: 20 de 20 · correctas: 20 · fallos: 0 · comprobación final: pendiente",
                IsGreen: true,
                "Todo bien. Haz la comprobación final y toca «Funcionó».",
                [
                    "Primer plano: notepad · Enviar teclas: no · piezas que no arrancaron: 2 (ver la ventana de control)",
                    "Cambios de primer plano: 0 · activaciones de superficies (WM_ACTIVATE): 0 · reg01.violations: 0",
                    "Última orden: Negrita (Ctrl+B) por toque (dedo) · sin enviar · latencia: 1,2 ms",
                    "Concesión: TextInput concedida (paso 2, 120 ms) · origen UiaInvoke · devolución: Restaurado al reintentar",
                    "La sonda recibió: F24 = 0, caracteres = 0, menú = 0",
                ],
                "Toque ignorado por el filtro (Debounce): no cuenta.",
                "Forzar activación del panel"
            )
        );

        var sizes = surfaces.All.ToDictionary(
            surface => surface.Id,
            surface => surface.MeasureSize()
        );
        var bubble = sizes[LabSurfaceIds.Bubble];
        sizes[LabSurfaceIds.Bubble] = new Size(
            Math.Max(bubble.Width, BubbleWindow.Width),
            Math.Max(bubble.Height, BubbleWindow.Height)
        );
        return sizes;
    }
}
