using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Threading;
using Clicalo.TestKit.Windows.Rendering;
using Clicalo.Tools.SpikeLab.Scripting;
using Clicalo.Tools.SpikeLab.Session;
using Clicalo.Tools.SpikeLab.Surfaces;
using Clicalo.Tools.SpikeLab.Views;
using Clicalo.UI.Wpf.Automation;

namespace Clicalo.Tools.SpikeLab.Tests.Views;

/// <summary>
/// The guide strip in process (no window is shown): big named buttons that voice can invoke, the step in words and
/// the state never by color alone.
/// </summary>
public sealed class GuideViewTests
{
    [Fact]
    public void The_buttons_are_named_tiles_of_at_least_44_px_that_voice_can_invoke() =>
        WpfThread.Invoke(() =>
        {
            var commands = new List<(string Tile, CommandPattern Pattern)>();
            var view = new GuideView(
                (tile, width, height) =>
                {
                    var control = TileFactory.Create(tile, width, height);
                    TileFactory.WireAutomation(
                        control,
                        (pattern, _) => commands.Add((tile.Id, pattern))
                    );
                    return control;
                }
            );
            view.Update(Model(stepAction: "Forzar activación del panel"));
            Layout(view);

            var tiles = Tiles(view);
            tiles
                .Select(tile => Peer(tile).GetName())
                .ShouldBe([
                    "Ver instrucción completa",
                    "Plegar la tira",
                    "Mover la tira",
                    "Funcionó",
                    "Falló",
                    "Repetir",
                    "Siguiente",
                    "Forzar activación del panel",
                    "Soltar todo ya",
                ]);
            tiles.ShouldAllBe(tile => tile.ActualWidth >= 44 && tile.ActualHeight >= 44);
            tiles.ShouldAllBe(tile =>
                Peer(tile).GetAutomationControlType() == AutomationControlType.Button
            );

            var worked = tiles.Single(tile =>
                string.Equals(tile.AccessibleName, "Funcionó", StringComparison.Ordinal)
            );
            ((IInvokeProvider)Peer(worked).GetPattern(PatternInterface.Invoke)).Invoke();

            // Invoke returns at once and the tile raises Invoked on the next dispatcher turn (UI Automation, S3).
            Dispatcher.CurrentDispatcher.Invoke(static () => { }, DispatcherPriority.Background);
            commands.ShouldBe([("guide-worked", CommandPattern.Invoke)]);
        });

    [Fact]
    public void The_step_action_button_is_hidden_when_the_step_has_none() =>
        WpfThread.Invoke(() =>
        {
            var view = new GuideView(
                (tile, width, height) => TileFactory.Create(tile, width, height)
            );

            view.Update(Model(stepAction: null));
            Layout(view);

            Tiles(view)
                .Single(tile =>
                    string.Equals(tile.AccessibleName, "Acción del paso", StringComparison.Ordinal)
                )
                .Visibility.ShouldBe(Visibility.Collapsed);
        });

    [Fact]
    public void The_state_is_in_words_and_the_measurements_are_shown() =>
        WpfThread.Invoke(() =>
        {
            var view = new GuideView(
                (tile, width, height) => TileFactory.Create(tile, width, height)
            );

            view.Update(
                Model(stepAction: null) with
                {
                    IsGreen = false,
                    StatusWord = "Algo cambió: reg01.violations subió en 1.",
                }
            );
            Layout(view);

            var texts = Texts(view);
            texts.ShouldContain(
                "Algo cambió: reg01.violations subió en 1.",
                StringComparer.Ordinal
            );
            texts.ShouldContain("Primer plano: notepad", StringComparer.Ordinal);
            texts.ShouldContain("Toca el panel 20 veces.", StringComparer.Ordinal);
            view.Notice.Text.ShouldBe("Toque ignorado por el filtro.");
        });

    [Fact]
    public void Folding_leaves_the_step_the_state_the_notice_and_the_buttons() =>
        WpfThread.Invoke(() =>
        {
            var view = new GuideView(
                (tile, width, height) => TileFactory.Create(tile, width, height)
            );
            view.Update(Model(stepAction: null) with { Instruction = LongestInstruction() });
            Layout(view);
            var compact = view.DesiredSize.Height;

            view.IsFolded = true;
            Layout(view);

            view.DesiredSize.Height.ShouldBeLessThan(compact - 100);
            var names = Tiles(view)
                .Where(tile => tile.Visibility == Visibility.Visible)
                .Select(tile => tile.AccessibleName)
                .ToArray();
            names.ShouldContain(GuideView.UnfoldName, StringComparer.Ordinal);
            names.ShouldContain("Funcionó", StringComparer.Ordinal);
            names.ShouldNotContain(name =>
                string.Equals(name, "Ver instrucción completa", StringComparison.Ordinal)
            );
            view.Notice.Visibility.ShouldBe(Visibility.Visible, "the live region stays");
            Texts(view).ShouldContain("Panel · dedo · Word", StringComparer.Ordinal);

            view.IsFolded = false;
            Layout(view);

            view.DesiredSize.Height.ShouldBe(compact, tolerance: 0.5);
        });

    [Fact]
    public void The_instruction_takes_three_lines_until_the_whole_one_is_asked_for() =>
        WpfThread.Invoke(() =>
        {
            var view = new GuideView(
                (tile, width, height) => TileFactory.Create(tile, width, height)
            );
            view.Update(Model(stepAction: null) with { Instruction = LongestInstruction() });
            Layout(view);
            var compact = view.DesiredSize.Height;

            view.ShowsFullInstruction = true;
            Layout(view);

            view.DesiredSize.Height.ShouldBeGreaterThan(compact);
            Tiles(view).ShouldContain(tile => tile.AccessibleName == GuideView.ShortenName);
        });

    [Fact]
    public void The_compact_strip_with_the_longest_step_fits_below_the_free_zone_of_the_maintainers_screen() =>
        WpfThread.Invoke(() =>
        {
            // 2400 × 1516 physical pixels of work area at 175 %, in logical pixels.
            var area = new Rect(0, 0, 2400 / 1.75, 1516 / 1.75);
            var panel = SpikeLabNames.Surfaces().Panel.MeasureSize();
            var view = new GuideView(
                (tile, width, height) => TileFactory.Create(tile, width, height)
            )
            {
                Width = LabLayout.StripWidth(area, panel),
            };
            view.Update(
                Model(stepAction: "Forzar activación del panel") with
                {
                    Instruction = LongestInstruction(),
                    Measurements =
                    [
                        "Primer plano: notepad · Enviar teclas: no · piezas que no arrancaron: 2 (ver la ventana de control)",
                        "Cambios de primer plano: 0 · activaciones de superficies (WM_ACTIVATE): 0 · reg01.violations: 0",
                        "Última orden: Negrita (Ctrl+B) por toque (dedo) · sin enviar · latencia: 1,2 ms",
                        "Concesión: TextInput concedida (paso 2, 120 ms) · origen UiaInvoke · devolución: Restaurado al reintentar",
                        "La sonda recibió: F24 = 0, caracteres = 0, menú = 0",
                    ],
                }
            );
            Layout(view);

            // The window adds a border of 1 on each side.
            var height = view.DesiredSize.Height + 2;
            height.ShouldBeLessThanOrEqualTo(
                (area.Height / 2) - LabLayout.StripBottomGap,
                "the strip starts at the bottom and must stay below the top-left quarter"
            );
            (
                view.DesiredSize.Width + 2 + panel.Width + (3 * LabLayout.Gap)
            ).ShouldBeLessThanOrEqualTo(area.Width + 0.5);
        });

    [Fact]
    public void At_its_maximum_height_the_whole_instruction_gives_up_lines_and_the_buttons_stay() =>
        WpfThread.Invoke(() =>
        {
            var view = new GuideView(
                (tile, width, height) => TileFactory.Create(tile, width, height)
            );
            view.Update(
                Model(stepAction: "Forzar activación del panel") with
                {
                    Instruction = string.Join(" ", Enumerable.Repeat(LongestInstruction(), 4)),
                }
            );
            view.ShowsFullInstruction = true;
            Layout(view);
            var unlimited = view.DesiredSize.Height;

            // A strip limited to less than it wants, as the window's MaxHeight does on a short work area.
            var limit = Math.Round(unlimited * 0.6);
            view.Measure(new Size(LabLayout.StripPreferredWidth, limit));
            view.Arrange(new Rect(0, 0, LabLayout.StripPreferredWidth, limit));
            view.UpdateLayout();

            view.RenderSize.Height.ShouldBeLessThanOrEqualTo(limit);
            foreach (var tile in Tiles(view).Where(tile => tile.Visibility == Visibility.Visible))
            {
                var bottom = tile.TranslatePoint(new Point(0, tile.ActualHeight), view).Y;
                bottom.ShouldBeLessThanOrEqualTo(
                    limit + 0.5,
                    $"«{tile.AccessibleName}» must stay inside the strip"
                );
            }
        });

    [Fact]
    public void The_handles_are_named_thumbs_of_at_least_44_px_without_their_glyph() =>
        WpfThread.Invoke(() =>
        {
            var view = new GuideView(
                (tile, width, height) => TileFactory.Create(tile, width, height)
            );
            var panel = SpikeLabNames.Surfaces().Panel;

            foreach (
                var (grip, name) in (ReadOnlySpan<(DragGrip, string)>)
                    [(view.Grip, "Asa de la tira-guía"), (panel.Grip, "Asa del panel")]
            )
            {
                var peer = UIElementAutomationPeer.CreatePeerForElement(grip);
                peer.GetAutomationControlType().ShouldBe(AutomationControlType.Thumb);
                peer.GetName().ShouldBe(name);
                peer.GetChildren().ShouldBeNull();
                grip.Width.ShouldBeGreaterThanOrEqualTo(DragGrip.MinimumSide);
                grip.MinHeight.ShouldBeGreaterThanOrEqualTo(DragGrip.MinimumSide);
            }
        });

    private static string LongestInstruction() =>
        SpikeScripts
            .All.SelectMany(script => script.Steps)
            .Select(step => step.Instruction)
            .MaxBy(instruction => instruction.Length)!;

    private static GuideModel Model(string? stepAction) =>
        new(
            "S1 · paso 1 de 32 (fila 1)",
            "Panel · dedo · Word",
            "Toca el panel 20 veces.",
            "Repeticiones: 0 de 20",
            IsGreen: true,
            "Todo bien.",
            ["Primer plano: notepad", "Cambios de primer plano: 0"],
            "Toque ignorado por el filtro.",
            stepAction
        );

    private static void Layout(FrameworkElement element)
    {
        element.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        element.Arrange(new Rect(element.DesiredSize));
        element.UpdateLayout();
    }

    private static List<ShortcutTile> Tiles(DependencyObject root) =>
        [.. Descendants(root).OfType<ShortcutTile>()];

    private static List<string> Texts(DependencyObject root) =>
        [
            .. Descendants(root)
                .OfType<System.Windows.Controls.TextBlock>()
                .Select(text => text.Text),
        ];

    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        foreach (var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())
        {
            yield return child;
            foreach (var descendant in Descendants(child))
            {
                yield return descendant;
            }
        }
    }

    private static AutomationPeer Peer(ShortcutTile tile) =>
        UIElementAutomationPeer.CreatePeerForElement(tile);
}
