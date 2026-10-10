using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using Clicalo.Domain.Keys;
using Clicalo.TestKit.Windows.Rendering;
using Clicalo.UI.Wpf.Controls;
using Clicalo.UI.Wpf.Theming.Generated;
using Clicalo.UI.Wpf.Workspace;

namespace Clicalo.Windowing.IntegrationTests.ControlCenter;

/// <summary>
/// The keyboard in the Control Center, headless: the focus ring of a text field (TEM-009), the control that takes the
/// keyboard when a section draws itself again (ACC-004) and the keys «Grabar con teclado» understands (EDI-010).
/// </summary>
public sealed class WorkspaceFocusTests
{
    [Fact]
    [Trait("Req", "TEM-009")]
    public void A_text_field_has_the_ring_of_3_px_2_px_outside_its_border_in_the_focus_token()
    {
        WpfThread.Invoke(() =>
        {
            var box = new TextBox();
            var layers = new Grid();
            layers.Children.Add(box);

            var ring = FieldFocusRing.Attach(layers, box, fieldRadius: 10, fieldBorder: 1);

            ring.BorderThickness.ShouldBe(new Thickness(3));
            ring.BorderThickness.Left.ShouldBe(FocusRing.Thickness);
            // 2 px of gap + 3 px of ring + the 1 px border of the field it sits inside.
            ring.Margin.ShouldBe(new Thickness(-(FocusRing.Offset + FocusRing.Thickness + 1)));
            ring.CornerRadius.ShouldBe(
                new CornerRadius(10 + FocusRing.Offset + FocusRing.Thickness)
            );
            ring.IsHitTestVisible.ShouldBeFalse("it never takes a tap from the field");
            ring.Visibility.ShouldBe(Visibility.Collapsed, "only while the field has the keyboard");
            layers.Children.Contains(ring).ShouldBeTrue();
            var resources = new ResourceDictionary
            {
                [ThemeBrushKey.For(ColorToken.FocusRing)] = System.Windows.Media.Brushes.Yellow,
            };
            layers.Resources = resources;
            ring.BorderBrush.ShouldBe(System.Windows.Media.Brushes.Yellow);
        });
    }

    [Fact]
    [Trait("Req", "ACC-004")]
    public void The_control_that_takes_the_place_of_the_focused_one_is_found_after_a_rebuild()
    {
        WpfThread.Invoke(() =>
        {
            var region = new ContentControl { Focusable = false, Content = Section("Eliminar") };
            var root = new StackPanel();
            root.Children.Add(Named(new Button(), "Atajos"));
            root.Children.Add(region);
            root.Children.Add(Named(new Button(), "Deshacer"));

            var before = FocusKeeper.Stops(root);
            before
                .Select(AutomationProperties.GetName)
                .ShouldBe(["Atajos", "Probar", "Duplicar", "Eliminar", "Nombre", "Deshacer"]);
            var duplicate = FocusKeeper.Mark(root, before[2]);
            var delete = FocusKeeper.Mark(root, before[3]);
            var name = FocusKeeper.Mark(root, before[4]);

            // The section draws itself again: new controls, and the delete button is now armed with another name.
            region.Content = Section("Confirmar");

            var after = FocusKeeper.Stops(root);
            after.ShouldNotContain(before[2]);
            FocusKeeper.Find(root, duplicate).ShouldBeSameAs(after[2], "same kind and name");
            FocusKeeper
                .Find(root, delete)
                .ShouldBeSameAs(after[3], "same place when the name changed");
            FocusKeeper.Find(root, name).ShouldBeSameAs(after[4]);
            FocusKeeper.Find(root, name).ShouldBeOfType<TextBox>();

            // A region that lost controls: the nearest place, never nothing.
            region.Content = null;
            FocusKeeper.Find(root, delete).ShouldBeSameAs(FocusKeeper.Stops(root)[^1]);
            FocusKeeper.Find(new StackPanel(), delete).ShouldBeNull();
        });
    }

    [Fact]
    [Trait("Req", "ACC-004")]
    public void Hidden_and_disabled_controls_are_not_places_for_the_keyboard()
    {
        WpfThread.Invoke(() =>
        {
            var root = new StackPanel();
            root.Children.Add(Named(new Button(), "Visible"));
            root.Children.Add(Named(new Button { IsEnabled = false }, "Desactivado"));
            root.Children.Add(Named(new Button { Visibility = Visibility.Collapsed }, "Oculto"));
            root.Children.Add(Named(new Button { Focusable = false }, "Sin foco"));

            FocusKeeper.Stops(root).Select(AutomationProperties.GetName).ShouldBe(["Visible"]);
        });
    }

    [Fact]
    [Trait("Req", "EDI-010")]
    public void Every_key_the_recording_understands_is_a_key_of_the_catalog()
    {
        var catalog = KeyDefinitions.All.Select(d => d.Id.Value).ToHashSet(StringComparer.Ordinal);
        var mapped = Enum.GetValues<Key>()
            .Select(key => (Key: key, Id: RecordedKeys.IdOf(key)))
            .Where(pair => pair.Id is not null)
            .ToList();

        mapped.ShouldAllBe(pair => catalog.Contains(pair.Id!.Value.Value));
        mapped.Count.ShouldBeGreaterThan(100);
        RecordedKeys.IdOf(Key.A).ShouldBe(KeyIds.A);
        RecordedKeys.IdOf(Key.D7).ShouldBe(new KeyId("7"));
        RecordedKeys.IdOf(Key.F12).ShouldBe(new KeyId("f12"));
        RecordedKeys.IdOf(Key.NumPad3).ShouldBe(new KeyId("num.3"));
        RecordedKeys.IdOf(Key.Escape).ShouldBe(KeyIds.Escape);
        // The modifiers keep their side (EDI-009).
        RecordedKeys.IdOf(Key.LeftCtrl).ShouldBe(new KeyId("lctrl"));
        RecordedKeys.IdOf(Key.RightAlt).ShouldBe(new KeyId("altgr"));
        RecordedKeys.IdOf(Key.RWin).ShouldBe(new KeyId("rwin"));
        // Punctuation depends on the layout: it is chosen in the key picker.
        RecordedKeys.IdOf(Key.OemComma).ShouldBeNull();
    }

    private static StackPanel Section(string deleteName)
    {
        var section = new StackPanel();
        section.Children.Add(Named(new Button(), "Probar"));
        section.Children.Add(Named(new Button(), "Duplicar"));
        section.Children.Add(Named(new Button(), deleteName));
        section.Children.Add(Named(new TextBox(), "Nombre"));
        return section;
    }

    private static T Named<T>(T control, string name)
        where T : Control
    {
        AutomationProperties.SetName(control, name);
        return control;
    }
}
