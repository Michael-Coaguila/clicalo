using System.ComponentModel;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using Clicalo.Domain.Settings;
using Clicalo.Presentation.ControlCenter.General;
using Clicalo.UI.Wpf.Controls;
using Clicalo.UI.Wpf.Theming;
using Clicalo.UI.Wpf.Theming.Generated;
using Clicalo.UI.Wpf.Workspace.Internal;

namespace Clicalo.UI.Wpf.Workspace.General;

/// <summary>
/// «General y panel» (docs/05 §3, GEN-001) in direction C (D12): the title, the 2 × 2 grid of groups of equal height
/// (Idioma, Tema, Tamaño, Vista; one column in a narrow window) and, below, two columns from 1240 of window width (one
/// below): Disposición, Transparencia and Accesibilidad y datos on the left; Modo pestaña, Confirmación al tocar,
/// Seguridad de teclas, Inteligencia artificial and Primeros pasos on the right (GEN-001 places the first two and the
/// four of the right; the two groups it does not place go where both columns end at about the same height). Every
/// group is the same outlined frame with its overline (<see cref="SettingsUi.Group"/>); every choice has the single
/// look of «elegido». It only draws <see cref="GeneralSectionViewModel.Screen"/> and forwards taps; each group is
/// rebuilt only when its part changed, and the two sliders live across rebuilds so a drag is never cut.
/// </summary>
public sealed class GeneralSectionView : ContentControl
{
    /// <summary>The window width from which the groups below go in two columns (GEN-001).</summary>
    private const double WideFrom = 1240;

    /// <summary>
    /// The width of this view from which the 2 × 2 grid has two columns: below it, the four themes of a group no
    /// longer fit two per row with their whole names, so the groups go one under the other.
    /// </summary>
    private const double TwoGroupsFrom = 900;

    private readonly GeneralSectionViewModel _viewModel;
    private readonly StyledText _title;
    private readonly StyledText _subtitle;
    private readonly Grid _top = new();
    private readonly ContentControl _language = Region();
    private readonly ContentControl _theme = Region();
    private readonly ContentControl _size = Region();
    private readonly ContentControl _view = Region();
    private readonly ContentControl _layout = Region();
    private readonly ContentControl _transparency = Region();
    private readonly ContentControl _dock = Region();
    private readonly ContentControl _feedback = Region();
    private readonly ContentControl _safety = Region();
    private readonly ContentControl _access = Region();
    private readonly ContentControl _start = Region();
    private readonly ContentControl _ai = Region();
    private readonly Grid _columns = new();
    private readonly StackPanel _left;
    private readonly StackPanel _right;
    private readonly SettingSlider _opacity;
    private readonly SettingSlider _dim;
    private readonly Border _preview = new() { CornerRadius = new CornerRadius(Radii.Small) };
    private GeneralScreen? _shown;
    private bool? _narrow;
    private bool? _narrowTop;

    /// <summary>Creates the view of <paramref name="viewModel"/>.</summary>
    /// <param name="viewModel">The section.</param>
    public GeneralSectionView(GeneralSectionViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        _viewModel = viewModel;
        Focusable = false;
        (_title, _subtitle) = SettingsUi.NewHeader();
        _opacity = new SettingSlider(viewModel.SetOpacityPercent, withDescription: false);
        _dim = new SettingSlider(viewModel.SetDimPercent, withDescription: false);
        Ui.Ink(_preview, Border.BackgroundProperty, ColorToken.Panel);
        Ui.Ink(_preview, Border.BorderBrushProperty, ColorToken.Border);
        _preview.BorderThickness = new Thickness(Stroke.Line);

        _left = Ui.Column(Space.S4, _layout, _transparency, _access);
        _right = Ui.Column(Space.S4, _dock, _feedback, _safety, _ai, _start);
        var page = Ui.Column(Space.S6, SettingsUi.PageHeader(_title, _subtitle), _top, _columns);
        page.Margin = new Thickness(Space.S6);
        var root = new Border { Child = new ThemedScrollViewer { Content = page } };
        Ui.Ink(root, Border.BackgroundProperty, ColorToken.Win);
        Content = root;

        viewModel.PropertyChanged += OnChanged;
        SizeChanged += (_, _) => ApplyWidth();
        Arrange(narrow: false, narrowTop: false);
        Render();
    }

    /// <summary>Stops following the view model (the window is closing for good).</summary>
    public void Detach() => _viewModel.PropertyChanged -= OnChanged;

    private static ContentControl Region() =>
        new()
        {
            Focusable = false,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Stretch,
        };

    private void OnChanged(object? sender, PropertyChangedEventArgs e) => Render();

    private void ApplyWidth()
    {
        // The groups below follow the window (GEN-001); the 2 × 2 grid, the room this view has.
        var width = ActualWidth;
        if (width <= 0)
        {
            return;
        }

        var narrow = SettingsUi.WindowWidth(this) < WideFrom;
        var narrowTop = width < TwoGroupsFrom;
        if (narrow != _narrow || narrowTop != _narrowTop)
        {
            Arrange(narrow, narrowTop);
        }
    }

    /// <summary>
    /// The 2 × 2 grid (one column below the default width) and the two columns below from 1240 of window width, one
    /// below that (GEN-001, CCM-005).
    /// </summary>
    private void Arrange(bool narrow, bool narrowTop)
    {
        _narrow = narrow;
        _narrowTop = narrowTop;
        Place(_top, narrowTop ? 1 : 2, [_language, _theme, _size, _view]);
        Place(_columns, narrow ? 1 : 2, [_left, _right]);
    }

    /// <summary>
    /// Lays <paramref name="cells"/> out in <paramref name="count"/> equal columns, <see cref="Space.S4"/> apart, with
    /// rows as tall as their tallest cell (the groups of a row share their height).
    /// </summary>
    private static void Place(Grid grid, int count, FrameworkElement[] cells)
    {
        grid.Children.Clear();
        grid.ColumnDefinitions.Clear();
        grid.RowDefinitions.Clear();
        for (var c = 0; c < count; c++)
        {
            grid.ColumnDefinitions.Add(
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }
            );
        }

        var rows = (cells.Length + count - 1) / count;
        for (var r = 0; r < rows; r++)
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        }

        for (var i = 0; i < cells.Length; i++)
        {
            var column = i % count;
            var row = i / count;
            cells[i].Margin = new Thickness(
                column == 0 ? 0 : Space.S4 / 2,
                row == 0 ? 0 : Space.S4 / 2,
                column == count - 1 ? 0 : Space.S4 / 2,
                row == rows - 1 ? 0 : Space.S4 / 2
            );
            Grid.SetColumn(cells[i], column);
            Grid.SetRow(cells[i], row);
            grid.Children.Add(cells[i]);
        }
    }

    private void Render()
    {
        var screen = _viewModel.Screen;
        var shown = _shown;
        _shown = screen;
        _title.Text = screen.Title;
        _subtitle.Text = screen.Subtitle;
        if (shown?.Look != screen.Look)
        {
            var look = screen.Look;
            _language.Content = LanguageGroup(look);
            _theme.Content = ThemeGroup(look);
            _size.Content = SizeGroup(look);
            _view.Content = ViewGroup(look);
        }

        if (shown?.Layout != screen.Layout)
        {
            _layout.Content = LayoutGroup(screen.Layout);
        }

        ShowTransparency(screen.Transparency, shown?.Transparency);
        if (shown?.Dock != screen.Dock)
        {
            _dock.Content = DockGroup(screen.Dock);
        }

        if (shown?.Feedback != screen.Feedback)
        {
            var feedback = screen.Feedback;
            _feedback.Content = SettingsUi.Group(
                feedback.Caption,
                Switches(
                    SwitchRow(feedback.Sound, _viewModel.ToggleSound),
                    SwitchRow(feedback.Flash, _viewModel.ToggleFlash)
                )
            );
        }

        if (shown?.Safety != screen.Safety)
        {
            _safety.Content = SafetyGroup(screen.Safety);
        }

        if (shown?.Access != screen.Access)
        {
            _access.Content = AccessGroup(screen.Access);
        }

        if (shown?.Start != screen.Start)
        {
            _start.Content = StartGroup(screen.Start);
        }

        if (shown?.Ai != screen.Ai)
        {
            _ai.Content = screen.Ai is { } ai ? AiGroup(ai) : null;
            _ai.Visibility = screen.Ai is null ? Visibility.Collapsed : Visibility.Visible;
        }
    }

    // ---- The 2 × 2 grid ------------------------------------------------------------------------------------------

    /// <summary>GEN-002: «Idioma · Language» with two options of 64; the title bar has the same choice.</summary>
    private Border LanguageGroup(LookModel look)
    {
        var options = new List<UIElement>();
        foreach (var option in look.Languages)
        {
            var button = SettingsUi.Option(
                OptionKind.Tile,
                option.Name,
                option.Name,
                option.Selected,
                () => _viewModel.SetLanguage(option.Code)
            );
            button.Height = ControlHeight.Large;
            options.Add(button);
        }

        return SettingsUi.Group(
            look.LanguageTitle,
            SettingsUi.Options(options.Count, options),
            look.LanguageNote.Length == 0
                ? null
                : SettingsUi.Styled(
                    look.LanguageNote,
                    TextStyleToken.Caption,
                    ColorToken.Muted,
                    lines: 0
                )
        );
    }

    /// <summary>GEN-003: the four themes in 2 × 2 tiles with the swatch of each one.</summary>
    private Border ThemeGroup(LookModel look)
    {
        var options = new List<UIElement>();
        foreach (var option in look.Themes)
        {
            options.Add(
                SettingsUi.ThemeOption(
                    option.Value,
                    option.Label,
                    option.Name,
                    option.Selected,
                    () => _viewModel.SetTheme(option.Value)
                )
            );
        }

        return SettingsUi.Group(look.ThemeTitle, SettingsUi.Options(2, options));
    }

    /// <summary>GEN-004: S, M and L as option cards with their grid, and the text size with − and + of 44.</summary>
    private Border SizeGroup(LookModel look)
    {
        var options = new List<UIElement>();
        foreach (var option in look.Sizes)
        {
            var symbol = option.Value switch
            {
                PanelSize.Small => "grid_4x4",
                PanelSize.Large => "grid_view",
                _ => "grid_3x3",
            };
            options.Add(
                SettingsUi.Option(
                    OptionKind.Card,
                    option.Label,
                    option.Name,
                    option.Selected,
                    () => _viewModel.SetSize(option.Value),
                    symbol
                )
            );
        }

        var value = SettingsUi.Styled(look.TextSize, TextStyleToken.Label);
        value.MinWidth = ControlHeight.Large;
        value.TextAlignment = TextAlignment.Center;
        AutomationProperties.SetName(value, look.TextSizeTitle);
        var steps = Ui.Row(
            0,
            SettingsUi.IconAction(
                "remove",
                look.TextSmallerName,
                _viewModel.TextSmaller,
                look.CanTextSmaller
            ),
            value,
            SettingsUi.IconAction(
                "add",
                look.TextBiggerName,
                _viewModel.TextBigger,
                look.CanTextBigger
            )
        );
        var textSize = SettingsUi.SettingRow("format_size", look.TextSizeTitle, null, steps);
        textSize.MinHeight = ControlHeight.Small;
        return SettingsUi.Group(
            look.SizeTitle,
            SettingsUi.Options(options.Count, options),
            textSize
        );
    }

    /// <summary>GEN-005: the three views as option cards and the description of the one in use.</summary>
    private Border ViewGroup(LookModel look)
    {
        var options = new List<UIElement>();
        foreach (var option in look.Views)
        {
            var symbol = option.Value switch
            {
                PanelDensity.Compact => "view_compact",
                PanelDensity.Dock => "dock_to_right",
                _ => "view_agenda",
            };
            var card = SettingsUi.Option(
                OptionKind.Card,
                option.Label,
                option.Name,
                option.Selected,
                () => _viewModel.SetDensity(option.Value),
                symbol
            );
            AutomationProperties.SetHelpText(
                card,
                option.Selected ? look.ViewDescription : string.Empty
            );
            options.Add(card);
        }

        return SettingsUi.Group(
            look.ViewTitle,
            SettingsUi.Options(options.Count, options),
            SettingsUi.Styled(look.ViewDescription, TextStyleToken.Caption, ColorToken.Muted, 2)
        );
    }

    // ---- Left column ---------------------------------------------------------------------------------------------

    private Border LayoutGroup(LayoutModel layout)
    {
        var rows = new List<UIElement>();
        foreach (var option in layout.Rows)
        {
            rows.Add(
                Miniature(
                    Miniatures.Rows(option.Cells, option.Selected),
                    option,
                    () => _viewModel.SetRows(option.Value)
                )
            );
        }

        var columns = new List<UIElement>();
        foreach (var option in layout.Columns)
        {
            columns.Add(
                Miniature(
                    Miniatures.Columns(option.Cells, option.Selected),
                    option,
                    () => _viewModel.SetColumns(option.Value)
                )
            );
        }

        var keys = new List<UIElement>();
        foreach (var option in layout.ShowKeys)
        {
            keys.Add(
                Miniature(
                    Miniatures.Tile(layout.SampleName, option.Value ? layout.SampleKeys : null),
                    option,
                    () => _viewModel.SetShowKeys(option.Value)
                )
            );
        }

        var note =
            layout.ProfileSelectorNote.Length == 0
                ? null
                : SettingsUi.Styled(
                    layout.ProfileSelectorNote,
                    TextStyleToken.Caption,
                    ColorToken.Muted,
                    lines: 0
                );
        if (note is not null)
        {
            // Under the text of its switch: after the icon box and its gap.
            note.Margin = new Thickness(IconBox.Medium + Space.S3, 0, 0, Space.S2);
        }

        return SettingsUi.Group(
            layout.Caption,
            Item(layout.RowsTitle, layout.RowsDescription),
            SettingsUi.Options(rows.Count, rows),
            Switches(
                SwitchRow(layout.FollowApp, _viewModel.ToggleFollowApp),
                SwitchRow(layout.AlwaysVisibleRow, _viewModel.ToggleAlwaysVisibleRow),
                SwitchRow(layout.ProfileSelectorRow, _viewModel.ToggleProfileSelectorRow),
                note
            ),
            Item(layout.ColumnsTitle, null),
            SettingsUi.Options(columns.Count, columns),
            Item(layout.ShowKeysTitle, null),
            SettingsUi.Options(keys.Count, keys)
        );
    }

    private void ShowTransparency(TransparencyModel model, TransparencyModel? shown)
    {
        _opacity.Show(
            model.OpacityTitle,
            model.OpacityValue,
            model.OpacityPercent,
            Percent(SettingsSchema.Opacity.Min),
            Percent(SettingsSchema.Opacity.Max),
            Percent(SettingsSchema.Opacity.Step),
            Percent(SettingsSchema.OpacityButtonStep),
            model.OpacityLessName,
            model.OpacityMoreName
        );
        _dim.Show(
            model.DimTitle,
            model.DimValue,
            model.DimPercent,
            Percent(SettingsSchema.DimTo.Min),
            Percent(SettingsSchema.DimTo.Max),
            Percent(SettingsSchema.DimTo.Step),
            Percent(SettingsSchema.DimTo.Step),
            model.DimLessName,
            model.DimMoreName
        );
        _preview.Opacity = model.OpacityPercent / 100d;
        if (
            shown is not null
            && string.Equals(shown.Caption, model.Caption, StringComparison.Ordinal)
            && string.Equals(shown.Behind, model.Behind, StringComparison.Ordinal)
            && shown.AutoDim == model.AutoDim
        )
        {
            return;
        }

        _transparency.Content = SettingsUi.Group(
            model.Caption,
            _opacity.Detached(),
            Document(model.Behind),
            Switches(SwitchRow(model.AutoDim, _viewModel.ToggleAutoDim)),
            _dim.Detached()
        );
    }

    /// <summary>The preview of the opacity over a «document» (GEN-009): light stripes and the panel on top.</summary>
    private Grid Document(string behind)
    {
        var paper = ThemeCatalog.GetPalette(ThemeId.Light);
        var light = new SolidColorBrush(paper.GetColor(ColorToken.Win));
        var dark = new SolidColorBrush(paper.GetColor(ColorToken.Side));
        var stripes = new DrawingBrush
        {
            TileMode = TileMode.Tile,
            Viewport = new Rect(0, 0, Space.S6, Space.S6),
            ViewportUnits = BrushMappingMode.Absolute,
            Transform = new RotateTransform(45),
            Drawing = new DrawingGroup
            {
                Children =
                {
                    new GeometryDrawing(
                        light,
                        null,
                        new RectangleGeometry(new Rect(0, 0, Space.S3, Space.S6))
                    ),
                    new GeometryDrawing(
                        dark,
                        null,
                        new RectangleGeometry(new Rect(Space.S3, 0, Space.S3, Space.S6))
                    ),
                },
            },
        };
        stripes.Freeze();
        var document = new Grid { Height = ControlHeight.Large + Space.S4, ClipToBounds = true };
        document.Children.Add(
            new Border { Background = stripes, CornerRadius = new CornerRadius(Radii.Medium) }
        );
        var label = new Border
        {
            Child = new StyledText(behind, TextStyleToken.Caption)
            {
                Foreground = new SolidColorBrush(paper.GetColor(ColorToken.Text)),
            },
            Background = new SolidColorBrush(paper.GetColor(ColorToken.Card)),
            CornerRadius = new CornerRadius(Radii.Small),
            Padding = new Thickness(Space.S2, Space.S1 / 2, Space.S2, Space.S1 / 2),
            Margin = new Thickness(Space.S3),
            VerticalAlignment = VerticalAlignment.Top,
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        document.Children.Add(label);
        if (_preview.Parent is Panel old)
        {
            old.Children.Remove(_preview);
        }

        var cards = new UniformGrid { Columns = 3 };
        for (var i = 0; i < 3; i++)
        {
            var card = new Border
            {
                CornerRadius = new CornerRadius(Radii.Small / 2),
                Margin = new Thickness(Space.S1 / 2),
            };
            Ui.Ink(card, Border.BackgroundProperty, ColorToken.Card);
            cards.Children.Add(card);
        }

        _preview.Child = cards;
        _preview.Padding = new Thickness(Space.S1);
        _preview.Margin = new Thickness(0, Space.S3, Space.S3, Space.S3);
        _preview.HorizontalAlignment = HorizontalAlignment.Right;
        document.SizeChanged += (_, e) => _preview.Width = e.NewSize.Width / 2;
        document.Children.Add(_preview);
        AutomationProperties.SetName(document, behind);
        return document;
    }

    // ---- Right column --------------------------------------------------------------------------------------------

    private Border DockGroup(DockModel dock)
    {
        var sides = new List<UIElement>();
        foreach (var option in dock.Sides)
        {
            sides.Add(
                Miniature(
                    Miniatures.Side(option.Value, dock.Gutter, option.Selected),
                    option,
                    () => _viewModel.SetDockSide(option.Value)
                )
            );
        }

        var back = SettingsUi.IconAction(
            dock.Vertical ? "arrow_upward" : "arrow_back",
            dock.HandleBackName,
            _viewModel.HandleBack,
            dock.CanBack
        );
        var forward = SettingsUi.IconAction(
            dock.Vertical ? "arrow_downward" : "arrow_forward",
            dock.HandleForwardName,
            _viewModel.HandleForward,
            dock.CanForward
        );
        var handle = SettingsUi.SettingRow(
            "open_with",
            dock.HandleTitle,
            dock.HandleDescription,
            Ui.Row(Space.S2, back, forward)
        );
        var counts = new List<OptionButton>();
        foreach (var option in dock.PerPage)
        {
            counts.Add(
                SettingsUi.Option(
                    OptionKind.Segment,
                    option.Label,
                    option.Name,
                    option.Selected,
                    () => _viewModel.SetPerPage(option.Value)
                )
            );
        }

        var perPage = SettingsUi.SettingBlock(
            "view_agenda",
            dock.PerPageTitle,
            null,
            SettingsUi.Track(dock.PerPageTitle, counts)
        );
        return SettingsUi.Group(
            dock.Caption,
            SettingsUi.Styled(dock.Explain, TextStyleToken.BodySmall, ColorToken.Muted, lines: 0),
            SettingsUi.Options(sides.Count, sides),
            Switches(
                handle,
                SwitchRow(dock.HandleLock, _viewModel.ToggleHandleLock),
                perPage,
                SwitchRow(dock.AutoHide, _viewModel.ToggleAutoHide),
                SwitchRow(dock.KeepScrollbar, _viewModel.ToggleKeepScrollbar)
            )
        );
    }

    private Border SafetyGroup(SafetyModel safety)
    {
        var options = new List<OptionButton>();
        foreach (var option in safety.MaxHold)
        {
            options.Add(
                SettingsUi.Option(
                    OptionKind.Segment,
                    option.Label,
                    option.Name,
                    option.Selected,
                    () => _viewModel.SetMaxHold(option.Value)
                )
            );
        }

        return SettingsUi.Group(
            safety.Caption,
            Item(safety.MaxHoldTitle, safety.MaxHoldDescription),
            SettingsUi.Track(safety.MaxHoldTitle, options),
            Switches(SwitchRow(safety.ReleaseOnAppSwitch, _viewModel.ToggleReleaseOnAppSwitch))
        );
    }

    private Border AccessGroup(AccessModel access)
    {
        var times = new List<OptionButton>();
        foreach (var option in access.Times)
        {
            times.Add(
                SettingsUi.Option(
                    OptionKind.Segment,
                    option.Label,
                    option.Name,
                    option.Selected,
                    () => _viewModel.SetTimeMultiplier(option.Value)
                )
            );
        }

        UIElement? hotkeys = null;
        if (!access.Hotkeys.IsEmpty)
        {
            // BUR-005, D10: the combination comes from a closed list; chips of keys, never a combo box.
            var chips = new List<UIElement>();
            foreach (var option in access.Hotkeys)
            {
                var chip = SettingsUi.Option(
                    OptionKind.Chip,
                    option.Label,
                    option.Name,
                    option.Selected,
                    () => _viewModel.SetGlobalHotkey(option.Value)
                );
                chip.IsMonospace = true;
                chips.Add(chip);
            }

            var wrap = Ui.Wrap(Space.S2, chips);
            AutomationProperties.SetName(wrap, access.HotkeyChoicesName);
            hotkeys = wrap;
        }

        var reset = SettingsUi.Button(
            access.ResetTitle,
            ButtonVariant.Danger,
            _viewModel.ResetFrequents,
            access.ResetArmed ? "warning" : "restart_alt",
            name: access.ResetName
        );
        reset.IsActive = access.ResetArmed;
        reset.HorizontalAlignment = HorizontalAlignment.Left;
        AutomationProperties.SetHelpText(reset, access.ResetDescription);
        if (access.ResetArmed)
        {
            AutomationProperties.SetLiveSetting(reset, AutomationLiveSetting.Polite);
        }

        return SettingsUi.Group(
            access.Caption,
            Switches(SwitchRow(access.ReduceMotion, _viewModel.ToggleReduceMotion)),
            Item(access.TimesTitle, access.TimesDescription),
            SettingsUi.Track(access.TimesTitle, times),
            Switches(SwitchRow(access.Hotkey, _viewModel.ToggleGlobalHotkey)),
            hotkeys,
            Item(access.ResetName, access.ResetDescription),
            reset
        );
    }

    // GEN-015: the state of the consent, the AI on or off and the saved key.
    private Border AiGroup(AiModel ai)
    {
        var consent = SettingsUi.Button(
            ai.ConsentAction,
            ButtonVariant.Secondary,
            _viewModel.ToggleAiConsent
        );
        TouchButton? delete = null;
        if (ai.DeleteKeyText is { } text)
        {
            delete = SettingsUi.Button(
                text,
                ButtonVariant.Danger,
                _viewModel.DeleteAiKey,
                ai.DeleteKeyArmed ? "warning" : "delete"
            );
            delete.IsActive = ai.DeleteKeyArmed;
            if (ai.DeleteKeyArmed)
            {
                AutomationProperties.SetLiveSetting(delete, AutomationLiveSetting.Polite);
            }
        }

        return SettingsUi.Group(
            ai.Caption,
            Switches(
                SwitchRow(ai.Use, _viewModel.ToggleAi),
                SettingsUi.SettingBlock(
                    ai.Consent ? "verified_user" : "gpp_maybe",
                    ai.ConsentState,
                    ai.ConsentDetail,
                    consent
                ),
                SettingsUi.SettingBlock("key", ai.KeyState, null, delete)
            )
        );
    }

    private Border StartGroup(StartModel start)
    {
        var welcome = new NavRow
        {
            Content = start.WelcomeTitle,
            Caption = start.WelcomeDescription,
            Symbol = "waving_hand",
        };
        AutomationProperties.SetName(welcome, start.WelcomeTitle);
        AutomationProperties.SetHelpText(welcome, start.WelcomeDescription);
        welcome.Click += (_, _) => _viewModel.SeeWelcome();
        var coach = new NavRow
        {
            Content = start.CoachTitle,
            Caption = start.CoachDescription,
            Symbol = "help",
        };
        AutomationProperties.SetName(coach, start.CoachTitle);
        AutomationProperties.SetHelpText(coach, start.CoachDescription);
        coach.Click += (_, _) => _viewModel.SeeTabGuide();
        return SettingsUi.Group(
            start.Caption,
            welcome,
            coach,
            Switches(SwitchRow(start.NoKeyboard, _viewModel.ToggleNoKeyboard))
        );
    }

    // ---- Builders ------------------------------------------------------------------------------------------------

    private static double Percent(double fraction) => Math.Round(fraction * 100);

    /// <summary>The title of a setting inside a group (<see cref="TextStyleToken.Label"/>) with its description.</summary>
    private static StackPanel Item(string title, string? description) =>
        Ui.Column(
            Space.S1 / 2,
            SettingsUi.Styled(title, TextStyleToken.Label, lines: 2),
            string.IsNullOrEmpty(description)
                ? null
                : SettingsUi.Styled(description, TextStyleToken.Caption, ColorToken.Muted, lines: 0)
        );

    /// <summary>Rows with a switch, one under the other with no gap: each is already 52 or more high.</summary>
    private static StackPanel Switches(params UIElement?[] rows) => Ui.Column(0, rows);

    /// <summary>
    /// An option with a miniature (GEN-006, GEN-008, GEN-010): the miniature over its label, with ✓ while chosen.
    /// </summary>
    private static OptionButton Miniature<T>(
        UIElement miniature,
        SettingOption<T> option,
        Action click
    )
    {
        var content = Ui.Column(
            Space.S2,
            miniature,
            SettingsUi.CheckLabel(option.Label, option.Selected)
        );
        return SettingsUi.Tall(
            SettingsUi.Option(OptionKind.Tile, content, option.Name, option.Selected, click)
        );
    }

    private static ToggleSwitch SwitchRow(SwitchItem item, Action click) =>
        Ui.SwitchRow(item.Icon, item.Title, item.Description, item.On, click);
}
