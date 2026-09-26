using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Threading;
using Clicalo.TestKit.Windows.Rendering;
using Clicalo.UI.Wpf.Automation;
using Clicalo.UI.Wpf.Controls;

namespace Clicalo.Windowing.IntegrationTests.Automation;

/// <summary>
/// The tile's UI Automation contract in process, headless (blueprint §8.6): name, help text and item status from the
/// view model, one pattern per kind, a leaf element, asynchronous Invoke, no focus outside a keyboard lease and the
/// shared default template and focus ring.
/// </summary>
public sealed class ShortcutTilePeerTests
{
    [Fact]
    [Trait("Req", "REG-06")]
    [Trait("Req", "IDI-003")]
    public void The_key_combination_is_the_help_text_and_an_explicit_value_wins() =>
        WpfThread.Invoke(() =>
        {
            var tile = new ShortcutTile
            {
                AccessibleName = "Negrita",
                AccessibleHelpText = "Ctrl + N",
            };
            var peer = PeerOf(tile);

            peer.GetHelpText().ShouldBe("Ctrl + N");

            AutomationProperties.SetHelpText(tile, "Negrita en Word");
            peer.GetHelpText().ShouldBe("Negrita en Word");
        });

    [Fact]
    [Trait("Req", "ACC-003")]
    [Trait("Req", "REG-06")]
    public void The_state_text_is_the_item_status() =>
        WpfThread.Invoke(() =>
        {
            var tile = new ShortcutTile
            {
                AccessibleName = "Mantener Ctrl",
                Pattern = ShortcutTilePattern.Toggle,
                ToggleState = ToggleState.On,
                AccessibleState = "ACTIVO",
            };

            PeerOf(tile).GetItemStatus().ShouldBe("ACTIVO");
        });

    [Fact]
    [Trait("Req", "ACC-009")]
    public void An_explicit_automation_name_wins_over_the_composed_one() =>
        WpfThread.Invoke(() =>
        {
            var tile = new ShortcutTile { AccessibleName = "Negrita", VoiceNumber = 1 };
            AutomationProperties.SetName(tile, "Negrita del documento");

            PeerOf(tile).GetName().ShouldBe("Negrita del documento");
            tile.AutomationName.ShouldBe("1 Negrita");
        });

    [Theory]
    [Trait("Req", "ACC-001")]
    [Trait("Req", "REG-06")]
    [InlineData(ShortcutTilePattern.Invoke, PatternInterface.Invoke)]
    [InlineData(ShortcutTilePattern.Toggle, PatternInterface.Toggle)]
    [InlineData(ShortcutTilePattern.ExpandCollapse, PatternInterface.ExpandCollapse)]
    public void A_tile_is_a_Button_with_exactly_the_pattern_of_its_action(
        ShortcutTilePattern pattern,
        PatternInterface expected
    ) =>
        WpfThread.Invoke(() =>
        {
            var peer = PeerOf(new ShortcutTile { AccessibleName = "Perfil", Pattern = pattern });

            peer.GetAutomationControlType().ShouldBe(AutomationControlType.Button);
            peer.GetClassName().ShouldBe(nameof(ShortcutTile));
            // WPF gives every UIElement SynchronizedInput (a testing aid, not an action): it is not a tile pattern.
            Enum.GetValues<PatternInterface>()
                .Where(candidate =>
                    candidate != PatternInterface.SynchronizedInput
                    && peer.GetPattern(candidate) is not null
                )
                .ShouldBe([expected]);
        });

    [Fact]
    [Trait("Req", "ACC-009")]
    [Trait("Req", "REG-06")]
    public void Only_a_visible_tile_is_in_the_control_view_so_a_hidden_one_gets_no_voice_number() =>
        WpfThread.Invoke(() =>
        {
            var shown = new ShortcutTile { AccessibleName = "Negrita", VoiceNumber = 1 };
            var collapsed = new ShortcutTile
            {
                AccessibleName = "Cursiva",
                VoiceNumber = 2,
                Visibility = Visibility.Collapsed,
            };
            var host = new StackPanel();
            host.Children.Add(shown);
            host.Children.Add(collapsed);

            // A presentation source makes WPF visibility real; its window is created hidden and never shown.
            using var source = new HwndSource(
                new HwndSourceParameters("clicalo-peer-test") { WindowStyle = 0 }
            )
            {
                RootVisual = host,
            };

            shown.IsVisible.ShouldBeTrue();
            PeerOf(shown).IsControlElement().ShouldBeTrue();
            PeerOf(shown).IsContentElement().ShouldBeTrue();
            PeerOf(collapsed).IsControlElement().ShouldBeFalse();
            PeerOf(collapsed).IsContentElement().ShouldBeFalse();

            collapsed.Visibility = Visibility.Visible;
            PeerOf(collapsed).IsControlElement().ShouldBeTrue();
            source.RootVisual = null;
            PeerOf(shown).IsControlElement().ShouldBeFalse();
        });

    [Fact]
    [Trait("Req", "REG-06")]
    public void A_tile_is_a_leaf_even_with_its_template_applied() =>
        WpfThread.Invoke(() =>
        {
            var tile = new ShortcutTile
            {
                AccessibleName = "Negrita",
                VoiceNumber = 1,
                AccessibleState = "ACTIVO",
            };
            LayOut(tile, new Size(120, 90));

            tile.Template.ShouldBeSameAs(ShortcutTileTemplate.Default);
            tile.Template.FindName(ShortcutTileTemplate.LabelPart, tile)
                .ShouldBeOfType<TextBlock>();
            PeerOf(tile).GetChildren().ShouldBeNull();
        });

    [Fact]
    [Trait("Req", "REG-06")]
    public void Invoke_returns_at_once_and_raises_the_event_on_the_next_dispatcher_turn() =>
        WpfThread.Invoke(() =>
        {
            var tile = new ShortcutTile { AccessibleName = "Negrita" };
            var threads = new List<int>();
            tile.Invoked += (_, _) => threads.Add(Environment.CurrentManagedThreadId);

            Provider<IInvokeProvider>(tile, PatternInterface.Invoke).Invoke();

            threads.ShouldBeEmpty("UI Automation's Invoke must return before the action runs.");
            WpfThread.DrainPendingWork();
            threads.ShouldBe([Environment.CurrentManagedThreadId]);
        });

    [Fact]
    [Trait("Req", "REG-06")]
    public void A_tile_disabled_before_the_queued_invoke_runs_does_nothing() =>
        WpfThread.Invoke(() =>
        {
            var tile = new ShortcutTile { AccessibleName = "Negrita" };
            var invoked = 0;
            tile.Invoked += (_, _) => invoked++;

            Provider<IInvokeProvider>(tile, PatternInterface.Invoke).Invoke();
            tile.IsEnabled = false;
            WpfThread.DrainPendingWork();

            invoked.ShouldBe(0);
        });

    [Fact]
    [Trait("Req", "REG-06")]
    public void A_disabled_tile_refuses_every_pattern() =>
        WpfThread.Invoke(() =>
        {
            var invoke = new ShortcutTile { IsEnabled = false };
            var toggle = new ShortcutTile
            {
                IsEnabled = false,
                Pattern = ShortcutTilePattern.Toggle,
            };
            var expand = new ShortcutTile
            {
                IsEnabled = false,
                Pattern = ShortcutTilePattern.ExpandCollapse,
            };

            Should.Throw<ElementNotEnabledException>(() =>
                Provider<IInvokeProvider>(invoke, PatternInterface.Invoke).Invoke()
            );
            Should.Throw<ElementNotEnabledException>(() =>
                Provider<IToggleProvider>(toggle, PatternInterface.Toggle).Toggle()
            );
            Should.Throw<ElementNotEnabledException>(() =>
                Provider<IExpandCollapseProvider>(expand, PatternInterface.ExpandCollapse).Expand()
            );
        });

    [Fact]
    [Trait("Req", "ACC-001")]
    public void Toggle_is_synchronous_so_the_client_reads_the_state_the_view_model_set() =>
        WpfThread.Invoke(() =>
        {
            var tile = new ShortcutTile
            {
                AccessibleName = "Mayús",
                Pattern = ShortcutTilePattern.Toggle,
            };
            tile.Toggled += (_, _) =>
                tile.ToggleState =
                    tile.ToggleState == ToggleState.Off
                        ? ToggleState.On
                        : ToggleState.Indeterminate;
            var toggle = Provider<IToggleProvider>(tile, PatternInterface.Toggle);

            toggle.Toggle();
            toggle.ToggleState.ShouldBe(ToggleState.On);
            toggle.Toggle();
            toggle.ToggleState.ShouldBe(ToggleState.Indeterminate);
        });

    [Fact]
    [Trait("Req", "ACC-001")]
    public void Expand_and_collapse_ask_the_view_model_and_report_its_state() =>
        WpfThread.Invoke(() =>
        {
            var tile = new ShortcutTile
            {
                AccessibleName = "Perfil",
                Pattern = ShortcutTilePattern.ExpandCollapse,
            };
            var requests = new List<string>();
            tile.ExpandRequested += (_, _) =>
            {
                requests.Add("expand");
                tile.IsExpanded = true;
            };
            tile.CollapseRequested += (_, _) =>
            {
                requests.Add("collapse");
                tile.IsExpanded = false;
            };
            var provider = Provider<IExpandCollapseProvider>(tile, PatternInterface.ExpandCollapse);

            provider.ExpandCollapseState.ShouldBe(ExpandCollapseState.Collapsed);
            provider.Expand();
            provider.ExpandCollapseState.ShouldBe(ExpandCollapseState.Expanded);
            provider.Collapse();
            provider.ExpandCollapseState.ShouldBe(ExpandCollapseState.Collapsed);
            requests.ShouldBe(["expand", "collapse"]);
        });

    [Fact]
    [Trait("Req", "REG-01")]
    public void Outside_an_active_window_the_tile_is_not_focusable_and_SetFocus_never_focuses() =>
        WpfThread.Invoke(() =>
        {
            var tile = new ShortcutTile { AccessibleName = "Negrita" };
            var host = new StackPanel();
            host.Children.Add(tile);
            LayOut(host, new Size(200, 100));
            var peer = PeerOf(tile);

            tile.Focusable.ShouldBeTrue("the keyboard and voice mode can focus it under a lease");
            peer.IsKeyboardFocusable().ShouldBeFalse();
            Should.Throw<InvalidOperationException>(peer.SetFocus);
            tile.IsKeyboardFocused.ShouldBeFalse();
            peer.HasKeyboardFocus().ShouldBeFalse();
        });

    [Fact]
    [Trait("Req", "TEM-009")]
    public void The_focus_visual_is_the_3_px_ring_of_the_tile_corner() =>
        WpfThread.Invoke(() =>
        {
            var tile = new ShortcutTile();

            tile.FocusVisualStyle.ShouldBeSameAs(FocusRingStyle.Tile);
        });

    [Fact]
    [Trait("Req", "REG-06")]
    public void The_default_template_and_focus_ring_can_be_used_by_another_UI_thread()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                var tile = new ShortcutTile { AccessibleName = "Copiar", VoiceNumber = 4 };
                LayOut(tile, new Size(92, 78));
                tile.Template.FindName(ShortcutTileTemplate.ChromePart, tile)
                    .ShouldBeOfType<Border>();
                tile.FocusVisualStyle.ShouldBeSameAs(FocusRingStyle.Tile);
                PeerOf(tile).GetName().ShouldBe("4 Copiar");
            }
            catch (Exception ex)
            {
                failure = ex;
            }
            finally
            {
                Dispatcher.CurrentDispatcher.InvokeShutdown();
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        failure.ShouldBeNull();
        ShortcutTileTemplate.Default.IsSealed.ShouldBeTrue();
        ShortcutTileTemplate.Default.Dispatcher.ShouldBeNull();
        FocusRingStyle.Tile.IsSealed.ShouldBeTrue();
        FocusRingStyle.Tile.Dispatcher.ShouldBeNull();
    }

    private static AutomationPeer PeerOf(UIElement element) =>
        UIElementAutomationPeer.CreatePeerForElement(element);

    private static T Provider<T>(ShortcutTile tile, PatternInterface pattern)
        where T : class => (T)PeerOf(tile).GetPattern(pattern);

    private static void LayOut(UIElement element, Size size)
    {
        element.Measure(size);
        element.Arrange(new Rect(size));
        element.UpdateLayout();
    }
}
