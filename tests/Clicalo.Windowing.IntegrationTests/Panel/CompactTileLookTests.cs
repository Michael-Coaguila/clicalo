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
using Clicalo.UI.Wpf.Theming.Generated;
using Clicalo.Windowing.IntegrationTests.SearchPanel;
using Clicalo.Windowing.IntegrationTests.Theming;
using static System.FormattableString;

namespace Clicalo.Windowing.IntegrationTests.MinimalPanel;

/// <summary>
/// The tiles of Frequents and of the search and the short tiles of the Compact view, headless (built and laid out on
/// the WPF thread; never shown): a Frequents tile keeps its profile of origin under its name, as a search result does,
/// in every size and view (FRE-003, BUS-005), in words and across the inner width of the tile; the other tiles of the
/// Compact view have no key line (VCO-001); and a voice number never covers the icon or the name (ACC-009).
/// </summary>
public sealed class CompactTileLookTests
{
    private const string AlwaysVisible = "Siempre visible";

    public static TheoryData<PanelSize, bool, bool, int> Shapes()
    {
        var shapes = new TheoryData<PanelSize, bool, bool, int>();
        foreach (var size in new[] { PanelSize.Small, PanelSize.Medium, PanelSize.Large })
        {
            foreach (var compact in new[] { true, false })
            {
                foreach (var numbers in new[] { false, true })
                {
                    foreach (
                        var scale in new[] { TypeScale.MinScalePercent, TypeScale.MaxScalePercent }
                    )
                    {
                        shapes.Add(size, compact, numbers, scale);
                    }
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
    [Trait("Req", "CUA-011")]
    public void A_frequent_shows_its_profile_of_origin_under_its_name(
        PanelSize size,
        bool compact,
        bool numbers,
        int scale
    ) =>
        WithBody(
            PanelLayoutSettings.Default with
            {
                Size = size,
                Compact = compact,
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

                // Its icon only where the tile has room for the icon, a line of name and the origin (D20).
                var keepsIcon =
                    !compact
                    && (
                        size == PanelSize.Large
                        || (size == PanelSize.Medium && scale == TypeScale.MinScalePercent)
                    );
                foreach (var tile in tiles.Select(static t => t.Control))
                {
                    foreach (var number in numbers ? new int?[] { 1, 15, 99 } : [null])
                    {
                        tile.VoiceNumber = number;
                        tile.UpdateLayout();
                        if (keepsIcon)
                        {
                            ShowsOriginUnderIcon(tile, number, scale);
                        }
                        else
                        {
                            ShowsOrigin(tile, number, scale);
                        }
                    }
                }
            }
        );

    [Theory]
    [InlineData(PanelSize.Small, true, true)]
    [InlineData(PanelSize.Small, false, false)]
    [InlineData(PanelSize.Small, false, true)]
    [InlineData(PanelSize.Medium, false, false)]
    [InlineData(PanelSize.Medium, true, true)]
    [Trait("Req", "BUS-005")]
    [Trait("Req", "FRE-003")]
    [Trait("Req", "ACC-009")]
    public void Two_results_with_one_name_are_told_apart_by_their_profile(
        PanelSize size,
        bool compact,
        bool numbers
    ) =>
        WithBody(
            PanelBodyTestData.GallerySearchLibrary(),
            PanelLayoutSettings.Default with
            {
                Size = size,
                Compact = compact,
                VoiceNumbers = numbers,
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
                    tile.VoiceNumber.HasValue.ShouldBe(numbers);
                    if (size == PanelSize.Medium && !compact)
                    {
                        ShowsOriginUnderIcon(tile, tile.VoiceNumber, TypeScale.MinScalePercent);
                    }
                    else
                    {
                        ShowsOrigin(tile, tile.VoiceNumber, TypeScale.MinScalePercent);
                    }
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
    [Trait("Req", "CUA-007")]
    public void The_voice_number_and_the_icon_of_the_row_are_one_pair_beside_the_type_mark_that_fits(
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
                tiles.ShouldContain(static t => t.TypeSymbol != null, "Hablar is a Mantener");
                var marks = 0;
                foreach (var tile in tiles)
                {
                    foreach (var number in new[] { 1, 15, 99 })
                    {
                        tile.VoiceNumber = number;
                        tile.UpdateLayout();
                        var what = Invariant($"{tile.AccessibleName} ({number})");
                        var badge = Bounds(Part(tile, ShortcutTileTemplate.VoiceNumberPart), tile);
                        var icon = Drawn(Part(tile, ShortcutTileTemplate.IconPart), tile);
                        var type = Part(tile, ShortcutTileTemplate.TypePart);

                        badge.Right.ShouldBeLessThanOrEqualTo(icon.Left, $"{what} covers the icon");
                        badge.Left.ShouldBeGreaterThanOrEqualTo(Space.S1 - 0.5, what);

                        // The corner of the type mark keeps it while the pair fits whole beside it, S1 from the edge.
                        var corner = tile.ActualWidth - Space.S1 - IconBox.Badge;
                        var fits =
                            tile.TypeSymbol is not null
                            && Space.S1 + badge.Width + Space.S1 + tile.IconSize <= corner + 0.5;
                        if (fits)
                        {
                            marks++;
                            type.Visibility.ShouldBe(Visibility.Visible, $"{what}: the mark fits");
                            var mark = Bounds(type, tile);
                            mark.Left.ShouldBe(corner, 0.5, what);
                            icon.Right.ShouldBeLessThanOrEqualTo(
                                mark.Left + 0.5,
                                $"{what}: the icon reaches the mark"
                            );
                        }
                        else
                        {
                            type.Visibility.ShouldBe(Visibility.Collapsed, $"{what}: no room");
                            var left = badge.Left;
                            var right = tile.ActualWidth - icon.Right;
                            Math.Abs(left - right)
                                .ShouldBeLessThanOrEqualTo(1, $"{what}: off centre");
                        }
                    }
                }

                // S: 61 wide, the hand of Hablar does not fit beside 20 + 4 + 20 (one digit); M and L keep it.
                if (size == PanelSize.Small)
                {
                    marks.ShouldBe(0);
                }
                else
                {
                    marks.ShouldBeGreaterThan(0);
                }
            }
        );

    /// <summary>
    /// A tile without room for its icon (Compact, S, M at a larger text): the name in one line and the origin under it
    /// in words, the two centred down the tile, and a voice number in its corner, clear of both.
    /// </summary>
    private static void ShowsOrigin(ShortcutTile tile, int? number, int scale)
    {
        var label = (TextBlock)Part(tile, ShortcutTileTemplate.LabelPart);
        var what = Invariant($"{tile.AccessibleName} ({number})");

        Part(tile, ShortcutTileTemplate.IconPart)
            .Visibility.ShouldBe(Visibility.Collapsed, $"{what}: no room for the icon");
        label.TextWrapping.ShouldBe(TextWrapping.NoWrap, what);
        var (name, origin) = OriginInWords(tile, scale, what);
        var tileBox = new Rect(tile.RenderSize);
        tileBox.Contains(name).ShouldBeTrue(Invariant($"{what}: the name {name} leaves {tileBox}"));
        name.Bottom.ShouldBeLessThanOrEqualTo(origin.Top + 0.5, what);

        // The name has the width of the tile, or of what the number leaves beside it, and is whole when it fits it.
        var start = Space.S1;
        var top = 0.0;
        var bottom = tileBox.Height;
        if (number is not null)
        {
            var badge = Bounds(Part(tile, ShortcutTileTemplate.VoiceNumberPart), tile);
            badge.Left.ShouldBe(Space.S1, 0.5, $"{what}: the number leaves its corner");
            badge.Top.ShouldBe(Space.S1, 0.5, $"{what}: the number leaves its corner");
            badge.IntersectsWith(name).ShouldBeFalse($"{what}: the number covers the name");
            badge.IntersectsWith(origin).ShouldBeFalse($"{what}: the number covers the origin");
            if (name.Top >= badge.Bottom)
            {
                // Under the number: centred in the room under its corner.
                top = badge.Bottom;
                bottom -= Space.S1;
            }
            else
            {
                start = badge.Right + Space.S1;
                name.Left.ShouldBeGreaterThanOrEqualTo(start - 0.5, what);
                if (origin.Width <= 2 * (tileBox.Width - Space.S1 - Centre(name)))
                {
                    Centre(origin)
                        .ShouldBe(Centre(name), 1, $"{what}: the origin is centred with the name");
                }
            }
        }

        var room = tileBox.Width - Space.S1 - start;
        name.Right.ShouldBeLessThanOrEqualTo(tileBox.Width - Space.S1 + 0.5, what);
        label.ActualWidth.ShouldBeGreaterThanOrEqualTo(
            Shown(label, room) - 0.5,
            $"{what}: «{label.Text}» is cut before the end of its room"
        );
        Math.Abs((name.Top - top) - (bottom - origin.Bottom))
            .ShouldBeLessThanOrEqualTo(1, $"{what}: the pair is centred down its room");

        // The name rises to the height of the corner: the type mark goes to the help of the tile, never over the name.
        Part(tile, ShortcutTileTemplate.TypePart)
            .Visibility.ShouldBe(Visibility.Collapsed, $"{what}: the type mark sits on the name");
    }

    /// <summary>A tile with room for its icon (Full, M and L): the anatomy of the grid, its origin in words under the name.</summary>
    private static void ShowsOriginUnderIcon(ShortcutTile tile, int? number, int scale)
    {
        var what = Invariant($"{tile.AccessibleName} ({number})");
        var icon = Drawn(Part(tile, ShortcutTileTemplate.IconPart), tile);
        Part(tile, ShortcutTileTemplate.IconPart).Visibility.ShouldBe(Visibility.Visible, what);
        var (name, origin) = OriginInWords(tile, scale, what);
        icon.Bottom.ShouldBeLessThanOrEqualTo(name.Top + 0.5, what);
        name.Bottom.ShouldBeLessThanOrEqualTo(origin.Top + 0.5, what);
        tile.MaxNameLines.ShouldBe(1, $"{what}: one line of name with the line under it (D20)");
        if (number is not null)
        {
            var badge = Bounds(Part(tile, ShortcutTileTemplate.VoiceNumberPart), tile);
            badge.Right.ShouldBeLessThanOrEqualTo(icon.Left, $"{what}: the number covers the icon");
            badge.Bottom.ShouldBeLessThanOrEqualTo(name.Top + 0.5, what);
        }
    }

    /// <summary>
    /// The origin of <paramref name="tile"/> under its name: shown whole in <c>labelSmall</c> of the interface font, a
    /// profile being words and not keys, across the whole inner width of the tile (<see cref="Space.S1"/> inside its
    /// edges), and ending in «…» only when it does not fit there.
    /// </summary>
    private static (Rect Name, Rect Origin) OriginInWords(ShortcutTile tile, int scale, string what)
    {
        var label = (TextBlock)Part(tile, ShortcutTileTemplate.LabelPart);
        var keys = (TextBlock)Part(tile, ShortcutTileTemplate.KeysPart);
        keys.Visibility.ShouldBe(Visibility.Visible, what);
        keys.Opacity.ShouldBe(1, $"{what}: the origin fits under the name");
        keys.FontFamily.ShouldBeSameAs(AppFonts.Ui, $"{what}: a profile is words, not keys");
        keys.FontSize.ShouldBe(
            TypeScale.Scale(TextStyles.LabelSmall.Size, scale),
            0.01,
            $"{what}: labelSmall"
        );
        var inner = tile.ActualWidth - (2 * Space.S1);
        var origin = Bounds(keys, tile);
        origin.Left.ShouldBeGreaterThanOrEqualTo(
            Space.S1 - 0.5,
            Invariant($"{what}: the origin {origin}")
        );
        origin.Right.ShouldBeLessThanOrEqualTo(
            tile.ActualWidth - Space.S1 + 0.5,
            Invariant($"{what}: the origin {origin}")
        );
        origin.Bottom.ShouldBeLessThanOrEqualTo(tile.ActualHeight, what);
        keys.ActualWidth.ShouldBeGreaterThanOrEqualTo(
            Shown(keys, inner) - 0.5,
            $"{what}: «{keys.Text}» is cut before the inner width of the tile"
        );
        return (Bounds(label, tile), origin);
    }

    private static double Centre(Rect box) => (box.Left + box.Right) / 2;

    /// <summary>
    /// The width <paramref name="text"/> takes in one line in <paramref name="room"/>: whole when it fits there, cut
    /// with «…» at its end when it does not.
    /// </summary>
    private static double Shown(TextBlock text, double room)
    {
        var line = new TextBlock
        {
            Text = text.Text,
            FontFamily = text.FontFamily,
            FontSize = text.FontSize,
            FontWeight = text.FontWeight,
            FontStretch = text.FontStretch,
            FontStyle = text.FontStyle,
            TextWrapping = TextWrapping.NoWrap,
            TextTrimming = TextTrimming.CharacterEllipsis,
        };
        line.Measure(new Size(room, double.PositiveInfinity));
        return line.DesiredSize.Width;
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
