using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using Clicalo.UI.Wpf.Automation;
using Clicalo.UI.Wpf.Controls;
using Clicalo.UI.Wpf.Theming.Generated;

namespace Clicalo.Windowing.IntegrationTests.Automation.Audit;

/// <summary>
/// A sheet with every base control of direction C (D12) in its states, built only with the tokens, for the gallery and
/// the audit: the four buttons in the three heights, icon buttons, the options of every kind, switches, the slider,
/// fields with 🎤, the navigation row, cards, tiles, the status bar, badges and counters, and a designed scroll area.
/// The texts are those of the product in Spanish.
/// </summary>
internal static class DesignSystemSpecimen
{
    /// <summary>The width of the sheet, in device-independent pixels.</summary>
    public const double Width = 760;

    /// <summary>The sheet on the <c>win</c> surface.</summary>
    public static Border Build()
    {
        var sheet = new StackPanel();
        sheet.Children.Add(Section("Botones", Buttons()));
        sheet.Children.Add(Section("Botones de icono", IconButtons()));
        sheet.Children.Add(Section("Opciones", Options()));
        sheet.Children.Add(Section("Interruptores y deslizador", Switches()));
        sheet.Children.Add(Section("Campos", Fields()));
        sheet.Children.Add(Section("Navegación y tarjetas", Navigation()));
        sheet.Children.Add(Section("Fichas", Tiles()));
        sheet.Children.Add(Section("Estado, insignias y contadores", Status()));
        sheet.Children.Add(Section("Desplazamiento", Scroll()));
        var root = new Border
        {
            Width = Width,
            Padding = new Thickness(Space.S6),
            Child = sheet,
        };
        root.SetResourceReference(Border.BackgroundProperty, ThemeBrushKey.For(ColorToken.Win));
        return root;
    }

    private static StackPanel Section(string title, UIElement content)
    {
        var section = new StackPanel { Margin = new Thickness(0, 0, 0, Space.S6) };
        section.Children.Add(new Overline(title) { Margin = new Thickness(0, 0, 0, Space.S2) });
        section.Children.Add(content);
        return section;
    }

    private static WrapPanel Row(params UIElement[] children)
    {
        var row = new WrapPanel { Margin = new Thickness(0, 0, 0, Space.S2) };
        foreach (var child in children)
        {
            if (child is FrameworkElement element)
            {
                element.Margin = new Thickness(0, 0, Space.S2, Space.S2);
                element.VerticalAlignment = VerticalAlignment.Top;
            }

            row.Children.Add(child);
        }

        return row;
    }

    private static StackPanel Buttons()
    {
        var column = new StackPanel();
        foreach (var size in Enum.GetValues<ControlSize>())
        {
            column.Children.Add(
                Row(
                    new TouchButton { Content = "Añadir", Symbol = "add", Size = size },
                    new TouchButton
                    {
                        Content = "Duplicar",
                        Symbol = "content_copy",
                        Variant = ButtonVariant.Secondary,
                        Size = size,
                    },
                    new TouchButton
                    {
                        Content = "Cambiar",
                        Variant = ButtonVariant.Tertiary,
                        Size = size,
                    },
                    new TouchButton
                    {
                        Content = "Eliminar",
                        Symbol = "delete",
                        Variant = ButtonVariant.Danger,
                        Size = size,
                    }
                )
            );
        }

        column.Children.Add(
            Row(
                new TouchButton
                {
                    Content = "Probar",
                    Symbol = "play_arrow",
                    IsEnabled = false,
                },
                new TouchButton
                {
                    Content = "Restaurar",
                    Variant = ButtonVariant.Secondary,
                    IsEnabled = false,
                },
                new TouchButton
                {
                    Content = "Un texto largo que no cabe en el botón",
                    Variant = ButtonVariant.Secondary,
                    Width = 200,
                },
                new TouchButton
                {
                    Content = "Ajustes",
                    Symbol = "tune",
                    Variant = ButtonVariant.Secondary,
                    IsActive = true,
                }
            )
        );
        return column;
    }

    private static WrapPanel IconButtons() =>
        Row(
            Icon("search", "Buscar"),
            Icon("edit", "Editar", active: true),
            Icon("tune", "Ajustes rápidos", active: true),
            Icon("remove", "Minimizar"),
            Icon("mic", "Dictar", ButtonVariant.Secondary, ControlSize.Medium),
            Icon("undo", "Deshacer", ButtonVariant.Secondary),
            Icon("delete", "Quitar", ButtonVariant.Danger)
        );

    private static IconButton Icon(
        string symbol,
        string name,
        ButtonVariant variant = ButtonVariant.Tertiary,
        ControlSize size = ControlSize.Small,
        bool active = false
    )
    {
        var button = new IconButton
        {
            Symbol = symbol,
            Variant = variant,
            Size = size,
            IsActive = active,
        };
        AutomationProperties.SetName(button, name);
        return button;
    }

    private static StackPanel Options()
    {
        var column = new StackPanel();

        var segments = new SegmentedControl { Width = 300, Margin = new Thickness(0, 0, 0, Space.S3) };
        segments.Items.Add(new SegmentedItem { Content = "Frecuentes", Symbol = "star" });
        segments.Items.Add(new SegmentedItem { Content = "Word", Symbol = "description" });
        segments.SelectedIndex = 1;
        column.Children.Add(segments);

        var sizes = new SegmentedControl
        {
            Kind = OptionKind.Tile,
            Columns = 3,
            Width = 300,
            Margin = new Thickness(-Space.S1, 0, -Space.S1, Space.S2),
        };
        foreach (var label in new[] { "S", "M", "L" })
        {
            sizes.Items.Add(new SegmentedItem { Content = label });
        }

        sizes.SelectedIndex = 1;
        column.Children.Add(sizes);

        var views = new SegmentedControl
        {
            Kind = OptionKind.Card,
            Columns = 3,
            Width = 300,
            Margin = new Thickness(-Space.S1, 0, -Space.S1, Space.S2),
        };
        views.Items.Add(new SegmentedItem { Content = "Completa", Symbol = "view_agenda" });
        views.Items.Add(new SegmentedItem { Content = "Compacta", Symbol = "grid_view" });
        views.Items.Add(new SegmentedItem { Content = "Pestaña", Symbol = "tab" });
        views.SelectedIndex = 0;
        column.Children.Add(views);

        column.Children.Add(
            Row(
                Option("Básicos", OptionKind.Chip, selected: true),
                Option("Word", OptionKind.Chip),
                Option("Chrome", OptionKind.Chip),
                Option("Ctrl", OptionKind.Tile, selected: true, mono: true),
                Option("Alt", OptionKind.Tile, mono: true),
                Option("Shift", OptionKind.Tile, mono: true)
            )
        );
        column.Children.Add(
            Row(
                Option("Pulsar", OptionKind.Card, "touch_app", selected: true, width: 80),
                Option("Mantener", OptionKind.Card, "back_hand", width: 80),
                Option("Alternar", OptionKind.Card, "toggle_on", width: 80),
                Option("Texto", OptionKind.Card, "text_fields", width: 80)
            )
        );

        var lists = new StackPanel { Width = 216, Margin = new Thickness(0, 0, Space.S6, 0) };
        lists.Children.Add(
            Option("Siempre visible", OptionKind.ListRow, "push_pin", caption: "todas las apps")
        );
        lists.Children.Add(
            Option("Word", OptionKind.ListRow, "description", caption: "WINWORD.EXE", selected: true)
        );
        lists.Children.Add(
            Option("Navegador web predeterminado", OptionKind.ListRow, "public", caption: "chrome.exe")
        );
        var rail = new StackPanel { Width = 88 };
        rail.Children.Add(Option("Atajos", OptionKind.Rail, "tune", selected: true, count: 2));
        rail.Children.Add(Option("Plantillas", OptionKind.Rail, "auto_awesome"));
        rail.Children.Add(Option("General y panel", OptionKind.Rail, "desktop_windows"));
        rail.Children.Add(Option("Sistema", OptionKind.Rail, "shield", count: 1));
        var pair = new StackPanel { Orientation = Orientation.Horizontal };
        pair.Children.Add(lists);
        pair.Children.Add(rail);
        column.Children.Add(pair);
        return column;
    }

    private static OptionButton Option(
        string text,
        OptionKind kind,
        string? symbol = null,
        bool selected = false,
        bool mono = false,
        string? caption = null,
        int count = 0,
        double width = double.NaN
    ) =>
        new()
        {
            Content = text,
            Kind = kind,
            Symbol = symbol,
            IsSelected = selected,
            IsMonospace = mono,
            Caption = caption,
            Count = count,
            Width = width,
        };

    private static StackPanel Switches()
    {
        var column = new StackPanel { Width = 360, HorizontalAlignment = HorizontalAlignment.Left };
        column.Children.Add(
            new ToggleSwitch { Content = "Atenuar cuando no lo uso", Symbol = "opacity" }
        );
        column.Children.Add(
            new ToggleSwitch
            {
                Content = "Números para voz",
                Symbol = "pin",
                Description = "Muestra un número en cada botón para decir «clic 4».",
                IsChecked = true,
            }
        );
        var slider = new StepSlider
        {
            Minimum = 30,
            Maximum = 100,
            SmallChange = 2,
            Value = 92,
            DecreaseName = "Bajar opacidad",
            IncreaseName = "Subir opacidad",
            Margin = new Thickness(0, Space.S2, 0, 0),
        };
        AutomationProperties.SetName(slider, "Opacidad");
        column.Children.Add(slider);
        return column;
    }

    private static StackPanel Fields()
    {
        var column = new StackPanel { Width = 420, HorizontalAlignment = HorizontalAlignment.Left };
        var name = new TextInput("Nombre", "Nombre del atajo") { DictateName = "Dictar nombre" };
        name.Show("Negrita");
        column.Children.Add(name);
        var search = new TextInput("Buscar", "Buscar acción en todos los perfiles…")
        {
            Symbol = "search",
            DictateName = "Dictar",
            Margin = new Thickness(0, Space.S3, 0, 0),
        };
        column.Children.Add(search);
        var warn = new TextInput("Dirección", "https://") { Margin = new Thickness(0, Space.S3, 0, 0) };
        warn.Show("htp:/ejemplo");
        warn.Warn(true);
        column.Children.Add(warn);
        return column;
    }

    private static StackPanel Navigation()
    {
        var column = new StackPanel { Width = 420, HorizontalAlignment = HorizontalAlignment.Left };
        column.Children.Add(
            new NavRow
            {
                Content = "Centro de control",
                Caption = "Atajos, plantillas, columnas, idioma y más",
                Symbol = "settings",
                Margin = new Thickness(0, 0, 0, Space.S3),
            }
        );
        var review = new TouchButton
        {
            Content = "Revisar",
            Variant = ButtonVariant.Tertiary,
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        var warnBody = new StackPanel();
        warnBody.Children.Add(
            new StyledText("2 combinaciones repetidas entre perfiles", TextStyleToken.Label)
            {
                MaxLines = 2,
            }
        );
        warnBody.Children.Add(review);
        column.Children.Add(
            new Card
            {
                Tone = CardTone.Warn,
                Content = warnBody,
                Margin = new Thickness(0, 0, 0, Space.S3),
            }
        );
        var neutral = new StackPanel();
        neutral.Children.Add(new StyledText("Se activa solo con Word", TextStyleToken.Label));
        neutral.Children.Add(
            new StyledText("WINWORD.EXE", TextStyleToken.Caption, ColorToken.Muted)
        );
        column.Children.Add(new Card { Content = neutral });
        return column;
    }

    private static WrapPanel Tiles()
    {
        var row = new WrapPanel();
        row.Children.Add(Tile("Negrita", "format_bold", "Ctrl+N", CategoryToken.Fmt, 1));
        row.Children.Add(Tile("Cursiva", "format_italic", "Ctrl+K", CategoryToken.Fmt, 2));
        var pdf = Tile("Guardar PDF", "picture_as_pdf", "5 pasos", CategoryToken.File, null);
        pdf.TypeSymbol = "format_list_bulleted";
        row.Children.Add(pdf);
        var on = Tile("Mayúsculas", "keyboard_capslock", "Bloq Mayús", CategoryToken.Text, null);
        on.Pattern = ShortcutTilePattern.Toggle;
        on.ToggleState = ToggleState.On;
        row.Children.Add(on);
        var armed = Tile("Cerrar ventana", "close", "Alt+F4", CategoryToken.Win, null);
        armed.IsArmed = true;
        row.Children.Add(armed);
        row.Children.Add(
            Tile("Reemplazar en todo el documento", "find_replace", "Ctrl+Shift+L", CategoryToken.Edit, null)
        );
        var strip = Tile("Dictar", "mic", string.Empty, CategoryToken.Voice, null);
        strip.Height = ControlHeight.Large;
        strip.Width = 72;
        strip.FontSize = TextStyles.LabelSmall.Size;
        row.Children.Add(strip);
        return row;
    }

    private static ShortcutTile Tile(
        string name,
        string symbol,
        string keys,
        CategoryToken category,
        int? number
    ) =>
        new()
        {
            AccessibleName = name,
            Symbol = symbol,
            Keys = keys,
            Category = category,
            VoiceNumber = number,
            Width = 100,
            Height = 80,
            FontSize = TextStyles.Label.Size,
            KeysFontSize = TextStyles.KeysTile.Size,
            Padding = new Thickness(Space.S2, 0, Space.S2, 0),
            Margin = new Thickness(0, 0, Space.S2, Space.S2),
        };

    private static StackPanel Status()
    {
        var column = new StackPanel();
        var undo = new IconButton { Symbol = "undo" };
        AutomationProperties.SetName(undo, "Deshacer");
        column.Children.Add(
            new StatusBar
            {
                Text = "Ctrl+N enviado a Word",
                Tone = StatusTone.Success,
                Action = undo,
                Width = 340,
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(0, 0, 0, Space.S3),
            }
        );
        column.Children.Add(
            new StatusBar
            {
                Text = "Listo. Toca un botón para usarlo.",
                Width = 340,
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(0, 0, 0, Space.S3),
            }
        );
        column.Children.Add(
            Row(
                new Badge { Text = "Incompleto" },
                new Badge { Text = "Nuevo", Tone = BadgeTone.Accent },
                new Badge { Text = "Repetida", Tone = BadgeTone.Warn },
                new Counter { Count = 1 },
                new Counter { Count = 12 }
            )
        );
        return column;
    }

    private static Border Scroll()
    {
        var list = new StackPanel { Margin = new Thickness(0, 0, Space.S1, 0) };
        foreach (
            var label in new[]
            {
                "Atenuar cuando no lo uso",
                "Números para voz",
                "Sonido al pulsar",
                "Vibración",
                "Mostrar teclas",
                "Modo compatible",
            }
        )
        {
            list.Children.Add(new ToggleSwitch { Content = label, Symbol = "check_box" });
        }

        var scroll = new ThemedScrollViewer { Content = list, Height = 180 };
        var frame = new Border
        {
            Width = 360,
            HorizontalAlignment = HorizontalAlignment.Left,
            Padding = new Thickness(Space.S3, 0, 0, 0),
            Child = scroll,
        };
        frame.SetResourceReference(Border.BackgroundProperty, ThemeBrushKey.For(ColorToken.Panel));
        return frame;
    }
}
