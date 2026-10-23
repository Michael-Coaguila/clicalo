using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Clicalo.Domain.Frequents;
using Clicalo.Domain.Library;
using Clicalo.Domain.PanelLayout;
using Clicalo.Domain.Primitives;
using Clicalo.Domain.Settings;
using Clicalo.Presentation.Panel;
using Clicalo.Presentation.Panel.Search;
using Clicalo.TestKit.Windows.Rendering;
using Clicalo.UI.Wpf.Automation;
using Clicalo.UI.Wpf.Controls;
using Clicalo.UI.Wpf.Resources;
using Clicalo.UI.Wpf.Surfaces.Panel;
using Clicalo.UI.Wpf.Theming;
using Clicalo.Windowing.IntegrationTests.SearchPanel;
using Clicalo.Windowing.IntegrationTests.Theming;
using static System.FormattableString;

namespace Clicalo.Windowing.IntegrationTests.MinimalPanel;

/// <summary>
/// The short tiles of the Compact view, headless (built and laid out on the WPF thread; never shown): a Frequents tile
/// keeps its profile of origin under its name, as a search result does (FRE-003, BUS-005), the other tiles have no key
/// line (VCO-001), and a voice number centred down a tile sits beside its body whatever its digits, so it never covers
/// the icon or the name (ACC-009).
/// </summary>
public sealed class CompactTileLookTests
{
    private const string AlwaysVisible = "Siempre visible";

    public static TheoryData<PanelSize, bool, int> Shapes()
    {
        var shapes = new TheoryData<PanelSize, bool, int>();
        foreach (var size in new[] { PanelSize.Small, PanelSize.Medium, PanelSize.Large })
        {
            foreach (var numbers in new[] { false, true })
            {
                foreach (
                    var scale in new[] { TypeScale.MinScalePercent, TypeScale.MaxScalePercent }
                )
                {
                    shapes.Add(size, numbers, scale);
                }
            }
        }

        return shapes;
    }

    [Theory]
    [MemberData(nameof(Shapes))]
    [Trait("Req", "FRE-003")]
    [Trait("Req", "BUS-005")]
    [Trait("Req", "VCO-001")]
    [Trait("Req", "ACC-009")]
    public void In_Compact_a_frequent_shows_its_profile_of_origin_under_its_name(
        PanelSize size,
        bool numbers,
        int scale
    ) =>
        WithBody(
            PanelLayoutSettings.Default with
            {
                Size = size,
                Compact = true,
                VoiceNumbers = numbers,
                TextScalePercent = scale,
            },
            scale,
            (body, panel, library) =>
            {
                // A tap of Word, one of the row, the Mantener of the row and the macro of Word: two with a type mark.
                panel.Apply(Frequents(library, "w0", "a0", "a1", "w6"));
                panel.ApplyContext(PanelBodyContext.Idle with { Frequents = true });
                body.UpdateLayout();

                var tiles = body.ShortcutGrid.TileControls;
                tiles
                    .Select(static t => t.Control.Keys)
                    .ShouldBe(["Word", AlwaysVisible, AlwaysVisible, "Word"]);
                tiles.Count(static t => t.Control.TypeSymbol is not null).ShouldBe(2);
                foreach (var tile in tiles.Select(static t => t.Control))
                {
                    foreach (var number in numbers ? new int?[] { 1, 15, 99 } : [null])
                    {
                        tile.VoiceNumber = number;
                        tile.UpdateLayout();
                        ShowsOrigin(tile, number);
                    }
                }
            }
        );

    [Fact]
    [Trait("Req", "BUS-005")]
    [Trait("Req", "FRE-003")]
    public void In_Compact_two_results_with_one_name_are_told_apart_by_their_profile() =>
        WithBody(
            PanelBodyTestData.GallerySearchLibrary(),
            PanelLayoutSettings.Default with
            {
                Size = PanelSize.Small,
                Compact = true,
                VoiceNumbers = true,
            },
            TypeScale.MinScalePercent,
            (body, panel, library) =>
            {
                var search = SearchSurfaceTests.Search();
                search.ApplyLibrary(library);
                _ = search.OpenAsync(SearchTrigger.Touch);
                search.Query = "Reemplazar";
                panel.ApplySearch([.. search.Results], search.NoResultsText);
                panel.ApplyContext(PanelBodyContext.Idle with { SearchingWithText = true });
                body.UpdateLayout();

                var tiles = body.ShortcutGrid.TileControls.Select(static t => t.Control).ToList();
                tiles
                    .Select(static t => (t.AccessibleName, t.Keys))
                    .ShouldBe(
                        [("Reemplazar", "Word"), ("Reemplazar", "Bloc de notas")],
                        ignoreOrder: true
                    );
                foreach (var tile in tiles)
                {
                    ShowsOrigin(tile, tile.VoiceNumber.ShouldNotBeNull());
                }
            }
        );

    [Fact]
    [Trait("Req", "FRE-003")]
    [Trait("Req", "VCO-001")]
    public void The_same_tiles_change_their_line_between_a_profile_and_Frequents_in_Compact() =>
        WithBody(
            PanelBodyTestData.Library(3, 0),
            PanelLayoutSettings.Default with
            {
                Size = PanelSize.Medium,
                Compact = true,
            },
            TypeScale.MinScalePercent,
            (body, panel, library) =>
            {
                // Frequents with the shortcuts of the profile, in the same order: each tile is drawn again with its line.
                panel.Apply(Frequents(library, "w0", "w1", "w2"));
                panel.ApplyContext(PanelBodyContext.Idle with { Frequents = true });
                body.UpdateLayout();
                body.ShortcutGrid.TileControls.ShouldAllBe(static t => t.Control.Keys == "Word");

                panel.ApplyContext(PanelBodyContext.Idle);
                panel.Apply(
                    PanelProjector.Project(
                        library,
                        PanelBodyTestData.Word,
                        LangCode.Es,
                        LangCode.Es,
                        PanelBodyTestData.GalleryKeys
                    )
                );
                body.UpdateLayout();

                body.ShortcutGrid.TileControls.Select(static t => t.ViewModel.Keys)
                    .ShouldBe(["Ctrl+N", "Ctrl+K", "Ctrl+S"]);
                body.ShortcutGrid.TileControls.ShouldAllBe(
                    static t => t.Control.Keys.Length == 0,
                    "the Compact view has no key line"
                );
                Part(body.ShortcutGrid.TileControls[0].Control, ShortcutTileTemplate.IconPart)
                    .Visibility.ShouldBe(Visibility.Visible);
            }
        );

    [Theory]
    [InlineData(PanelSize.Small)]
    [InlineData(PanelSize.Medium)]
    [InlineData(PanelSize.Large)]
    [Trait("Req", "ACC-009")]
    [Trait("Req", "VCO-001")]
    public void The_voice_number_and_the_icon_of_the_row_are_one_pair_centred_in_the_tile(
        PanelSize size
    ) =>
        WithBody(
            PanelLayoutSettings.Default with
            {
                Size = size,
                Compact = true,
                VoiceNumbers = true,
            },
            TypeScale.MinScalePercent,
            (body, _, _) =>
            {
                var tiles = body.Strip.TileControls.Select(static t => t.Control).ToList();
                tiles.ShouldNotBeEmpty();
                foreach (var tile in tiles)
                {
                    foreach (var number in new[] { 1, 15, 99 })
                    {
                        tile.VoiceNumber = number;
                        tile.UpdateLayout();
                        var badge = Bounds(Part(tile, ShortcutTileTemplate.VoiceNumberPart), tile);
                        var icon = Drawn(Part(tile, ShortcutTileTemplate.IconPart), tile);

                        badge.Right.ShouldBeLessThanOrEqualTo(
                            icon.Left,
                            Invariant($"{number} covers the icon")
                        );
                        var left = badge.Left;
                        var right = tile.ActualWidth - icon.Right;
                        Math.Abs(left - right)
                            .ShouldBeLessThanOrEqualTo(1, Invariant($"{number}: off centre"));
                        Part(tile, ShortcutTileTemplate.TypePart)
                            .Visibility.ShouldBe(Visibility.Collapsed);
                    }
                }
            }
        );

    private static void ShowsOrigin(ShortcutTile tile, int? number)
    {
        var label = (TextBlock)Part(tile, ShortcutTileTemplate.LabelPart);
        var keys = (TextBlock)Part(tile, ShortcutTileTemplate.KeysPart);
        var what = Invariant($"{tile.AccessibleName} ({number})");

        Part(tile, ShortcutTileTemplate.IconPart)
            .Visibility.ShouldBe(Visibility.Collapsed, $"{what}: no room for the icon");
        label.TextWrapping.ShouldBe(TextWrapping.NoWrap, what);
        keys.Visibility.ShouldBe(Visibility.Visible, what);
        keys.Opacity.ShouldBe(1, $"{what}: the origin fits under the name");
        keys.FontFamily.ShouldBeSameAs(AppFonts.Ui, $"{what}: a profile is words, not keys");
        var tileBox = new Rect(tile.RenderSize);
        var name = Bounds(label, tile);
        var origin = Bounds(keys, tile);
        tileBox.Contains(name).ShouldBeTrue(Invariant($"{what}: the name {name} leaves {tileBox}"));
        tileBox
            .Contains(origin)
            .ShouldBeTrue(Invariant($"{what}: the origin {origin} leaves {tileBox}"));
        name.Bottom.ShouldBeLessThanOrEqualTo(origin.Top + 0.5, what);
        var above = name.Top;
        var below = tileBox.Height - origin.Bottom;
        Math.Abs(above - below)
            .ShouldBeLessThanOrEqualTo(1, $"{what}: the pair is centred down the tile");
        if (number is not null)
        {
            var badge = Bounds(Part(tile, ShortcutTileTemplate.VoiceNumberPart), tile);
            badge.IntersectsWith(name).ShouldBeFalse($"{what}: the number covers the name");
            badge.IntersectsWith(origin).ShouldBeFalse($"{what}: the number covers the origin");
        }

        // The name rises to the height of the corner: the type mark goes to the help of the tile, never over the name.
        Part(tile, ShortcutTileTemplate.TypePart)
            .Visibility.ShouldBe(Visibility.Collapsed, $"{what}: the type mark sits on the name");
    }

    private static PanelModel Frequents(ShortcutLibrary library, params string[] ids) =>
        PanelProjector.ProjectFrequents(
            library,
            [
                .. ids.Select(id =>
                {
                    var shortcut = new ShortcutId(id);
                    library.TryLocate(shortcut, out var location).ShouldBeTrue();
                    library.TryGetShortcut(shortcut, out var found).ShouldBeTrue();
                    return new FrequentEntry(found, location, Pinned: false, Uses: 1);
                }),
            ],
            PanelBodyTestData.Word,
            AlwaysVisible,
            LangCode.Es,
            LangCode.Es
        );

    private static void WithBody(
        PanelLayoutSettings layout,
        int scale,
        Action<PanelBodyView, PanelViewModel, ShortcutLibrary> check
    ) => WithBody(PanelBodyTestData.GalleryLibrary(), layout, scale, check);

    private static void WithBody(
        ShortcutLibrary library,
        PanelLayoutSettings layout,
        int scale,
        Action<PanelBodyView, PanelViewModel, ShortcutLibrary> check
    ) =>
        WpfThread.Invoke(() =>
        {
            using var theme = new ThemeService(
                new FakeSystemTheme(),
                ThemeChoice.Dark,
                scale,
                reduceMotion: true
            );
            var panel = PanelBodyTestData.Panel(
                library,
                PanelBodyTestData.Word,
                new RecordingBodyIntents(),
                keys: PanelBodyTestData.GalleryKeys
            );
            panel.ApplyLayout(layout);
            var body = new PanelBodyView(panel);
            var root = new StackPanel();
            root.Children.Add(body);
            theme.Attach(root);
            try
            {
                root.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                root.Arrange(new Rect(root.DesiredSize));
                root.UpdateLayout();
                check(body, panel, library);
            }
            finally
            {
                body.Detach();
                theme.Detach(root);
            }
        });

    private static FrameworkElement Part(ShortcutTile tile, string name) =>
        (FrameworkElement)tile.Template.FindName(name, tile);

    private static Rect Bounds(FrameworkElement part, Visual tile) =>
        part.TransformToAncestor(tile).TransformBounds(new Rect(part.RenderSize));

    /// <summary>The box of the glyph an icon draws, centred in its box.</summary>
    private static Rect Drawn(FrameworkElement icon, Visual tile)
    {
        var box = Bounds(icon, tile);
        var size = ((SymbolIcon)icon).Size;
        return new Rect(
            box.Left + ((box.Width - size) / 2),
            box.Top + ((box.Height - size) / 2),
            size,
            size
        );
    }
}
