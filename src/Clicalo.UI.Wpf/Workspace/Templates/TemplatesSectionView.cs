using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Clicalo.Presentation.ControlCenter.Templates;
using Clicalo.UI.Wpf.Automation;
using Clicalo.UI.Wpf.Controls;
using Clicalo.UI.Wpf.Theming;
using Clicalo.UI.Wpf.Theming.Generated;
using Clicalo.UI.Wpf.Workspace.Editor;
using Clicalo.UI.Wpf.Workspace.Internal;

namespace Clicalo.UI.Wpf.Workspace.Templates;

/// <summary>
/// The section Plantillas (docs/05 §2) in direction C (D12): one column that scrolls with «Crea atajos con IA» (compact,
/// so the templates are in view), the keyboard the shortcuts are made for, «Perfil vacío», the apps open without a
/// profile, the templates and the installed profiles; and the preview (<see cref="PreviewWidth"/>) only while a
/// template or a proposal is in it, with its install button fixed at its foot. Where both do not fit (the smallest
/// window, CCM-005), the preview is a page of its own with ✕. The icon picker of «Perfil vacío» is a layer (D14).
/// </summary>
/// <remarks>
/// It only draws <see cref="TemplatesSectionViewModel.Screen"/> and forwards taps; each region is rebuilt only when its
/// part of the screen changed, and the text fields live across rebuilds so typing is never interrupted. The key field
/// is a password box: the key is never shown, not even while it is pasted (PLA-003, ADR-0008).
/// </remarks>
public sealed class TemplatesSectionView : Grid
{
    /// <summary>The width of the preview: the width of the editor of «Atajos».</summary>
    public const double PreviewWidth = ShortcutsSectionView.EditorWidth;

    /// <summary>The least width of the content beside the preview: two templates side by side.</summary>
    private const double ContentMinWidth = ShortcutsSectionView.ProfilesWidth * 2 + Space.S6 * 2;

    private readonly TemplatesSectionViewModel _viewModel;
    private readonly ContentControl _header = new() { Focusable = false };
    private readonly ContentControl _ai = new() { Focusable = false };
    private readonly ContentControl _keyboard = new() { Focusable = false };
    private readonly ContentControl _blank = new() { Focusable = false };
    private readonly ContentControl _suggested = new() { Focusable = false };
    private readonly ContentControl _available = new() { Focusable = false };
    private readonly ContentControl _installed = new() { Focusable = false };
    private readonly ContentControl _previewHead = new() { Focusable = false };
    private readonly ContentControl _previewBody = new() { Focusable = false };
    private readonly ContentControl _previewFoot = new() { Focusable = false };
    private bool _keyFieldShown;
    private readonly ColumnDefinition _contentColumn = new()
    {
        Width = new GridLength(1, GridUnitType.Star),
    };
    private readonly ColumnDefinition _previewColumn = new() { Width = new GridLength(0) };
    private readonly ThemedScrollViewer _content;
    private readonly Border _preview;
    private readonly IconPickerView _picker = new();
    private readonly TextInput _query;
    private readonly TextInput _blankName;
    private readonly TextInput _rowName;
    private readonly PasswordBox _key = new()
    {
        Background = Brushes.Transparent,
        BorderThickness = new Thickness(0),
        Padding = new Thickness(Space.S3, 0, Space.S3, 0),
        VerticalContentAlignment = VerticalAlignment.Center,
        MinHeight = ControlHeight.Medium - Theming.Generated.Stroke.Line * 2,
        FocusVisualStyle = null,
    };
    private readonly Grid _keyLayers = new();

    // The text of an error of the AI lives across the rebuilds of its card, so it is one live region (ACC-001): the card
    // is a Border, which UI Automation never sees.
    private readonly StyledText _errorText = Parts.Note(string.Empty, ColorToken.Text);
    private readonly LiveAnnouncer _errorAnnouncer;

    // The keys of one row of the preview are open at a time.
    private readonly ComboLines _rowComboLines = new();
    private TemplatesScreen? _shown;
    private bool _narrow;
    private int? _renaming;
    private int? _renamingShown;
    private int? _keysOpen;
    private int? _keysShown;
    private bool _blankShown;

    /// <summary>Creates the view of <paramref name="viewModel"/>.</summary>
    /// <param name="viewModel">The section.</param>
    public TemplatesSectionView(TemplatesSectionViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        _viewModel = viewModel;
        AutomationProperties.SetLiveSetting(_errorText, AutomationLiveSetting.Assertive);
        _errorAnnouncer = new LiveAnnouncer(_errorText);
        _query = new TextInput(string.Empty, string.Empty);
        _query.Changed += (_, _) => _viewModel.SetQuery(_query.Text);
        _query.DictateRequested += (_, _) => _viewModel.Dictate();
        _query.Box.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter && _shown?.Ai.CanGenerate == true)
            {
                e.Handled = true;
                _ = _viewModel.GenerateAsync();
            }
        };
        _blankName = new TextInput(string.Empty, string.Empty);
        _blankName.Changed += (_, _) => _viewModel.SetBlankName(_blankName.Text);
        _blankName.DictateRequested += (_, _) => _viewModel.Dictate();
        _rowName = new TextInput(string.Empty, string.Empty);
        _rowName.Changed += (_, _) =>
        {
            if (_renaming is { } index)
            {
                _viewModel.RenameRow(index, _rowName.Text);
            }
        };
        _rowName.DictateRequested += (_, _) => _viewModel.Dictate();
        _key.SetResourceReference(
            PasswordBox.FontSizeProperty,
            ThemeKeys.FontSize(TextStyleToken.Body)
        );
        _keyLayers.Children.Add(_key);
        _ = FieldFocusRing.Attach(_keyLayers, _key, Radii.Medium, Theming.Generated.Stroke.Line);
        Ui.Ink(_key, PasswordBox.ForegroundProperty, ColorToken.Text);
        Ui.Ink(_key, PasswordBox.CaretBrushProperty, ColorToken.Text);

        ColumnDefinitions.Add(_contentColumn);
        ColumnDefinitions.Add(_previewColumn);
        var sections = new StackPanel();
        // The real templates right after the AI, so they are in view; the keyboard they are made for, after them.
        foreach (
            var region in new[]
            {
                _header,
                _ai,
                _suggested,
                _available,
                _blank,
                _keyboard,
                _installed,
            }
        )
        {
            region.Margin = new Thickness(0, 0, 0, Space.S6);
            sections.Children.Add(region);
        }

        _content = new ThemedScrollViewer
        {
            Content = sections,
            Padding = new Thickness(Space.S6, Space.S6, Space.S4, Space.S6),
        };
        Children.Add(_content);

        var preview = new DockPanel { LastChildFill = true };
        var foot = new Border
        {
            Child = _previewFoot,
            Padding = new Thickness(Space.S4, Space.S3, Space.S4, Space.S3),
            BorderThickness = new Thickness(0, Theming.Generated.Stroke.Line, 0, 0),
        };
        Ui.Ink(foot, Border.BorderBrushProperty, ColorToken.Border);
        _previewHead.Margin = new Thickness(Space.S4, Space.S4, Space.S2, Space.S3);
        DockPanel.SetDock(_previewHead, Dock.Top);
        DockPanel.SetDock(foot, Dock.Bottom);
        preview.Children.Add(_previewHead);
        preview.Children.Add(foot);
        preview.Children.Add(
            new ThemedScrollViewer
            {
                Content = _previewBody,
                Padding = new Thickness(Space.S4, 0, Space.S2, Space.S6),
            }
        );
        _preview = new Border
        {
            Child = preview,
            BorderThickness = new Thickness(Theming.Generated.Stroke.Line, 0, 0, 0),
            Visibility = Visibility.Collapsed,
        };
        Ui.Ink(_preview, Border.BackgroundProperty, ColorToken.Win);
        Ui.Ink(_preview, Border.BorderBrushProperty, ColorToken.Border);
        SetColumn(_preview, 1);
        Children.Add(_preview);
        SetColumnSpan(_picker, 2);
        Children.Add(_picker);
        // The picker of «Perfil vacío» covers the templates and the preview for every input, not only for the eye.
        Parts.Cover(_picker, sections, preview);
        viewModel.PropertyChanged += OnChanged;
        Render();
    }

    /// <summary>What the text of an error of the AI announced last, and how (ACC-001).</summary>
    public LiveAnnouncer ErrorAnnouncer => _errorAnnouncer;

    /// <summary>
    /// Lays the section out for <paramref name="width"/>, the width beside the rail (CCM-005): the preview beside the
    /// content where both fit; otherwise the preview is a page of its own.
    /// </summary>
    /// <param name="width">The width of the section.</param>
    public void ApplyWidth(double width)
    {
        var narrow = width < ContentMinWidth + PreviewWidth;
        if (narrow == _narrow)
        {
            return;
        }

        _narrow = narrow;
        ApplyLayout();
    }

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

    /// <summary>A square of 44 with <paramref name="icon"/> in the ink of the accent on its wash.</summary>
    private static Border IconBoxOf(string icon)
    {
        var box = Ui.Card(
            Ui.Icon(icon, IconBox.Medium, ColorToken.AccentInk),
            ColorToken.AccentWash,
            null,
            Radii.Small,
            new Thickness(0)
        );
        box.Width = ControlHeight.Small;
        box.Height = ControlHeight.Small;
        box.VerticalAlignment = VerticalAlignment.Top;
        box.Margin = new Thickness(0, 0, Space.S3, 0);
        return box;
    }

    /// <summary>The icons of a template, as they will be in the panel: plain, in a row, never in boxes of their own.</summary>
    private static StackPanel MiniIcons(IEnumerable<string> icons)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal };
        foreach (var icon in icons)
        {
            var glyph = Ui.Icon(icon, IconBox.Small, ColorToken.Muted);
            glyph.Margin = new Thickness(0, 0, Space.S2, 0);
            row.Children.Add(glyph);
        }

        return row;
    }

    private static DockPanel Lead(UIElement lead, UIElement fill)
    {
        var dock = new DockPanel { LastChildFill = true };
        DockPanel.SetDock(lead, Dock.Left);
        dock.Children.Add(lead);
        dock.Children.Add(fill);
        return dock;
    }

    private static FrameworkElement Spaced(FrameworkElement element, double above)
    {
        element.Margin = new Thickness(0, above, 0, 0);
        return element;
    }

    /// <summary>A part of a card set apart by a line above it.</summary>
    private static Border Divided(UIElement child)
    {
        var part = new Border
        {
            Child = child,
            BorderThickness = new Thickness(0, Theming.Generated.Stroke.Line, 0, 0),
            Padding = new Thickness(0, Space.S3, 0, 0),
            Margin = new Thickness(0, Space.S2, 0, 0),
        };
        Ui.Ink(part, Border.BorderBrushProperty, ColorToken.Border);
        return part;
    }

    private void ApplyLayout()
    {
        var has = _shown?.Preview.HasContent == true;
        var page = _narrow && has;
        _contentColumn.Width = page ? new GridLength(0) : new GridLength(1, GridUnitType.Star);
        _content.Visibility = page ? Visibility.Collapsed : Visibility.Visible;
        _previewColumn.Width =
            !has ? new GridLength(0)
            : page ? new GridLength(1, GridUnitType.Star)
            : new GridLength(PreviewWidth);
        _preview.Visibility = has ? Visibility.Visible : Visibility.Collapsed;
        _preview.BorderThickness = new Thickness(page ? 0 : Theming.Generated.Stroke.Line, 0, 0, 0);
        _picker.Compact = _narrow;
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
            var title = new StyledText(screen.Title, TextStyleToken.Title) { MaxLines = 2 };
            AutomationProperties.SetHeadingLevel(title, AutomationHeadingLevel.Level2);
            _header.Content = Parts.Section(null, title, Parts.Note(screen.Subtitle));
        }

        if (before?.Ai != screen.Ai)
        {
            _ai.Content = AiCard(screen.Ai);
            _keyboard.Content = KeyboardLine(screen.Ai.Keyboard);
            if (screen.Ai.Error is { } error && before?.Ai.Error != error)
            {
                // The card shows the title and the text; the region says both, at once (ACC-001).
                _errorAnnouncer.Say(error.Title + ". " + error.Text, AnnouncementUrgency.Assertive);
            }
        }

        if (before?.Blank != screen.Blank)
        {
            _blank.Content = Blank(screen.Blank);
        }

        _picker.Show(screen.Blank.Picker, _viewModel);
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
            _previewHead.Content = PreviewHead(screen.Preview);
            _previewBody.Content = PreviewBody(screen.Preview);
            _previewFoot.Content = PreviewFoot(screen.Preview);
        }

        ApplyLayout();
    }

    // ---- Crear con IA ---------------------------------------------------------------------------------------

    /// <summary>
    /// «Crea atajos para cualquier programa» (PLA-002 to PLA-009), compact: the field with 🎤 and «Generar con IA», the
    /// examples, what is sent and the key. A consent or an error goes in the card after a line, never in a card of its
    /// own (no more than two nested borders).
    /// </summary>
    private Card AiCard(AiCardModel model)
    {
        var heading = new StyledText(model.Title, TextStyleToken.Heading) { MaxLines = 2 };
        AutomationProperties.SetHeadingLevel(heading, AutomationHeadingLevel.Level3);
        var top = Lead(
            IconBoxOf("auto_awesome"),
            Parts.Section(null, heading, Parts.Note(model.Description))
        );

        Detach(_query);
        AutomationProperties.SetName(_query.Box, model.Placeholder);
        _query.DictateName = model.DictateName;
        _query.Show(model.Query);
        _query.SetPlaceholder(model.Placeholder);
        var generate = Parts.Button(
            model.GenerateText,
            "auto_awesome",
            ButtonVariant.Primary,
            () => _ = _viewModel.GenerateAsync()
        );
        generate.Size = ControlSize.Medium;
        generate.IsEnabled = model.CanGenerate;
        generate.Margin = new Thickness(Space.S2, 0, 0, 0);
        var field = new DockPanel { LastChildFill = true };
        DockPanel.SetDock(generate, Dock.Right);
        field.Children.Add(generate);
        field.Children.Add(_query);

        var examples = new WrapPanel { Margin = new Thickness(0, 0, -Space.S2, -Space.S2) };
        foreach (var name in model.Examples.Items)
        {
            var chip = Parts.Button(
                name,
                null,
                ButtonVariant.Secondary,
                () =>
                {
                    _query.Show(name, force: true);
                    _viewModel.UseExample(name);
                }
            );
            chip.Margin = new Thickness(0, 0, Space.S2, Space.S2);
            examples.Children.Add(chip);
        }

        var keyButton = Parts.Button(
            model.KeyButton,
            "key",
            ButtonVariant.Tertiary,
            _viewModel.ToggleKey
        );
        // ACC-001: the button opens and closes the field of the key under it.
        keyButton.IsExpanded = model.KeyFieldOpen;
        keyButton.Margin = new Thickness(Space.S2, 0, 0, 0);
        var keyLine = new DockPanel { LastChildFill = true };
        DockPanel.SetDock(keyButton, Dock.Right);
        keyLine.Children.Add(keyButton);
        // The state of the key (PLA-003) carries the icon of the key, as its button and «General › IA» do.
        var status = Parts.IconLine("key", model.KeyStatus, style: TextStyleToken.Caption);
        status.VerticalAlignment = VerticalAlignment.Center;
        keyLine.Children.Add(status);

        var column = Parts.Section(
            null,
            top,
            Spaced(field, Space.S2),
            examples,
            Parts.IconLine("shield", model.Privacy, style: TextStyleToken.Caption),
            keyLine
        );
        if (model.KeyFieldOpen)
        {
            column.Children.Add(KeyField(model));

            // The keyboard goes to the key only when its field opens: the card is built again on every letter of the
            // app field, and taking the focus each time sent the rest of a dictated name into the key (D20 review).
            if (!_keyFieldShown)
            {
                _ = Dispatcher.BeginInvoke(() => Keyboard.Focus(_key));
            }
        }
        else
        {
            _key.Clear();
        }

        _keyFieldShown = model.KeyFieldOpen;

        if (model.Consent is { } consent)
        {
            column.Children.Add(Consent(consent));
        }

        if (model.Error is { } error)
        {
            column.Children.Add(Error(error));
        }

        return new Card { Content = column };
    }

    private StackPanel KeyField(AiCardModel model)
    {
        Detach(_keyLayers);
        AutomationProperties.SetName(_key, model.KeyPlaceholder);
        var box = Ui.Card(
            _keyLayers,
            ColorToken.Field,
            ColorToken.Line,
            Radii.Medium,
            new Thickness(0)
        );
        var paste = Parts.Button(
            model.PasteText,
            "content_paste",
            ButtonVariant.Secondary,
            PasteKey
        );
        var done = Parts.Button(
            model.DoneText,
            null,
            ButtonVariant.Primary,
            () =>
            {
                var key = _key.Password;
                _key.Clear();
                _viewModel.SaveKey(key);
            }
        );
        // As tall as the field of the key beside them, as «Generar» beside the field of the app.
        paste.Size = ControlSize.Medium;
        done.Size = ControlSize.Medium;
        var row = new DockPanel { LastChildFill = true };
        DockPanel.SetDock(done, Dock.Right);
        DockPanel.SetDock(paste, Dock.Right);
        done.Margin = new Thickness(Space.S2, 0, 0, 0);
        paste.Margin = new Thickness(Space.S2, 0, 0, 0);
        row.Children.Add(done);
        row.Children.Add(paste);
        row.Children.Add(box);

        // ADR-0033: whose key it is comes first, with where it comes from and what the free tier of Gemini means.
        var providers = Parts.Track([
            .. model.Providers.Select(option =>
                (UIElement)
                    Parts.Segment(
                        option.Label,
                        option.Selected,
                        () => _viewModel.SetProvider(option.Value)
                    )
            ),
        ]);
        AutomationProperties.SetName(providers, model.ProviderTitle);
        var column = Parts.Section(
            model.ProviderTitle,
            providers,
            Parts.Note(model.ProviderDetail, style: TextStyleToken.Caption),
            model.ProviderNotice is { } notice
                ? Parts.IconLine("shield", notice, style: TextStyleToken.Caption)
                : null,
            Parts.Note(model.KeyPlaceholder, style: TextStyleToken.Caption),
            row
        );
        if (model.DeleteKeyText is { } delete)
        {
            var remove = Parts.Button(
                delete,
                "key_off",
                ButtonVariant.Danger,
                _viewModel.DeleteKey
            );
            remove.HorizontalAlignment = HorizontalAlignment.Left;
            column.Children.Add(Spaced(remove, Space.S2));
        }

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
        var ok = Parts.Button(
            model.AcceptText,
            null,
            ButtonVariant.Primary,
            () => _ = _viewModel.AcceptConsentAsync()
        );
        var no = Parts.Button(
            model.DeclineText,
            null,
            ButtonVariant.Secondary,
            _viewModel.DeclineConsent
        );
        return Divided(
            Parts.Section(
                null,
                new StyledText(model.Title, TextStyleToken.Label) { MaxLines = 0 },
                Parts.Note(model.Text),
                // ADR-0033: with Gemini the consent also says what the free tier lets Google do with what is sent.
                model.Notice
                    is { } notice
                    ? Parts.IconLine("shield", notice)
                    : null,
                Ui.Columns(2, Space.S2, [ok, no])
            )
        );
    }

    private Border Error(AiErrorModel model)
    {
        var buttons = model
            .Actions.Items.Select(action =>
            {
                var button = Parts.Button(
                    action.Label,
                    null,
                    ButtonVariant.Secondary,
                    () => _ = _viewModel.ErrorActionAsync(action.Kind)
                );
                button.MaxLines = 2;
                button.Height = double.NaN;
                button.MinHeight = ControlHeight.Small;
                return (UIElement)button;
            })
            .ToList();
        Detach(_errorText);
        _errorText.Text = Parts.Unbroken(model.Text);
        // The section spaces its parts by their top margin, which the text would otherwise keep from the card before.
        _errorText.Margin = default;
        return Divided(
            Parts.Section(
                null,
                Parts.IconLine(
                    model.Icon,
                    model.Title,
                    ColorToken.WarnText,
                    ColorToken.Text,
                    TextStyleToken.Label
                ),
                _errorText,
                Ui.Columns(Math.Max(1, buttons.Count), Space.S2, buttons)
            )
        );
    }

    /// <summary>The keyboard the shortcuts are made for (PLA-013): the row of <see cref="KeyboardChooser"/>.</summary>
    private StackPanel KeyboardLine(KeyboardModel model) =>
        KeyboardChooser.Build(
            model,
            _viewModel.ToggleKeyboard,
            _viewModel.SetLayout,
            _viewModel.SetAppsLanguage
        );

    // ---- Perfil vacío ---------------------------------------------------------------------------------------

    /// <summary>
    /// «Perfil vacío» (PLA-010): a row that unfolds the name with 🎤, its icon (which opens the icon picker, D14), where
    /// it acts and «Crear perfil».
    /// </summary>
    private StackPanel Blank(BlankModel model)
    {
        var caret = Ui.Icon(
            model.Open ? "expand_less" : "expand_more",
            IconBox.Medium,
            ColorToken.Muted
        );
        var lines = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        lines.Children.Add(new StyledText(model.Title, TextStyleToken.Label));
        lines.Children.Add(
            new StyledText(model.Subtitle, TextStyleToken.Caption, ColorToken.Muted)
            {
                MaxLines = 2,
            }
        );
        var content = new DockPanel { LastChildFill = true };
        var box = IconBoxOf("add_circle");
        box.VerticalAlignment = VerticalAlignment.Center;
        DockPanel.SetDock(box, Dock.Left);
        DockPanel.SetDock(caret, Dock.Right);
        content.Children.Add(box);
        content.Children.Add(caret);
        content.Children.Add(lines);
        var toggle = new TouchButton
        {
            Content = content,
            Variant = ButtonVariant.Secondary,
            Size = ControlSize.Large,
            IsExpanded = model.Open,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Padding = new Thickness(Space.S2, Space.S2, Space.S3, Space.S2),
        };
        toggle.Height = double.NaN;
        toggle.MinHeight = ControlHeight.Large;
        AutomationProperties.SetName(toggle, model.Title);
        AutomationProperties.SetHelpText(toggle, model.Subtitle);
        toggle.Click += (_, _) => _viewModel.ToggleBlank();
        var column = new StackPanel();
        column.Children.Add(toggle);
        if (model.Open)
        {
            var form = BlankForm(model);
            form.Margin = new Thickness(0, Space.S4, 0, 0);
            column.Children.Add(form);
            if (!_blankShown)
            {
                // Only when it opens: the form comes into view under «Perfil vacío».
                column.Loaded += (_, _) => column.BringIntoView();
            }
        }

        _blankShown = model.Open;
        return column;
    }

    private StackPanel BlankForm(BlankModel model)
    {
        var icon = new OptionButton
        {
            Kind = OptionKind.Tile,
            Content = Ui.Icon(model.Icon, IconBox.Medium, ColorToken.AccentInk),
            Role = OptionRole.Expander,
            IsSelected = model.IconsOpen,
            Width = ControlHeight.Medium,
            Height = ControlHeight.Medium,
            Padding = new Thickness(0),
            Margin = new Thickness(0, 0, Space.S2, 0),
        };
        AutomationProperties.SetName(icon, model.ChangeIconName);
        icon.Click += (_, _) => _viewModel.ToggleBlankIcons();
        Detach(_blankName);
        AutomationProperties.SetName(_blankName.Box, model.Placeholder);
        _blankName.DictateName = model.DictateName;
        _blankName.SetPlaceholder(model.Placeholder);
        _blankName.Show(model.Name);
        var row = new DockPanel { LastChildFill = true };
        DockPanel.SetDock(icon, Dock.Left);
        row.Children.Add(icon);
        row.Children.Add(_blankName);

        var links = new WrapPanel { Margin = new Thickness(0, 0, -Space.S2, -Space.S2) };
        foreach (var link in model.Links.Items)
        {
            var chip = new OptionButton
            {
                Kind = OptionKind.Chip,
                Content = link.Label,
                Symbol = link.Icon,
                IsSelected = link.Selected,
                Margin = new Thickness(0, 0, Space.S2, Space.S2),
            };
            AutomationProperties.SetName(chip, link.Label);
            chip.Click += (_, _) => _viewModel.SetBlankLink(link.Id);
            links.Children.Add(chip);
        }

        var create = Parts.Button(
            model.CreateText,
            "add",
            ButtonVariant.Primary,
            _viewModel.CreateBlank
        );
        create.IsEnabled = model.CanCreate;
        create.HorizontalAlignment = HorizontalAlignment.Left;
        var where = Parts.Section(model.LinkTitle, links);
        where.Margin = new Thickness(0, Space.S4, 0, 0);
        var column = new StackPanel();
        column.Children.Add(row);
        column.Children.Add(where);
        column.Children.Add(Spaced(create, Space.S4));
        return column;
    }

    // ---- Apps abiertas, plantillas y perfiles ---------------------------------------------------------------

    /// <summary>
    /// «Apps abiertas sin perfil» (PLA-011): one header line with the dot of attention, the title and [asShort] with its
    /// switch at the end; the whole line is the switch (DIS-30), named with what it does and what it detects. Then a
    /// card per suggested template.
    /// </summary>
    private StackPanel Suggested(SuggestedModel model)
    {
        var dot = new Border
        {
            Width = Space.S2,
            Height = Space.S2,
            CornerRadius = new CornerRadius(Radii.Pill),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, Space.S2, 0),
        };
        Ui.Ink(dot, Border.BackgroundProperty, ColorToken.Warn);
        var title = new Overline(model.Title) { VerticalAlignment = VerticalAlignment.Center };
        var label = new StyledText(model.DetectText, TextStyleToken.Body)
        {
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(Space.S3, 0, 0, 0),
        };
        var line = new DockPanel { LastChildFill = true };
        DockPanel.SetDock(label, Dock.Right);
        DockPanel.SetDock(dot, Dock.Left);
        line.Children.Add(label);
        line.Children.Add(dot);
        line.Children.Add(title);
        var head = new ToggleSwitch
        {
            Content = line,
            IsChecked = model.DetectOn,
            FollowsViewModel = true,
        };
        AutomationProperties.SetName(head, model.DetectText + " " + model.Title);
        head.Click += (_, _) => _viewModel.ToggleDetect();
        var column = new StackPanel();
        column.Children.Add(head);
        foreach (var card in model.Cards)
        {
            var preview = Parts.Button(
                model.PreviewText,
                "visibility",
                ButtonVariant.Secondary,
                () => _viewModel.PreviewTemplate(card.Id)
            );
            var install = Parts.Button(
                model.InstallText,
                "download",
                ButtonVariant.Primary,
                () => _viewModel.InstallTemplate(card.Id)
            );
            AutomationProperties.SetName(install, model.InstallText + " " + card.Name);
            AutomationProperties.SetName(preview, model.PreviewText + " " + card.Name);
            var info = Lead(
                IconBoxOf(card.Icon),
                Parts.Section(
                    null,
                    new StyledText(card.Meta, TextStyleToken.Label) { MaxLines = 2 },
                    MiniIcons(card.Icons.Items)
                )
            );
            var actions = Ui.Columns(2, Space.S2, [preview, install]);
            column.Children.Add(
                Spaced(
                    new Card
                    {
                        Tone = CardTone.Warn,
                        Content = Parts.Section(null, info, Spaced(actions, Space.S2)),
                    },
                    Space.S3
                )
            );
        }

        if (model.EmptyText is { } empty)
        {
            column.Children.Add(Spaced(Parts.Note(empty), Space.S2));
        }

        return column;
    }

    /// <summary>
    /// The templates (PLA-011): a card per template, its icon, name, what it has and its icons as the panel shows them;
    /// a tap shows it in the preview with the single look of «elegido», and «Instalar» under it installs it all.
    /// </summary>
    private StackPanel Available(TemplatesScreen screen)
    {
        var column = Parts.Section(screen.AvailableTitle);
        if (screen.AllInstalledText is { } all)
        {
            column.Children.Add(Parts.Note(all));
            return column;
        }

        var grid = new AutoFillGrid
        {
            MinItemWidth = ShortcutsSectionView.ProfilesWidth,
            Gap = Space.S3,
        };
        foreach (var card in screen.Available)
        {
            var install = Parts.Button(
                screen.InstallText,
                "download",
                ButtonVariant.Secondary,
                () => _viewModel.InstallTemplate(card.Id)
            );
            AutomationProperties.SetName(install, screen.InstallText + " " + card.Name);
            var lines = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            lines.Children.Add(new StyledText(card.Name, TextStyleToken.Label));
            lines.Children.Add(new StyledText(card.Meta, TextStyleToken.Caption, ColorToken.Muted));
            var body = Parts.Section(
                null,
                Lead(IconBoxOf(card.Icon), lines),
                MiniIcons(card.Icons.Items)
            );
            body.Margin = new Thickness(0, Space.S3, 0, Space.S3);
            var open = new OptionButton
            {
                Kind = OptionKind.Tile,
                Content = body,
                IsSelected = card.Selected,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
            };
            open.Height = double.NaN;
            open.MinHeight = ControlHeight.Large;
            AutomationProperties.SetName(open, card.Name + ", " + card.Meta);
            open.Click += (_, _) => _viewModel.PreviewTemplate(card.Id);
            var cell = new StackPanel();
            cell.Children.Add(open);
            cell.Children.Add(Spaced(install, Space.S2));
            grid.Children.Add(cell);
        }

        column.Children.Add(grid);
        return column;
    }

    private StackPanel Installed(InstalledModel model)
    {
        var title = new Overline(model.Title + " · " + model.Count);
        var profiles = new WrapPanel { Margin = new Thickness(0, 0, -Space.S2, -Space.S2) };
        foreach (var profile in model.Profiles.Items)
        {
            var icon = Ui.Icon(profile.Icon, IconBox.Small, ColorToken.AccentInk);
            icon.Margin = new Thickness(0, 0, Space.S2, 0);
            var count = new StyledText(profile.Count, TextStyleToken.BodySmall, ColorToken.Muted)
            {
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(Space.S2, 0, Space.S1, 0),
            };
            var row = new StackPanel { Orientation = Orientation.Horizontal };
            row.Children.Add(icon);
            row.Children.Add(
                new StyledText(profile.Name, TextStyleToken.Label)
                {
                    VerticalAlignment = VerticalAlignment.Center,
                }
            );
            row.Children.Add(count);
            row.Children.Add(Ui.Icon("chevron_right", IconBox.Small, ColorToken.Muted));
            var button = new TouchButton
            {
                Content = row,
                Variant = ButtonVariant.Secondary,
                Padding = new Thickness(Space.S3, 0, Space.S2, 0),
                Margin = new Thickness(0, 0, Space.S2, Space.S2),
            };
            AutomationProperties.SetName(button, profile.Name + ", " + profile.CountName);
            button.Click += (_, _) => _viewModel.OpenInstalled(profile.Id);
            profiles.Children.Add(button);
        }

        var import = Parts.Button(
            model.ImportText,
            "download",
            ButtonVariant.Secondary,
            () => _ = _viewModel.ImportAsync()
        );
        import.VerticalAlignment = VerticalAlignment.Top;
        var hint = Parts.Note(model.ShareHint, style: TextStyleToken.Caption);
        hint.Margin = new Thickness(Space.S3, 0, 0, 0);
        hint.VerticalAlignment = VerticalAlignment.Center;
        var line = Lead(import, hint);
        line.Margin = new Thickness(0, Space.S2, 0, 0);
        return Parts.Section(null, title, profiles, line);
    }

    // ---- Vista previa ---------------------------------------------------------------------------------------

    private DockPanel? PreviewHead(PreviewModel model)
    {
        if (!model.HasContent)
        {
            return null;
        }

        var close = Parts.Button(
            string.Empty,
            "close",
            ButtonVariant.Tertiary,
            _viewModel.ClosePreview,
            model.CloseName
        );
        close.VerticalAlignment = VerticalAlignment.Top;
        var heading = new StyledText(model.Name, TextStyleToken.Heading);
        AutomationProperties.SetHeadingLevel(heading, AutomationHeadingLevel.Level3);
        var lines = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        lines.Children.Add(heading);
        if (model.Process.Length > 0)
        {
            lines.Children.Add(
                new StyledText(model.Process, TextStyleToken.Caption, ColorToken.Muted)
            );
        }

        var head = new DockPanel { LastChildFill = true };
        DockPanel.SetDock(close, Dock.Right);
        head.Children.Add(close);
        var box = IconBoxOf(model.Icon);
        box.VerticalAlignment = VerticalAlignment.Center;
        DockPanel.SetDock(box, Dock.Left);
        head.Children.Add(box);
        head.Children.Add(lines);
        return head;
    }

    private StackPanel? PreviewBody(PreviewModel model)
    {
        if (!model.HasContent)
        {
            _renaming = null;
            _keysShown = null;
            return null;
        }

        var column = new StackPanel();
        if (model.UnknownText is { } unknown)
        {
            var blank = Parts.Button(
                model.UnknownButton ?? string.Empty,
                "add",
                ButtonVariant.Secondary,
                _viewModel.CreateFromUnknown
            );
            blank.MaxLines = 2;
            blank.Height = double.NaN;
            blank.MinHeight = ControlHeight.Small;
            column.Children.Add(
                new Card
                {
                    Tone = CardTone.Warn,
                    Content = Parts.Section(
                        null,
                        Parts.IconLine("help", unknown, ColorToken.WarnText, ColorToken.Text),
                        blank
                    ),
                    Margin = new Thickness(0, 0, 0, Space.S3),
                }
            );
        }

        if (model.InstalledNote is { } note)
        {
            column.Children.Add(
                Parts.IconLine("check_circle", note, ColorToken.Success, ColorToken.Text)
            );
        }

        var notes = new List<FrameworkElement>
        {
            Parts.IconLine("keyboard", model.KeyboardLine, style: TextStyleToken.Caption),
        };
        notes.AddRange(
            new[] { model.OnlyEsNote, model.TextsNote }
                .OfType<string>()
                .Select(warning =>
                    (FrameworkElement)
                        Parts.IconLine(
                            "translate",
                            warning,
                            ColorToken.WarnText,
                            ColorToken.WarnText,
                            TextStyleToken.Caption
                        )
                )
        );
        foreach (var line in notes)
        {
            column.Children.Add(Spaced(line, Space.S2));
        }

        _renaming = null;
        _keysOpen = null;
        foreach (var row in model.Rows)
        {
            column.Children.Add(Spaced(Row(row), Space.S2));
        }

        _renamingShown = _renaming;
        _keysShown = _keysOpen;
        return column;
    }

    /// <summary>The fixed foot of the preview: «Instalar N atajos» (and the update of the variant), never under a row.</summary>
    private StackPanel? PreviewFoot(PreviewModel model)
    {
        if (!model.HasContent)
        {
            return null;
        }

        var column = new StackPanel();
        if (model.VariantText is { } variant)
        {
            // EC-PLA-04: the installed shortcuts keep their keys when the programs language changes; this updates them.
            var update = Parts.Button(
                variant,
                "translate",
                ButtonVariant.Secondary,
                _viewModel.UpdateVariant
            );
            update.Margin = new Thickness(0, 0, 0, Space.S2);
            column.Children.Add(update);
        }

        var install = Parts.Button(
            model.ButtonText,
            model.ButtonIcon,
            model.ButtonSecondary ? ButtonVariant.Secondary : ButtonVariant.Primary,
            _viewModel.InstallPreview
        );
        install.IsEnabled = model.ButtonEnabled;
        column.Children.Add(install);
        return column;
    }

    /// <summary>
    /// A shortcut of the preview (PLA-015, PLA-016): the row itself is the switch that keeps it (✓ and the single look
    /// of «elegido»), with its name and its keys; ⌨ and ✏ beside it change its keys and its name.
    /// </summary>
    private StackPanel Row(PreviewRowModel row)
    {
        var check = Ui.Icon(
            row.AlreadyIn ? "check_circle"
                : row.Checked ? "check_box"
                : "check_box_outline_blank",
            IconBox.Medium,
            row.AlreadyIn ? ColorToken.Success
                : row.Checked ? ColorToken.AccentInk
                : ColorToken.Muted
        );
        check.IsFilled = row.Checked || row.AlreadyIn;
        check.Margin = new Thickness(0, 0, Space.S3, 0);
        var lines = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        lines.Children.Add(new StyledText(row.Name, TextStyleToken.Label));
        // A combination in the font of keys; what a Web, App or Macro row does, whole and wrapped (LOG-008).
        lines.Children.Add(
            row.AlreadyIn ? new StyledText(row.Foot, TextStyleToken.Caption, ColorToken.Muted)
            : row.KeysName is not null
                ? new StyledText(row.Foot, TextStyleToken.Keys, ColorToken.Muted)
            : new StyledText(row.Foot, TextStyleToken.Caption, ColorToken.Muted) { MaxLines = 0 }
        );
        if (row.Warning is { } warning)
        {
            lines.Children.Add(
                new StyledText(warning, TextStyleToken.Caption, ColorToken.WarnText)
                {
                    MaxLines = 2,
                }
            );
        }

        var content = new DockPanel { LastChildFill = true };
        DockPanel.SetDock(check, Dock.Left);
        content.Children.Add(check);
        content.Children.Add(lines);
        var toggle = new OptionButton
        {
            Kind = OptionKind.Tile,
            Content = content,
            Role = OptionRole.Toggle,
            IsSelected = row.Checked && !row.AlreadyIn,
            IsEnabled = !row.AlreadyIn,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Padding = new Thickness(Space.S3, Space.S2, Space.S3, Space.S2),
        };
        toggle.Height = double.NaN;
        toggle.MinHeight = ControlHeight.Medium;
        AutomationProperties.SetName(toggle, row.Name + ", " + row.Foot);
        AutomationProperties.SetHelpText(toggle, row.Warning ?? string.Empty);
        toggle.Click += (_, _) => _viewModel.ToggleRow(row.Index);

        // The row, then the slots of ⌨ and ✏: a row without ⌨ (a web, an app, a macro) keeps its slot empty, so every
        // row that can still change ends at the same x, and the tools keep the gap between controls, so two rings of
        // «elegido» never touch.
        var line = new Grid();
        line.ColumnDefinitions.Add(
            new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }
        );
        line.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        line.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        line.Children.Add(toggle);
        if (!row.AlreadyIn)
        {
            FrameworkElement keys = new Border { Width = ControlHeight.Small };
            if (row.KeysName is { } keysName)
            {
                // PLA-016: the keys of the row can change before installing.
                var change = Parts.Button(
                    string.Empty,
                    "keyboard",
                    ButtonVariant.Tertiary,
                    () => _viewModel.EditRowKeys(row.Index),
                    keysName + ": " + row.Name
                );
                change.IsExpanded = row.Combo is not null;
                change.IsActive = row.Combo is not null;
                keys = change;
            }

            var edit = Parts.Button(
                string.Empty,
                "edit",
                ButtonVariant.Tertiary,
                () => _viewModel.EditRow(row.Index),
                row.RenameName + ": " + row.Name
            );
            edit.IsExpanded = row.Editing;
            edit.IsActive = row.Editing;
            foreach (var (tool, slot) in new[] { (keys, 1), ((FrameworkElement)edit, 2) })
            {
                tool.VerticalAlignment = VerticalAlignment.Center;
                tool.Margin = new Thickness(Space.S2, 0, 0, 0);
                Grid.SetColumn(tool, slot);
                line.Children.Add(tool);
            }
        }
        else
        {
            Grid.SetColumnSpan(toggle, 3);
        }
        var column = new StackPanel();
        column.Children.Add(line);
        if (row.Editing)
        {
            _renaming = row.Index;
            Detach(_rowName);
            AutomationProperties.SetName(_rowName.Box, row.RenameName);
            _rowName.DictateName = row.DictateName;
            var starting = _renamingShown != row.Index;
            _rowName.Show(row.Name, force: starting);
            // PLA-015, ACC-011: the name of a row is dictated like every other free text.
            column.Children.Add(Spaced(_rowName, Space.S2));
            if (starting)
            {
                // Only when the field opens: selecting again on every keystroke would replace what is being typed.
                _ = Dispatcher.BeginInvoke(() =>
                {
                    _ = Keyboard.Focus(_rowName.Box);
                    _rowName.Box.SelectAll();
                });
            }
        }

        if (row.Combo is { } combo)
        {
            _keysOpen = row.Index;
            column.Children.Add(
                Spaced(ComboView.Build(combo, _viewModel.RowKeys, _rowComboLines), Space.S3)
            );
            if (_keysShown != row.Index)
            {
                // Only when the keys open: the row and its keys come into view in the narrow preview column.
                column.Loaded += (_, _) => column.BringIntoView();
            }
        }

        return column;
    }
}
