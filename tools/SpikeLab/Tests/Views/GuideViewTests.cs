using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
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

            ((IInvokeProvider)Peer(tiles[0]).GetPattern(PatternInterface.Invoke)).Invoke();
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
