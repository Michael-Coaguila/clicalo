using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Clicalo.Domain.Primitives;
using Clicalo.Presentation.ControlCenter.Templates;
using Clicalo.UI.Wpf.Automation;
using Clicalo.UI.Wpf.Controls;
using Clicalo.UI.Wpf.Theming;
using Clicalo.UI.Wpf.Theming.Generated;
using Clicalo.UI.Wpf.Workspace.Internal;

namespace Clicalo.UI.Wpf.Workspace.Templates;

/// <summary>
/// The section Plantillas (docs/05 §2): the content (flexible) and the preview (340, or 260 when narrow). It only draws
/// <see cref="TemplatesSectionViewModel.Screen"/> and forwards taps; each region is rebuilt only when its part of the
/// screen changed, and the text fields live across rebuilds so typing is never interrupted. The key field is a
/// password box: the key is never shown, not even while it is pasted (PLA-003, ADR-0008).
/// </summary>
public sealed class TemplatesSectionView : Grid
{
    private const double WidePreview = 340;
    private const double NarrowPreview = 260;

    private readonly TemplatesSectionViewModel _viewModel;
    private readonly ContentControl _header = new() { Focusable = false };
    private readonly ContentControl _ai = new() { Focusable = false };
    private readonly ContentControl _blank = new() { Focusable = false };
    private readonly ContentControl _suggested = new() { Focusable = false };
    private readonly ContentControl _available = new() { Focusable = false };
    private readonly ContentControl _installed = new() { Focusable = false };
    private readonly ContentControl _preview = new() { Focusable = false };
    private readonly ColumnDefinition _previewColumn = new()
    {
        Width = new GridLength(WidePreview),
    };
    private readonly TextField _query;
    private readonly TextField _blankName;
    private readonly TextField _rowName;
    private readonly PasswordBox _key = new()
    {
        Background = Brushes.Transparent,
        BorderThickness = new Thickness(0),
        Padding = new Thickness(10, 0, 10, 0),
        VerticalContentAlignment = VerticalAlignment.Center,
        MinHeight = 42,
        FocusVisualStyle = null,
    };
    private readonly LiveAnnouncer _announcer;
    private TemplatesScreen? _shown;
    private int? _renaming;

    /// <summary>Creates the view of <paramref name="viewModel"/>.</summary>
    /// <param name="viewModel">The section.</param>
    public TemplatesSectionView(TemplatesSectionViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        _viewModel = viewModel;
        _announcer = new LiveAnnouncer(this);
        _query = new TextField(string.Empty, string.Empty) { Height = 52 };
        _query.Changed += (_, _) => _viewModel.SetQuery(_query.Text);
        _query.Box.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter && _shown?.Ai.CanGenerate == true)
            {
                e.Handled = true;
                _ = _viewModel.GenerateAsync();
            }
        };
        _blankName = new TextField(string.Empty, string.Empty) { Height = 48 };
        _blankName.Changed += (_, _) => _viewModel.SetBlankName(_blankName.Text);
        _rowName = new TextField(string.Empty, string.Empty);
        _rowName.Changed += (_, _) =>
        {
            if (_renaming is { } index)
            {
                _viewModel.RenameRow(index, _rowName.Text);
            }
        };
        _key.SetResourceReference(PasswordBox.FontSizeProperty, ThemeKeys.TextSize(15));
        Ui.Ink(_key, PasswordBox.ForegroundProperty, ColorToken.Text);
        Ui.Ink(_key, PasswordBox.CaretBrushProperty, ColorToken.Text);

        ColumnDefinitions.Add(
            new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MinWidth = 320 }
        );
        ColumnDefinitions.Add(_previewColumn);
        var content = Ui.Column(24, _header, _ai, _blank, _suggested, _available, _installed);
        Children.Add(new TouchPanScrollViewer { Content = content, Padding = new Thickness(24) });
        var preview = new Border
        {
            Child = new TouchPanScrollViewer { Content = _preview, Padding = new Thickness(20) },
            BorderThickness = new Thickness(1, 0, 0, 0),
        };
        Ui.Ink(preview, Border.BackgroundProperty, ColorToken.Side);
        Ui.Ink(preview, Border.BorderBrushProperty, ColorToken.Border);
        SetColumn(preview, 1);
        Children.Add(preview);
        viewModel.PropertyChanged += OnChanged;
        Render();
    }

    /// <summary>Below 1240 the preview narrows to 260 (PLA-001).</summary>
    /// <param name="narrow">Whether the window is narrow.</param>
    public void SetNarrow(bool narrow) =>
        _previewColumn.Width = new GridLength(narrow ? NarrowPreview : WidePreview);

    private static void Detach(FrameworkElement element)
    {
        switch (element.Parent)
        {
            case Panel panel:
                panel.Children.Remove(element);
                break;
            case Decorator decorator:
                decorator.Child = null;
                break;
            case ContentControl content:
                content.Content = null;
                break;
        }
    }

    private static CcButton Primary(string? icon, string text, Action click, double height = 44)
    {
        var button = Ui.Button(
            Ui.IconLabel(
                icon,
                text,
                ink: ColorToken.OnAccent,
                iconInk: ColorToken.OnAccent,
                px: 14
            ),
            text,
            click,
            ColorToken.Accent,
            ColorToken.OnAccent,
            height: height
        );
        button.BorderThickness = new Thickness(0);
        return button;
    }

    private static CcButton Secondary(
        string? icon,
        string text,
        Action click,
        double height = 44
    ) =>
        Ui.Button(
            Ui.IconLabel(icon, text, px: 13),
            text,
            click,
            ColorToken.Card,
            stroke: ColorToken.Border,
            height: height
        );

    private static Border IconTile(string icon, double size, ColorToken fill, ColorToken ink)
    {
        var tile = Ui.Card(Ui.Icon(icon, size * 0.55, ink), fill, null, 12, new Thickness(0));
        tile.Width = size;
        tile.Height = size;
        return tile;
    }

    private static Panel MiniIcons(IEnumerable<string> icons, double size)
    {
        var row = Ui.Row(4);
        foreach (var icon in icons)
        {
            var tile = Ui.Card(
                Ui.Icon(icon, size * 0.6, ColorToken.Muted),
                ColorToken.Side,
                null,
                6,
                new Thickness(0)
            );
            tile.Width = size;
            tile.Height = size;
            row.Children.Add(tile);
        }

        return row;
    }

    private static DockPanel Leading(double gap, FrameworkElement lead, UIElement fill)
    {
        var dock = new DockPanel { LastChildFill = true };
        lead.Margin = new Thickness(0, 0, gap, 0);
        lead.VerticalAlignment = VerticalAlignment.Top;
        DockPanel.SetDock(lead, Dock.Left);
        dock.Children.Add(lead);
        dock.Children.Add(fill);
        return dock;
    }

    private static TextBlock SectionTitle(string text)
    {
        var title = Ui.Text(text.ToUpperInvariant(), 13, bold: true, ink: ColorToken.Muted);
        AutomationProperties.SetHeadingLevel(title, AutomationHeadingLevel.Level3);
        return title;
    }

    private void OnChanged(object? sender, PropertyChangedEventArgs e) => Render();

    private void Render()
    {
        var screen = _viewModel.Screen;
        var before = _shown;
        _shown = screen;
        if (
            before is null
            || !string.Equals(before.Title, screen.Title, StringComparison.Ordinal)
            || !string.Equals(before.Subtitle, screen.Subtitle, StringComparison.Ordinal)
        )
        {
            var title = Ui.Text(screen.Title, 24, bold: true, wrap: true);
            AutomationProperties.SetHeadingLevel(title, AutomationHeadingLevel.Level2);
            _header.Content = Ui.Column(
                4,
                title,
                Ui.Text(screen.Subtitle, 14, ink: ColorToken.Muted, wrap: true)
            );
        }

        if (before?.Ai != screen.Ai)
        {
            _ai.Content = AiCard(screen.Ai);
            if (screen.Ai.Error is { } error && before?.Ai.Error != error)
            {
                _announcer.Announce(error.Title + ". " + error.Text, AnnouncementUrgency.Assertive);
            }
        }

        if (before?.Blank != screen.Blank)
        {
            _blank.Content = Blank(screen.Blank);
        }

        if (before?.Suggested != screen.Suggested)
        {
            _suggested.Content = Suggested(screen.Suggested);
        }

        if (
            before?.Available != screen.Available
            || !string.Equals(
                before.AllInstalledText,
                screen.AllInstalledText,
                StringComparison.Ordinal
            )
            || !string.Equals(
                before.AvailableTitle,
                screen.AvailableTitle,
                StringComparison.Ordinal
            )
        )
        {
            _available.Content = Available(screen);
        }

        if (before?.Installed != screen.Installed)
        {
            _installed.Content = Installed(screen.Installed);
        }

        if (before?.Preview != screen.Preview)
        {
            _preview.Content = Preview(screen.Preview);
        }
    }

    // ---- Crear con IA ---------------------------------------------------------------------------------------

    private Border AiCard(AiCardModel model)
    {
        var heading = Ui.Text(model.Title, 18, bold: true, wrap: true);
        AutomationProperties.SetHeadingLevel(heading, AutomationHeadingLevel.Level3);
        var top = Leading(
            12,
            IconTile("auto_awesome", 44, ColorToken.Accent, ColorToken.OnAccent),
            Ui.Column(2, heading, Ui.Text(model.Description, 13, ink: ColorToken.Muted, wrap: true))
        );

        Detach(_query);
        AutomationProperties.SetName(_query.Box, model.Placeholder);
        _query.Show(model.Query);
        _query.SetPlaceholder(model.Placeholder);
        var mic = Ui.Button(
            Ui.Icon("mic", 24, ColorToken.Accent),
            model.DictateName,
            () => FocusAndDictate(_query),
            ColorToken.Field,
            height: 52,
            radius: 12
        );
        mic.Width = 52;
        mic.Padding = new Thickness(0);
        var generate = Primary(null, model.GenerateText, () => _ = _viewModel.GenerateAsync(), 52);
        generate.IsEnabled = model.CanGenerate;
        var field = new DockPanel { LastChildFill = true };
        DockPanel.SetDock(generate, Dock.Right);
        DockPanel.SetDock(mic, Dock.Right);
        generate.Margin = new Thickness(8, 0, 0, 0);
        mic.Margin = new Thickness(8, 0, 0, 0);
        field.Children.Add(generate);
        field.Children.Add(mic);
        field.Children.Add(_query);

        var examples = Ui.Wrap(
            6,
            model.Examples.Items.Select(name =>
            {
                var chip = Ui.Button(
                    Ui.Text(name, 13),
                    name,
                    () =>
                    {
                        _query.Show(name, force: true);
                        _viewModel.UseExample(name);
                    },
                    ColorToken.Field,
                    stroke: ColorToken.Border,
                    radius: 22
                );
                return (UIElement)chip;
            })
        );

        var privacy = Ui.Row(
            6,
            Ui.Icon("shield", 16, ColorToken.Muted),
            Ui.Text(model.Privacy, 12, ink: ColorToken.Muted, wrap: true)
        );
        var keyButton = Secondary(null, model.KeyButton, _viewModel.ToggleKey);
        var keyLine = new DockPanel { LastChildFill = true };
        DockPanel.SetDock(keyButton, Dock.Right);
        keyButton.Margin = new Thickness(8, 0, 0, 0);
        keyLine.Children.Add(keyButton);
        keyLine.Children.Add(
            Ui.Row(
                6,
                Ui.Icon("bolt", 16, ColorToken.Muted),
                Ui.Text(model.KeyStatus, 12, ink: ColorToken.Muted, wrap: true)
            )
        );

        var column = Ui.Column(12, top, field, examples, privacy, keyLine);
        if (model.KeyFieldOpen)
        {
            column.Children.Add(KeyField(model));
        }
        else
        {
            _key.Clear();
        }

        if (model.Consent is { } consent)
        {
            column.Children.Add(Consent(consent));
        }

        if (model.Error is { } error)
        {
            column.Children.Add(Error(error));
        }

        column.Children.Add(KeyboardLine(model.Keyboard));
        foreach (var child in column.Children.OfType<FrameworkElement>().Skip(1))
        {
            child.Margin = new Thickness(0, 12, 0, 0);
        }

        return Ui.Card(column, ColorToken.AccentWash, ColorToken.Accent, 16, new Thickness(18));
    }

    private StackPanel KeyField(AiCardModel model)
    {
        Detach(_key);
        AutomationProperties.SetName(_key, model.KeyPlaceholder);
        var box = Ui.Card(_key, ColorToken.Field, ColorToken.Border, 10, new Thickness(0));
        var paste = Secondary("content_paste", model.PasteText, PasteKey);
        var done = Primary(
            null,
            model.DoneText,
            () =>
            {
                var key = _key.Password;
                _key.Clear();
                _viewModel.SaveKey(key);
            }
        );
        var row = new DockPanel { LastChildFill = true };
        DockPanel.SetDock(done, Dock.Right);
        DockPanel.SetDock(paste, Dock.Right);
        done.Margin = new Thickness(6, 0, 0, 0);
        paste.Margin = new Thickness(6, 0, 0, 0);
        row.Children.Add(done);
        row.Children.Add(paste);
        row.Children.Add(box);
        var hint = Ui.Text(model.KeyPlaceholder, 12, ink: ColorToken.Muted);
        var column = Ui.Column(6, hint, row);
        if (model.DeleteKeyText is { } delete)
        {
            var remove = Ui.Button(
                Ui.IconLabel(
                    "key_off",
                    delete,
                    ink: ColorToken.DangerText,
                    iconInk: ColorToken.DangerText
                ),
                delete,
                _viewModel.DeleteKey,
                ColorToken.DangerWash
            );
            remove.HorizontalAlignment = HorizontalAlignment.Left;
            column.Children.Add(remove);
        }

        _ = Dispatcher.BeginInvoke(() => Keyboard.Focus(_key));
        return column;
    }

    private void PasteKey()
    {
        try
        {
            if (Clipboard.ContainsText())
            {
                _key.Password = Clipboard.GetText().Trim();
            }
        }
        catch (ExternalException)
        {
            // The clipboard is busy: the person can paste again.
        }

        _ = Keyboard.Focus(_key);
    }

    private Border Consent(ConsentModel model)
    {
        var ok = Primary(null, model.AcceptText, () => _ = _viewModel.AcceptConsentAsync());
        var no = Secondary(null, model.DeclineText, _viewModel.DeclineConsent);
        var column = Ui.Column(
            10,
            Ui.Text(model.Title, 14, bold: true, wrap: true),
            Ui.Text(model.Text, 13, ink: ColorToken.Muted, wrap: true),
            Ui.Columns(2, 6, [ok, no])
        );
        return Ui.Card(column, ColorToken.Field, ColorToken.Accent, 12, new Thickness(12));
    }

    private Border Error(AiErrorModel model)
    {
        var buttons = model
            .Actions.Items.Select(action =>
            {
                var button = Ui.Button(
                    Ui.Text(action.Label, 12, bold: true, wrap: true),
                    action.Label,
                    () => _ = _viewModel.ErrorActionAsync(action.Kind),
                    ColorToken.Card,
                    stroke: ColorToken.Border
                );
                button.Height = double.NaN;
                button.MinHeight = 44;
                return (UIElement)button;
            })
            .ToList();
        var column = Ui.Column(
            10,
            Ui.Row(
                8,
                Ui.Icon(model.Icon, 20, ColorToken.Warn),
                Ui.Text(model.Title, 14, bold: true, wrap: true)
            ),
            Ui.Text(model.Text, 13, wrap: true),
            Ui.Columns(3, 6, buttons)
        );
        var card = Ui.Card(column, ColorToken.WarnWash, ColorToken.Warn, 12, new Thickness(12));
        AutomationProperties.SetLiveSetting(card, AutomationLiveSetting.Assertive);
        AutomationProperties.SetName(card, model.Title);
        return card;
    }

    private UIElement KeyboardLine(KeyboardModel model)
    {
        var line = new DockPanel { LastChildFill = true };
        var action = Ui.Text(model.ActionText, 12, bold: true, ink: ColorToken.Accent);
        DockPanel.SetDock(action, Dock.Right);
        var icon = Ui.Icon("keyboard", 22, ColorToken.Accent);
        icon.Margin = new Thickness(0, 0, 10, 0);
        DockPanel.SetDock(icon, Dock.Left);
        line.Children.Add(action);
        line.Children.Add(icon);
        line.Children.Add(
            Ui.Column(
                1,
                Ui.Text(model.Line, 13, bold: true, wrap: true),
                Ui.Text(model.Why, 12, ink: ColorToken.Muted, wrap: true)
            )
        );
        var toggle = Ui.Choice(
            line,
            model.Line,
            model.Open,
            _viewModel.ToggleKeyboard,
            height: double.NaN,
            radius: 12,
            offFill: ColorToken.Field
        );
        toggle.MinHeight = 48;
        toggle.Padding = new Thickness(12, 6, 12, 6);
        toggle.HorizontalContentAlignment = HorizontalAlignment.Stretch;
        AutomationProperties.SetHelpText(toggle, model.Why);
        if (!model.Open)
        {
            return toggle;
        }

        var options = Ui.Columns(
            2,
            10,
            [
                Options(model.LayoutTitle, model.Layouts, model.DetectedText, _viewModel.SetLayout),
                Options(
                    model.AppsTitle,
                    model.AppsLanguages,
                    model.DetectedText,
                    _viewModel.SetAppsLanguage
                ),
            ]
        );
        var panel = Ui.Card(options, ColorToken.Field, ColorToken.Border, 12, new Thickness(12));
        return Ui.Column(8, toggle, panel);
    }

    private static StackPanel Options(
        string title,
        ValueList<KbOption> options,
        string detected,
        Action<string> choose
    )
    {
        var column = Ui.Column(6, Ui.Caption(title));
        foreach (var option in options)
        {
            var content = new DockPanel { LastChildFill = true };
            if (option.Detected)
            {
                var tag = Ui.Text(detected, 11, bold: true, ink: ColorToken.Accent);
                DockPanel.SetDock(tag, Dock.Right);
                content.Children.Add(tag);
            }

            content.Children.Add(Ui.Text(option.Label, 13, wrap: true));
            var button = Ui.Choice(
                content,
                option.Detected ? option.Label + ", " + detected : option.Label,
                option.Selected,
                () => choose(option.Id),
                radius: 9,
                offFill: null
            );
            button.HorizontalContentAlignment = HorizontalAlignment.Stretch;
            button.Padding = new Thickness(10, 0, 10, 0);
            button.Margin = new Thickness(0, 6, 0, 0);
            column.Children.Add(button);
        }

        return column;
    }

    // ---- Perfil vacío ---------------------------------------------------------------------------------------

    private Border Blank(BlankModel model)
    {
        var header = new DockPanel { LastChildFill = true };
        var caret = Ui.Icon(model.Open ? "expand_less" : "expand_more", 22, ColorToken.Muted);
        DockPanel.SetDock(caret, Dock.Right);
        var tile = IconTile("add_circle", 40, ColorToken.Side, ColorToken.Accent);
        tile.Margin = new Thickness(0, 0, 12, 0);
        DockPanel.SetDock(tile, Dock.Left);
        header.Children.Add(caret);
        header.Children.Add(tile);
        header.Children.Add(
            Ui.Column(
                2,
                Ui.Text(model.Title, 15, bold: true, wrap: true),
                Ui.Text(model.Subtitle, 12, ink: ColorToken.Muted, wrap: true)
            )
        );
        var toggle = Ui.Choice(
            header,
            model.Title,
            model.Open,
            _viewModel.ToggleBlank,
            height: double.NaN,
            radius: 14,
            offFill: null
        );
        toggle.MinHeight = 60;
        toggle.Padding = new Thickness(14, 8, 14, 8);
        toggle.BorderThickness = new Thickness(0);
        toggle.HorizontalContentAlignment = HorizontalAlignment.Stretch;
        CcChrome.Paint(toggle, null, ColorToken.Text, null);
        AutomationProperties.SetHelpText(toggle, model.Subtitle);

        var column = Ui.Column(10, toggle);
        if (model.Open)
        {
            column.Children.Add(BlankForm(model));
        }

        return Ui.Card(column, ColorToken.Card, ColorToken.Border, 14, new Thickness(0));
    }

    private StackPanel BlankForm(BlankModel model)
    {
        var icon = Ui.Choice(
            Ui.Icon(model.Icon, 26, ColorToken.Accent),
            model.ChangeIconName,
            model.IconsOpen,
            _viewModel.ToggleBlankIcons,
            48,
            12,
            ColorToken.Side
        );
        icon.Width = 48;
        icon.Padding = new Thickness(0);
        Detach(_blankName);
        AutomationProperties.SetName(_blankName.Box, model.Placeholder);
        _blankName.SetPlaceholder(model.Placeholder);
        _blankName.Show(model.Name);
        var mic = Ui.Button(
            Ui.Icon("mic", 22, ColorToken.Accent),
            model.DictateName,
            () => FocusAndDictate(_blankName),
            ColorToken.AccentWash,
            height: 48
        );
        mic.Width = 48;
        mic.Padding = new Thickness(0);
        var row = new DockPanel { LastChildFill = true };
        DockPanel.SetDock(icon, Dock.Left);
        DockPanel.SetDock(mic, Dock.Right);
        icon.Margin = new Thickness(0, 0, 8, 0);
        mic.Margin = new Thickness(8, 0, 0, 0);
        row.Children.Add(icon);
        row.Children.Add(mic);
        row.Children.Add(_blankName);

        var column = Ui.Column(10, row);
        if (model.IconsOpen)
        {
            var grid = new AutoFillGrid
            {
                MinItemWidth = 44,
                ItemHeight = 44,
                Gap = 4,
            };
            foreach (var option in model.Icons)
            {
                var button = Ui.Choice(
                    Ui.Icon(option.Icon, 20),
                    option.Icon,
                    option.Selected,
                    () => _viewModel.SetBlankIcon(option.Icon),
                    44,
                    8
                );
                button.Padding = new Thickness(0);
                grid.Children.Add(button);
            }

            column.Children.Add(new TouchPanScrollViewer { Content = grid, MaxHeight = 140 });
        }

        column.Children.Add(Ui.Caption(model.LinkTitle));
        column.Children.Add(
            Ui.Wrap(
                6,
                model.Links.Items.Select(link =>
                    (UIElement)
                        Ui.Choice(
                            Ui.IconLabel(link.Icon, link.Label),
                            link.Label,
                            link.Selected,
                            () => _viewModel.SetBlankLink(link.Id),
                            radius: 22,
                            offFill: ColorToken.Side
                        )
                )
            )
        );
        var create = Primary("add", model.CreateText, _viewModel.CreateBlank, 48);
        create.IsEnabled = model.CanCreate;
        column.Children.Add(create);
        column.Margin = new Thickness(14, 0, 14, 14);
        return column;
    }

    // ---- Apps abiertas, plantillas y perfiles ---------------------------------------------------------------

    private StackPanel Suggested(SuggestedModel model)
    {
        var dot = new Border
        {
            Width = 8,
            Height = 8,
            CornerRadius = new CornerRadius(4),
        };
        Ui.Ink(dot, Border.BackgroundProperty, ColorToken.Warn);
        var knob = new ToggleSwitch
        {
            IsChecked = model.DetectOn,
            IsHitTestVisible = false,
            Focusable = false,
            Margin = new Thickness(10, 0, 0, 0),
        };
        var detect = Ui.Choice(
            Ui.Row(0, Ui.Text(model.DetectText, 13, ink: ColorToken.Muted), knob),
            model.DetectText,
            model.DetectOn,
            _viewModel.ToggleDetect,
            radius: 22,
            offFill: null
        );
        detect.BorderThickness = new Thickness(0);
        detect.Padding = new Thickness(10, 0, 4, 0);
        CcChrome.Paint(detect, null, ColorToken.Text, null);
        var header = new DockPanel { LastChildFill = true };
        DockPanel.SetDock(detect, Dock.Right);
        header.Children.Add(detect);
        header.Children.Add(Ui.Row(8, dot, SectionTitle(model.Title)));
        var column = Ui.Column(10, header);
        foreach (var card in model.Cards)
        {
            var preview = Secondary(
                null,
                model.PreviewText,
                () => _viewModel.PreviewTemplate(card.Id)
            );
            var install = Primary(
                "download",
                model.InstallText,
                () => _viewModel.InstallTemplate(card.Id)
            );
            var info = Leading(
                14,
                IconTile(card.Icon, 48, ColorToken.Side, ColorToken.Accent),
                Ui.Column(
                    6,
                    Ui.Text(card.Meta, 16, bold: true, wrap: true),
                    MiniIcons(card.Icons.Items, 24)
                )
            );
            var body = Ui.Column(12, info, Ui.Columns(2, 8, [preview, install]));
            column.Children.Add(
                Ui.Card(
                    body,
                    ColorToken.WarnWash,
                    ColorToken.Warn,
                    14,
                    new Thickness(16, 14, 16, 14)
                )
            );
        }

        if (model.EmptyText is { } empty)
        {
            column.Children.Add(
                Ui.Card(
                    Ui.Text(empty, 13, ink: ColorToken.Muted, wrap: true),
                    ColorToken.Card,
                    null,
                    12,
                    new Thickness(14, 12, 14, 12)
                )
            );
        }

        return column;
    }

    private StackPanel Available(TemplatesScreen screen)
    {
        var column = Ui.Column(10, SectionTitle(screen.AvailableTitle));
        if (screen.AllInstalledText is { } all)
        {
            column.Children.Add(
                Ui.Card(
                    Ui.Text(all, 13, ink: ColorToken.Muted, wrap: true),
                    ColorToken.Card,
                    null,
                    12,
                    new Thickness(14, 12, 14, 12)
                )
            );
            return column;
        }

        var grid = new AutoFillGrid { MinItemWidth = 200, Gap = 10 };
        foreach (var card in screen.Available)
        {
            var install = Primary(
                "download",
                screen.InstallText,
                () => _viewModel.InstallTemplate(card.Id)
            );
            AutomationProperties.SetName(install, screen.InstallText + " " + card.Name);
            var top = Leading(
                10,
                IconTile(card.Icon, 44, ColorToken.Side, ColorToken.Accent),
                Ui.Column(
                    2,
                    Ui.Text(card.Name, 16, bold: true),
                    Ui.Text(card.Meta, 12, ink: ColorToken.Muted)
                )
            );
            var open = Ui.Choice(
                Ui.Column(12, top, MiniIcons(card.Icons.Items, 28)),
                card.Name + ", " + card.Meta,
                card.Selected,
                () => _viewModel.PreviewTemplate(card.Id),
                height: double.NaN,
                radius: 14
            );
            open.Padding = new Thickness(14, 14, 14, 8);
            open.HorizontalContentAlignment = HorizontalAlignment.Stretch;
            open.BorderThickness = new Thickness(card.Selected ? 2 : 0);
            install.Margin = new Thickness(14, 4, 14, 14);
            grid.Children.Add(
                Ui.Card(
                    Ui.Column(0, open, install),
                    card.Selected ? ColorToken.CardHi : ColorToken.Card,
                    card.Selected ? ColorToken.Accent : ColorToken.Border,
                    14,
                    new Thickness(0),
                    card.Selected ? 2 : 1
                )
            );
        }

        column.Children.Add(grid);
        return column;
    }

    private StackPanel Installed(InstalledModel model)
    {
        var column = Ui.Column(
            10,
            Ui.Row(8, SectionTitle(model.Title), Ui.Text(model.Count, 13, ink: ColorToken.Muted))
        );
        column.Children.Add(
            Ui.Wrap(
                8,
                model.Profiles.Items.Select(profile =>
                {
                    var content = Ui.Row(
                        8,
                        Ui.Icon(profile.Icon, 20, ColorToken.Accent),
                        Ui.Text(profile.Name, 14, bold: true),
                        Ui.Text(profile.Count, 12, ink: ColorToken.Muted),
                        Ui.Icon("chevron_right", 18, ColorToken.Muted)
                    );
                    var button = Ui.Button(
                        content,
                        profile.Name + ", " + profile.Count,
                        () => _viewModel.OpenInstalled(profile.Id),
                        ColorToken.Card,
                        stroke: ColorToken.Border,
                        height: 48,
                        radius: 12
                    );
                    button.Padding = new Thickness(10, 0, 8, 0);
                    return (UIElement)button;
                })
            )
        );
        var import = Secondary("download", model.ImportText, () => _ = _viewModel.ImportAsync());
        column.Children.Add(
            Ui.Row(8, import, Ui.Text(model.ShareHint, 12, ink: ColorToken.Muted, wrap: true))
        );
        return column;
    }

    // ---- Vista previa ---------------------------------------------------------------------------------------

    private StackPanel Preview(PreviewModel model)
    {
        if (!model.HasContent)
        {
            _renaming = null;
            var empty = Ui.Column(
                10,
                Ui.Icon("preview", 40, ColorToken.Muted),
                Ui.Text(model.EmptyText, 14, ink: ColorToken.Muted, wrap: true)
            );
            empty.Margin = new Thickness(10, 80, 10, 0);
            foreach (var child in empty.Children.OfType<FrameworkElement>())
            {
                child.HorizontalAlignment = HorizontalAlignment.Center;
            }

            ((TextBlock)empty.Children[1]).TextAlignment = TextAlignment.Center;
            return empty;
        }

        var heading = Ui.Text(model.Name, 18, bold: true);
        AutomationProperties.SetHeadingLevel(heading, AutomationHeadingLevel.Level3);
        var column = Ui.Column(
            12,
            Leading(
                10,
                IconTile(model.Icon, 44, ColorToken.Card, ColorToken.Accent),
                Ui.Column(
                    2,
                    heading,
                    model.Process.Length == 0
                        ? null
                        : Ui.Text(model.Process, 12, ink: ColorToken.Muted, mono: true)
                )
            )
        );
        if (model.UnknownText is { } unknown)
        {
            var blank = Ui.Button(
                Ui.Text(
                    model.UnknownButton ?? string.Empty,
                    14,
                    bold: true,
                    ink: ColorToken.OnWarn,
                    wrap: true
                ),
                model.UnknownButton ?? string.Empty,
                _viewModel.CreateFromUnknown,
                ColorToken.Warn,
                ColorToken.OnWarn
            );
            blank.Height = double.NaN;
            blank.MinHeight = 44;
            column.Children.Add(
                Ui.Card(
                    Ui.Column(
                        8,
                        Ui.Row(
                            8,
                            Ui.Icon("help", 20, ColorToken.Warn),
                            Ui.Text(unknown, 13, wrap: true)
                        ),
                        blank
                    ),
                    ColorToken.WarnWash,
                    ColorToken.Warn,
                    10,
                    new Thickness(12)
                )
            );
        }

        if (model.InstalledNote is { } note)
        {
            column.Children.Add(
                Ui.Card(
                    Ui.Row(
                        8,
                        Ui.Icon("check_circle", 20, ColorToken.Accent),
                        Ui.Text(note, 13, wrap: true)
                    ),
                    ColorToken.AccentWash,
                    null,
                    10,
                    new Thickness(12, 10, 12, 10)
                )
            );
        }

        column.Children.Add(
            Ui.Row(
                6,
                Ui.Icon("keyboard", 16, ColorToken.Muted),
                Ui.Text(model.KeyboardLine, 12, ink: ColorToken.Muted, wrap: true)
            )
        );
        foreach (var warning in new[] { model.OnlyEsNote, model.TextsNote }.OfType<string>())
        {
            column.Children.Add(
                Ui.Row(
                    6,
                    Ui.Icon("translate", 16, ColorToken.WarnText),
                    Ui.Text(warning, 12, ink: ColorToken.WarnText, wrap: true)
                )
            );
        }

        _renaming = null;
        foreach (var row in model.Rows)
        {
            column.Children.Add(Row(row));
        }

        var finalButton = model.ButtonSecondary
            ? Secondary(model.ButtonIcon, model.ButtonText, _viewModel.InstallPreview, 48)
            : Primary(model.ButtonIcon, model.ButtonText, _viewModel.InstallPreview, 48);
        finalButton.IsEnabled = model.ButtonEnabled;
        column.Children.Add(finalButton);
        return column;
    }

    private Border Row(PreviewRowModel row)
    {
        var text = Ui.Column(
            2,
            Ui.Text(row.Name, 14, bold: true, wrap: true),
            Ui.Text(row.Foot, 11, ink: ColorToken.Muted, mono: true, wrap: true),
            row.Warning is null
                ? null
                : Ui.Row(
                    4,
                    Ui.Icon("warning", 14, ColorToken.WarnText),
                    Ui.Text(row.Warning, 11, ink: ColorToken.WarnText, wrap: true)
                )
        );
        var check = Ui.Icon(
            row.AlreadyIn ? "check_circle"
                : row.Checked ? "check_box"
                : "check_box_outline_blank",
            22,
            row.AlreadyIn || row.Checked ? ColorToken.Accent : ColorToken.Muted
        );
        check.Margin = new Thickness(0, 0, 10, 0);
        var icon = Ui.Icon(row.Icon, 18, ColorToken.Muted);
        icon.Margin = new Thickness(0, 0, 10, 0);
        var content = new DockPanel { LastChildFill = true };
        DockPanel.SetDock(check, Dock.Left);
        DockPanel.SetDock(icon, Dock.Left);
        content.Children.Add(check);
        content.Children.Add(icon);
        content.Children.Add(text);
        var toggle = Ui.Choice(
            content,
            row.Name + ", " + row.Foot,
            row.Checked,
            () => _viewModel.ToggleRow(row.Index),
            height: double.NaN,
            radius: 10,
            offFill: null
        );
        toggle.MinHeight = 48;
        toggle.BorderThickness = new Thickness(0);
        toggle.Padding = new Thickness(10, 6, 4, 6);
        toggle.HorizontalContentAlignment = HorizontalAlignment.Stretch;
        CcChrome.Paint(toggle, null, ColorToken.Text, null);
        toggle.IsEnabled = !row.AlreadyIn;
        AutomationProperties.SetHelpText(toggle, row.Warning ?? string.Empty);

        var line = new DockPanel { LastChildFill = true };
        if (!row.AlreadyIn)
        {
            var edit = Ui.Button(
                Ui.Icon("edit", 18, ColorToken.Muted),
                row.RenameName + ": " + row.Name,
                () => _viewModel.EditRow(row.Index),
                null
            );
            edit.Width = 44;
            edit.Padding = new Thickness(0);
            edit.BorderThickness = new Thickness(0);
            DockPanel.SetDock(edit, Dock.Right);
            line.Children.Add(edit);
        }

        line.Children.Add(toggle);
        var column = Ui.Column(0, line);
        if (row.Editing)
        {
            _renaming = row.Index;
            Detach(_rowName);
            AutomationProperties.SetName(_rowName.Box, row.RenameName);
            _rowName.Show(row.Name, force: true);
            _rowName.Margin = new Thickness(10, 0, 10, 10);
            column.Children.Add(_rowName);
            _ = Dispatcher.BeginInvoke(() =>
            {
                _ = Keyboard.Focus(_rowName.Box);
                _rowName.Box.SelectAll();
            });
        }

        var card = Ui.Card(column, ColorToken.Card, ColorToken.Border, 10, new Thickness(0));
        card.Opacity =
            row.AlreadyIn ? 0.55
            : row.Checked ? 1
            : 0.75;
        return card;
    }

    private void FocusAndDictate(TextField field)
    {
        _ = Keyboard.Focus(field.Box);
        _viewModel.Dictate();
    }
}
