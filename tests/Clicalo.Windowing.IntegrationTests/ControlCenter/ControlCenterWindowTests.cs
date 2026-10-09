using System.IO;
using System.Windows;
using Clicalo.Application.UseCases.Editor;
using Clicalo.Domain.Library;
using Clicalo.Domain.Primitives;
using Clicalo.Domain.Settings;
using Clicalo.Presentation.ControlCenter;
using Clicalo.Presentation.ControlCenter.Shortcuts;
using Clicalo.TestKit;
using Clicalo.TestKit.Windows.Rendering;
using Clicalo.UI.Wpf.Theming;
using Clicalo.UI.Wpf.Workspace;
using Clicalo.Windowing.IntegrationTests.Theming;

namespace Clicalo.Windowing.IntegrationTests.ControlCenter;

/// <summary>
/// The Control Center headless (docs/05): the window is built but never shown, so no handle, no desktop, no input and
/// no foreground. It checks the frame (CCM-001 to CCM-003), Esc (CCM-001) and the shortcuts section and editor that the
/// view models project. With <c>CLICALO_CC_PREVIEW=1</c> it also writes PNG previews to <c>artifacts/cc-preview</c> to
/// compare by eye with <c>docs/design/reference</c>.
/// </summary>
public sealed class ControlCenterWindowTests
{
    [Fact]
    [Trait("Req", "CCM-001")]
    [Trait("Req", "CCM-002")]
    [Trait("Req", "CCM-003")]
    public void The_window_has_the_frame_of_the_control_center()
    {
        var world = new ControlCenterTestWorld();
        var closed = 0;
        var (window, theme, viewModel) = Build(world, () => closed++);
        try
        {
            WpfThread.Invoke(() =>
            {
                window.Width.ShouldBe(ControlCenterWindow.DefaultWidth);
                window.Height.ShouldBe(ControlCenterWindow.DefaultHeight);
                window.MinWidth.ShouldBeLessThanOrEqualTo(760);
                window.MinHeight.ShouldBeLessThanOrEqualTo(520);
                window.ShowActivated.ShouldBeFalse("it comes to the front only through its lease");
                viewModel
                    .Nav.Select(n => n.Section)
                    .ShouldBe([
                        ControlCenterSection.Shortcuts,
                        ControlCenterSection.Templates,
                        ControlCenterSection.Panel,
                        ControlCenterSection.Touch,
                        ControlCenterSection.System,
                        ControlCenterSection.About,
                    ]);
                viewModel
                    .Nav.Single(n => n.SeparatorBefore)
                    .Section.ShouldBe(ControlCenterSection.System);
                viewModel.Nav[0].Count.ShouldBe(1, "Copiar is repeated");
                viewModel.Status.Text.ShouldBe("Cambios guardados");
                viewModel.Languages.Select(l => l.Label).ShouldBe(["ES", "EN"]);
                viewModel.Select(ControlCenterSection.Touch);
                viewModel.SectionTitle.ShouldBe("Precisión táctil");
            });
        }
        finally
        {
            Close(window, theme);
        }
    }

    [Fact]
    [Trait("Req", "CCM-001")]
    public void Esc_closes_the_open_menu_before_the_window()
    {
        var world = new ControlCenterTestWorld();
        var closed = 0;
        var (window, theme, viewModel) = Build(world, () => closed++);
        try
        {
            WpfThread.Invoke(() =>
            {
                viewModel.Shortcuts.OpenLibrary();
                viewModel.Escape();
                world.Shortcuts.Pane.ShouldNotBeOfType<EditorPane.Library>();
                closed.ShouldBe(0);
                viewModel.Escape();
                closed.ShouldBe(1);
            });
        }
        finally
        {
            Close(window, theme);
        }
    }

    [Fact]
    [Trait("Req", "ATJ-002")]
    [Trait("Req", "ATJ-005")]
    [Trait("Req", "ATJ-009")]
    [Trait("Req", "REP-003")]
    public void The_section_shows_profiles_link_and_tiles_with_their_marks()
    {
        var world = new ControlCenterTestWorld();
        var (window, theme, viewModel) = Build(world, () => { });
        try
        {
            WpfThread.Invoke(() =>
            {
                var screen = viewModel.Shortcuts.Screen;
                screen
                    .Profiles.Rows.Select(r => r.Name)
                    .ShouldBe(["Siempre visible", "General", "Word", "Navegador"]);
                screen.Profiles.Rows.Single(r => r.Selected).Name.ShouldBe("Word");
                screen.Link.ShouldNotBeNull().State.ShouldBe(LinkState.Linked);
                screen.TopBar.Duplicates.ShouldNotBeNull().ShouldStartWith("1 ");
                var tiles = screen.Grid.Tiles;
                tiles.Count.ShouldBe(6);
                tiles[0].Foot.ShouldBe("Ctrl + N");
                tiles[3].TypeIcon.ShouldBe("pan_tool");
                tiles[4].Foot.ShouldBe("3 pasos");
                tiles[5].Incomplete.ShouldBeTrue();
                tiles[5].Name.ShouldBe("Sin nombre");
                screen.Column.ShouldBe(EditorColumn.Editor);
                viewModel
                    .Shortcuts.Editor.Model.ShouldNotBeNull()
                    .Identity.Name.ShouldBe("Negrita");
            });
        }
        finally
        {
            Close(window, theme);
        }
    }

    [Fact]
    [Trait("Req", "EDI-006")]
    [Trait("Req", "EDI-007")]
    [Trait("Req", "EDI-013")]
    public void The_editor_projects_the_kind_the_combination_and_the_steps()
    {
        var world = new ControlCenterTestWorld();
        var (window, theme, viewModel) = Build(world, () => { });
        try
        {
            WpfThread.Invoke(() =>
            {
                var editor = viewModel.Shortcuts.Editor;
                var combo = editor.Model!.Combo.ShouldNotBeNull();
                combo.Chips.Select(c => c.Label).ShouldBe(["Ctrl", "N"]);
                combo
                    .Modifiers.Single(m => string.Equals(m.Label, "Ctrl", StringComparison.Ordinal))
                    .Chosen.ShouldBeTrue();
                combo.Columns.ShouldBe(7, "letters");
                editor.Model.Kinds.Options.Count(o => o.Selected).ShouldBe(1);
                world.Shortcuts.Select(new ShortcutId("macro"));
                viewModel.Shortcuts.Refresh();
                var steps = editor.Model.Steps.ShouldNotBeNull();
                steps
                    .Steps.Select(s => s.Text)
                    .ShouldBe(["Pulsar Ctrl + C", "Esperar 0,5 s", "Pulsar Ctrl + V"]);
                editor.Model.Combo.ShouldBeNull(
                    "a macro has no combination until a keys step opens"
                );
            });
        }
        finally
        {
            Close(window, theme);
        }
    }

    [Fact]
    public void Previews_are_written_when_asked()
    {
        if (
            !string.Equals(
                Environment.GetEnvironmentVariable("CLICALO_CC_PREVIEW"),
                "1",
                StringComparison.Ordinal
            )
        )
        {
            return;
        }

        var world = new ControlCenterTestWorld();
        var (window, theme, viewModel) = Build(world, () => { });
        try
        {
            Preview(window, theme, "shortcuts-editor", 1120, 680);
            WpfThread.Invoke(() =>
            {
                viewModel.Shortcuts.Editor.ToggleMore();
                viewModel.Shortcuts.Editor.TogglePicker();
                viewModel.Shortcuts.Editor.ToggleTest();
            });
            Preview(window, theme, "shortcuts-editor-open", 1120, 2400);
            WpfThread.Invoke(() =>
            {
                world.Shortcuts.Select(new ShortcutId("macro"));
                viewModel.Shortcuts.Editor.ToggleStep(1);
            });
            Preview(window, theme, "shortcuts-macro", 1120, 1400);
            WpfThread.Invoke(() =>
            {
                world.Shortcuts.Select(new ShortcutId("ccopy"));
                viewModel.Shortcuts.Refresh();
                viewModel.Shortcuts.Editor.ToggleDuplicates();
                viewModel.Shortcuts.ToggleProfileEdit();
                viewModel.Shortcuts.LinkAction();
            });
            Preview(window, theme, "shortcuts-duplicate", 1120, 1400);
            WpfThread.Invoke(viewModel.Shortcuts.OpenLibrary);
            Preview(window, theme, "shortcuts-library", 900, 680);
        }
        finally
        {
            Close(window, theme);
        }
    }

    private static (
        ControlCenterWindow Window,
        ThemeService Theme,
        ControlCenterViewModel ViewModel
    ) Build(ControlCenterTestWorld world, Action close) =>
        WpfThread.Invoke(() =>
        {
            var theme = new ThemeService(
                new FakeSystemTheme(),
                ThemeChoice.Dark,
                100,
                reduceMotion: true
            );
            world.Shortcuts.Open(new ListRef.InProfile(ControlCenterTestWorld.Word), null, false);
            var viewModel = new ControlCenterViewModel(world.Services, close);
            // As the composition root does: every change projects again once per dispatcher turn.
            world.Shortcuts.Changed += (_, _) => viewModel.Shortcuts.Invalidate();
            world.Profiles.Changed += (_, _) => viewModel.Shortcuts.Invalidate();
            world.Store.Changed += (_, _) =>
            {
                world.Shortcuts.OnDocumentChanged();
                viewModel.Shortcuts.Invalidate();
            };
            var window = new ControlCenterWindow(viewModel, theme);
            WpfThread.DrainPendingWork();
            return (window, theme, viewModel);
        });

    private static void Preview(
        ControlCenterWindow window,
        ThemeService theme,
        string name,
        double width,
        double height
    )
    {
        WpfThread.Invoke(WpfThread.DrainPendingWork);
        FrameworkElement? root = null;
        var png = RenderSnapshot.Render(
            () =>
            {
                root = (FrameworkElement)window.Content;
                window.Content = null;
                theme.Attach(root);
                return root;
            },
            new RenderSnapshotOptions { Width = width, Height = height }
        );
        WpfThread.Invoke(() =>
        {
            theme.Detach(root!);
            window.Content = root;
        });
        var folder = Path.Combine(RepoPaths.Root, "artifacts", "cc-preview");
        Directory.CreateDirectory(folder);
        File.WriteAllBytes(Path.Combine(folder, name + ".png"), png);
    }

    private static void Close(ControlCenterWindow window, ThemeService theme) =>
        WpfThread.Invoke(() =>
        {
            window.Destroy();
            theme.Dispose();
        });
}
