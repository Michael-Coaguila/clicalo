using System.ComponentModel;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using Clicalo.Application.Ports;
using Clicalo.Domain.Primitives;
using Clicalo.Domain.Settings;
using Clicalo.Presentation.Welcome;
using Clicalo.UI.Wpf.Controls;
using Clicalo.UI.Wpf.Theming;
using Clicalo.UI.Wpf.Theming.Generated;
using Clicalo.UI.Wpf.Workspace.General;
using Clicalo.UI.Wpf.Workspace.Internal;

namespace Clicalo.UI.Wpf.Workspace.Welcome;

/// <summary>
/// The welcome (docs/06, BIE-001) in direction C (D12): a window of a fixed size for its five steps (600 ×
/// <see cref="WelcomeHeight"/>, padding <see cref="Space.S8"/>), always on top, with five progress bars, the step in a
/// zone that scrolls with the designed bar when it needs more room, and the fixed footer [Atrás] (secondary) ·
/// [Omitir] (tertiary) · [Siguiente]/[Empezar] (primary), all 52 high. Every choice has the single look of «elegido»
/// (the wash, the ring and a ✓ or a filled icon). Esc does not close it; Alt+F4 is [Omitir].
/// </summary>
/// <remarks>
/// Like the Control Center it never activates itself (<c>Window.Activate</c> is banned): it is shown without
/// activation and the composition root brings it to the front through the <c>ControlCenter</c> foreground lease
/// (blueprint §8.1). The steps are rebuilt from <see cref="WelcomeViewModel.Screen"/>, and the keyboard stays on the
/// control with the same automation id.
/// </remarks>
public sealed class WelcomeWindow : Window
{
    /// <summary>The width of the welcome (BIE-001).</summary>
    public const double WelcomeWidth = 600;

    /// <summary>
    /// The height of the welcome, the same for its five steps (D12: the window never jumps from one step to the next);
    /// the tallest step fits, and a longer one (a reinstallation, the note of a repeated welcome) scrolls inside.
    /// </summary>
    public const double WelcomeHeight = 640;

    private readonly WelcomeViewModel _viewModel;
    private readonly Grid _progress = new();
    private readonly ThemedScrollViewer _scroll = new();
    private readonly ContentControl _footer = new() { Focusable = false };
    private int _shownStep = -1;
    private bool _closing;

    /// <summary>Creates the window of <paramref name="viewModel"/>, painted by <paramref name="theme"/>.</summary>
    /// <param name="viewModel">The welcome.</param>
    /// <param name="theme">The theme service of the UI thread.</param>
    public WelcomeWindow(WelcomeViewModel viewModel, ThemeService theme)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        ArgumentNullException.ThrowIfNull(theme);
        _viewModel = viewModel;
        theme.Attach(this);
        Width = WelcomeWidth;
        Height = WelcomeHeight;
        SizeToContent = SizeToContent.Manual;
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.Manual;
        ShowActivated = false;
        ShowInTaskbar = true;
        Topmost = true;
        UseLayoutRounding = true;

        var layout = new Grid();
        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        layout.RowDefinitions.Add(
            new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }
        );
        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        _scroll.Margin = new Thickness(0, Space.S6, 0, Space.S6);
        Grid.SetRow(_scroll, 1);
        Grid.SetRow(_footer, 2);
        layout.Children.Add(_progress);
        layout.Children.Add(_scroll);
        layout.Children.Add(_footer);
        var frame = new Border
        {
            Child = layout,
            CornerRadius = new CornerRadius(Radii.Large),
            Padding = new Thickness(Space.S8),
            SnapsToDevicePixels = true,
        };
        frame.SetResourceReference(Border.BorderThicknessProperty, ThemeScope.BorderThicknessKey);
        Ui.Ink(frame, Border.BackgroundProperty, ColorToken.Win);
        Ui.Ink(frame, Border.BorderBrushProperty, ColorToken.Border);
        Content = frame;
        viewModel.PropertyChanged += OnChanged;
        PreviewKeyDown += OnPreviewKeyDown;
        // ACC-004: a step that draws itself again does not take the keyboard away.
        _ = FocusKeeper.Attach(this);
        Render();
    }

    /// <summary>Alt+F4 (BIE-001): the composition root treats it as [Omitir].</summary>
    public event EventHandler? CloseRequested;

    /// <summary>The window handle, for the foreground lease.</summary>
    public WindowToken Token => new(new WindowInteropHelper(this).EnsureHandle());

    /// <summary>Closes it for good (it ended, or the app is exiting).</summary>
    public void Destroy()
    {
        _closing = true;
        _viewModel.PropertyChanged -= OnChanged;
        Close();
    }

    /// <summary>Centers it on <paramref name="workArea"/>.</summary>
    /// <param name="workArea">The work area, in device-independent pixels.</param>
    public void Place(Rect workArea)
    {
        Width = Math.Min(WelcomeWidth, workArea.Width);
        Height = Math.Min(WelcomeHeight, workArea.Height);
        Left = workArea.Left + ((workArea.Width - Width) / 2);
        Top = workArea.Top + Math.Max(0, (workArea.Height - Height) / 2);
    }

    /// <inheritdoc />
    protected override void OnClosing(CancelEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        if (!_closing)
        {
            e.Cancel = true;
            CloseRequested?.Invoke(this, EventArgs.Empty);
        }

        base.OnClosing(e);
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        // BIE-001: Esc does not close the welcome.
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
        }
    }

    private void OnChanged(object? sender, PropertyChangedEventArgs e) => Render();

    private void Render()
    {
        var screen = _viewModel.Screen;
        Title = screen.WindowTitle;
        AutomationProperties.SetName(this, screen.WindowTitle);
        var focused =
            (Keyboard.FocusedElement as FrameworkElement) is { } element && IsAncestorOf(element)
                ? AutomationProperties.GetAutomationId(element)
                : null;
        Progress(screen);
        var step = Step(screen);
        _scroll.Content = step;
        if (screen.Step != _shownStep)
        {
            // A new step starts at its top; a choice inside the same step keeps the place.
            _shownStep = screen.Step;
            _scroll.ScrollToTop();
        }

        _footer.Content = Footer(screen);
        if (!string.IsNullOrEmpty(focused) && Content is DependencyObject root)
        {
            _ = Dispatcher.BeginInvoke(() => Refocus(root, focused));
        }
    }

    private static void Refocus(DependencyObject root, string automationId)
    {
        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (
                child is UIElement { Focusable: true } candidate
                && string.Equals(
                    AutomationProperties.GetAutomationId(candidate),
                    automationId,
                    StringComparison.Ordinal
                )
            )
            {
                _ = Keyboard.Focus(candidate);
                return;
            }

            Refocus(child, automationId);
        }
    }

    /// <summary>The five progress bars: the steps done and the one in view in the accent, the rest in the line.</summary>
    private void Progress(WelcomeScreen screen)
    {
        _progress.Children.Clear();
        _progress.ColumnDefinitions.Clear();
        AutomationProperties.SetName(_progress, screen.StepName);
        for (var i = 0; i < screen.StepCount; i++)
        {
            _progress.ColumnDefinitions.Add(
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }
            );
            var bar = new Border
            {
                Height = Space.S1,
                CornerRadius = new CornerRadius(Space.S1 / 2),
                Margin = new Thickness(
                    i == 0 ? 0 : Space.S1 / 2,
                    0,
                    i == screen.StepCount - 1 ? 0 : Space.S1 / 2,
                    0
                ),
            };
            Ui.Ink(
                bar,
                Border.BackgroundProperty,
                i <= screen.Step ? ColorToken.Accent : ColorToken.Line
            );
            Grid.SetColumn(bar, i);
            _progress.Children.Add(bar);
        }
    }

    private StackPanel Step(WelcomeScreen screen) =>
        screen.Step switch
        {
            0 => Step0(screen),
            1 => Step1(screen),
            2 => Step2(screen),
            3 => Step3(screen),
            _ => Step4(screen),
        };

    /// <summary>The title of a step and the text under it.</summary>
    private static StackPanel Head(WelcomeScreen screen)
    {
        var title = SettingsUi.Styled(screen.Title, TextStyleToken.Display, lines: 2);
        AutomationProperties.SetHeadingLevel(title, AutomationHeadingLevel.Level1);
        return Ui.Column(
            Space.S2,
            title,
            screen.Body.Length == 0
                ? null
                : SettingsUi.Styled(screen.Body, TextStyleToken.Body, ColorToken.Muted, lines: 0)
        );
    }

    private StackPanel Step0(WelcomeScreen screen)
    {
        var brand = Ui.Row(
            Space.S3,
            BrandMark.Mark(ControlHeight.Medium),
            Ui.Column(
                0,
                BrandMark.Word(screen.AppName, TextStyles.Get(TextStyleToken.Title).Size),
                SettingsUi.Styled(screen.Tagline, TextStyleToken.BodySmall, ColorToken.Muted)
            )
        );
        var signature = Ui.Row(
            Space.S3,
            BrandMark.Avatar(
                screen.CreatorInitials,
                ControlHeight.Small,
                TextStyles.Get(TextStyleToken.Label).Size
            ),
            Ui.Column(
                0,
                SettingsUi.Styled(screen.CreatorName, TextStyleToken.Label),
                SettingsUi.Styled(screen.CreatorRole, TextStyleToken.Caption, ColorToken.Muted)
            )
        );
        var story = new Card
        {
            Content = Ui.Column(
                Space.S3,
                SettingsUi.Styled(screen.Story, TextStyleToken.Body, lines: 0),
                signature
            ),
        };
        var languages = new List<UIElement>();
        foreach (var option in screen.Languages)
        {
            var language = SettingsUi.Option(
                OptionKind.Tile,
                option.Label,
                option.Label,
                option.Selected,
                () => _viewModel.SetLanguage(option.Id)
            );
            language.Height = ControlHeight.Medium;
            AutomationProperties.SetAutomationId(language, "language." + option.Id);
            languages.Add(language);
        }

        return Ui.Column(
            Space.S6,
            brand,
            Head(screen),
            story,
            screen.Reinstall is { } reinstall ? Reinstall(reinstall) : null,
            SettingsUi.Options(languages.Count, languages)
        );
    }

    /// <summary>
    /// The question of a reinstallation that found data from before (P6): «Conservar mis datos» is the option in force
    /// and «Empezar de cero» takes two taps (REG-04), both 52 high (REG-02).
    /// </summary>
    private Card Reinstall(WelcomeReinstallCard card)
    {
        var title = SettingsUi.Styled(card.Title, TextStyleToken.Label, lines: 0);
        AutomationProperties.SetLiveSetting(title, AutomationLiveSetting.Polite);
        if (card.Done.Length > 0)
        {
            var done = SettingsUi.Styled(card.Done, TextStyleToken.Body, lines: 0);
            AutomationProperties.SetLiveSetting(done, AutomationLiveSetting.Polite);
            var check = Ui.Icon("check_circle", IconBox.Medium, ColorToken.Success);
            check.VerticalAlignment = VerticalAlignment.Top;
            return new Card { Content = Ui.Row(Space.S2, check, done) };
        }

        var keep = SettingsUi.Option(
            OptionKind.Tile,
            card.KeepText,
            card.KeepText,
            true,
            static () => { }
        );
        keep.Height = ControlHeight.Medium;
        AutomationProperties.SetAutomationId(keep, "reinstall.keep");
        var fresh = SettingsUi.Button(
            card.FreshText,
            ButtonVariant.Danger,
            _viewModel.StartFromScratch,
            card.FreshArmed ? "warning" : "delete",
            ControlSize.Medium
        );
        fresh.IsActive = card.FreshArmed;
        AutomationProperties.SetAutomationId(fresh, "reinstall.fresh");
        AutomationProperties.SetHelpText(fresh, card.Description);
        return new Card
        {
            Content = Ui.Column(
                Space.S3,
                title,
                SettingsUi.Styled(card.Description, TextStyleToken.BodySmall, ColorToken.Muted, 0),
                SettingsUi.Options(2, [keep, fresh])
            ),
        };
    }

    /// <summary>What [Siguiente] changes and what it leaves as the person set it (BIE-010), read out when it changes.</summary>
    private static Card Changes(WelcomeChangesNote note)
    {
        var column = new StackPanel();
        void Add(string title, ValueList<string> lines, string icon)
        {
            if (lines.IsEmpty)
            {
                return;
            }

            var heading = SettingsUi.Styled(title, TextStyleToken.Label, lines: 0);
            heading.Margin = new Thickness(0, column.Children.Count == 0 ? 0 : Space.S2, 0, 0);
            column.Children.Add(heading);
            foreach (var line in lines)
            {
                var mark = Ui.Icon(icon, IconBox.Small, ColorToken.Muted);
                mark.VerticalAlignment = VerticalAlignment.Top;
                var row = Ui.Row(
                    Space.S2,
                    mark,
                    SettingsUi.Styled(line, TextStyleToken.BodySmall, lines: 0)
                );
                row.Margin = new Thickness(0, Space.S1, 0, 0);
                column.Children.Add(row);
            }
        }

        Add(note.ChangesTitle, note.Changes, "swap_horiz");
        Add(note.KeptTitle, note.Kept, "lock");
        var card = new Card { Content = column };
        AutomationProperties.SetLiveSetting(card, AutomationLiveSetting.Polite);
        AutomationProperties.SetName(
            card,
            string.Join(
                ". ",
                new[] { note.Changes.IsEmpty ? null : note.ChangesTitle }
                    .Concat(note.Changes)
                    .Concat([note.Kept.IsEmpty ? null : note.KeptTitle])
                    .Concat(note.Kept)
                    .Where(text => !string.IsNullOrEmpty(text))
            )
        );
        return card;
    }

    /// <summary>
    /// BIE-005: five option cards of 88 in two columns, each with its icon (filled while chosen) over its name; several
    /// can be chosen, so each one is a switch for UI Automation.
    /// </summary>
    private StackPanel Step1(WelcomeScreen screen)
    {
        var options = new List<UIElement>();
        foreach (var option in screen.Uses)
        {
            var card = SettingsUi.Option(
                OptionKind.Card,
                option.Label,
                option.Label,
                option.Selected,
                () => _viewModel.ToggleUse(option.Id),
                option.Icon,
                OptionRole.Toggle
            );
            card.Height = ControlHeight.Large + Space.S6;
            AutomationProperties.SetAutomationId(card, "use." + option.Id);
            options.Add(card);
        }

        return Ui.Column(
            Space.S6,
            Head(screen),
            SettingsUi.Options(2, options),
            screen.Changes is { } changes ? Changes(changes) : null
        );
    }

    /// <summary>
    /// BIE-006: the detected keyboard, «Básicos» as a whole row with its description, and the templates as tiles of
    /// 44 in three columns, all with the same border, height and ✓ or filled icon while chosen.
    /// </summary>
    private StackPanel Step2(WelcomeScreen screen)
    {
        var keyboard = SettingsUi.SettingRow("keyboard", screen.KeyboardLine, null, null);
        keyboard.MinHeight = ControlHeight.Small;
        UIElement? basics = null;
        var templates = new List<UIElement>();
        foreach (var option in screen.Kit)
        {
            OptionButton chip;
            if (option.Description.Length > 0)
            {
                var texts = Ui.Column(
                    0,
                    SettingsUi.Styled(option.Label, TextStyleToken.Label),
                    SettingsUi.Styled(option.Description, TextStyleToken.Caption, ColorToken.Muted)
                );
                chip = SettingsUi.Tall(
                    SettingsUi.Option(
                        OptionKind.Tile,
                        texts,
                        option.Label,
                        option.Selected,
                        () => _viewModel.ToggleKit(option.Id),
                        option.Icon,
                        OptionRole.Toggle
                    ),
                    ControlHeight.Medium
                );
                chip.HorizontalContentAlignment = HorizontalAlignment.Left;
                AutomationProperties.SetHelpText(chip, option.Description);
                basics = chip;
            }
            else
            {
                chip = SettingsUi.Option(
                    OptionKind.Tile,
                    option.Label,
                    option.Label,
                    option.Selected,
                    () => _viewModel.ToggleKit(option.Id),
                    option.Icon,
                    OptionRole.Toggle
                );
                chip.HorizontalContentAlignment = HorizontalAlignment.Left;
                templates.Add(chip);
            }

            AutomationProperties.SetAutomationId(chip, "kit." + option.Id);
        }

        return Ui.Column(
            Space.S4,
            Head(screen),
            keyboard,
            basics,
            SettingsUi.Options(3, templates)
        );
    }

    /// <summary>BIE-007: the three views as option cards with their icon, name and description.</summary>
    private StackPanel Step3(WelcomeScreen screen)
    {
        var cards = new List<UIElement>();
        foreach (var view in screen.Views)
        {
            var name = SettingsUi.Styled(view.Label, TextStyleToken.Label);
            name.HorizontalAlignment = HorizontalAlignment.Center;
            var description = SettingsUi.Styled(
                view.Description,
                TextStyleToken.Caption,
                ColorToken.Muted,
                lines: 0
            );
            description.TextAlignment = TextAlignment.Center;
            var card = SettingsUi.Tall(
                SettingsUi.Option(
                    OptionKind.Card,
                    Ui.Column(Space.S1, name, description),
                    view.Label,
                    view.Selected,
                    () => _viewModel.SetDensity(view.Density),
                    view.Icon
                )
            );
            card.VerticalContentAlignment = VerticalAlignment.Top;
            AutomationProperties.SetHelpText(card, view.Description);
            AutomationProperties.SetAutomationId(card, "view." + view.Density);
            cards.Add(card);
        }

        return Ui.Column(Space.S6, Head(screen), SettingsUi.Options(3, cards));
    }

    /// <summary>
    /// BIE-008: the three sizes with a «Copiar» button at real scale in a stage as tall as the largest, so the names
    /// line up, and the four themes as tiles with their swatch (as in «General y panel»).
    /// </summary>
    private StackPanel Step4(WelcomeScreen screen)
    {
        var stage = screen.Sizes.IsEmpty ? 0 : screen.Sizes.Max(size => size.TileHeight);
        var sizes = new List<UIElement>();
        foreach (var size in screen.Sizes)
        {
            var sample = new Border
            {
                Child = Ui.Column(
                    Space.S1,
                    Ui.Icon("content_copy", size.IconSize, ColorToken.AccentInk),
                    Sample(screen.CopyLabel, size.TextSize)
                ),
                Width = size.TileWidth,
                Height = size.TileHeight,
                CornerRadius = new CornerRadius(Radii.Medium),
                BorderThickness = new Thickness(Stroke.Line),
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
            };
            if (sample.Child is FrameworkElement inner)
            {
                inner.VerticalAlignment = VerticalAlignment.Center;
            }

            Ui.Ink(sample, Border.BackgroundProperty, ColorToken.Card);
            Ui.Ink(sample, Border.BorderBrushProperty, ColorToken.Border);
            var holder = new Grid { Height = stage };
            holder.Children.Add(sample);
            var card = SettingsUi.Tall(
                SettingsUi.Option(
                    OptionKind.Tile,
                    Ui.Column(Space.S3, holder, SettingsUi.CheckLabel(size.Label, size.Selected)),
                    size.Label,
                    size.Selected,
                    () => _viewModel.SetSize(size.Size)
                )
            );
            AutomationProperties.SetAutomationId(card, "size." + size.Size);
            sizes.Add(card);
        }

        var themes = new List<UIElement>();
        foreach (var option in screen.Themes)
        {
            var value = Enum.TryParse<ThemeChoice>(option.Id, out var parsed)
                ? parsed
                : ThemeChoice.Auto;
            var theme = SettingsUi.ThemeOption(
                value,
                option.Label,
                option.Label,
                option.Selected,
                () => _viewModel.SetTheme(option.Id)
            );
            AutomationProperties.SetAutomationId(theme, "theme." + option.Id);
            themes.Add(theme);
        }

        return Ui.Column(
            Space.S6,
            Head(screen),
            Ui.Column(
                Space.S2,
                new Overline(screen.SizeTitle),
                SettingsUi.Options(sizes.Count, sizes)
            ),
            Ui.Column(Space.S2, new Overline(screen.ThemeTitle), SettingsUi.Options(2, themes))
        );
    }

    /// <summary>[Atrás] (secondary, not on step 0) · [Omitir] (tertiary) · [Siguiente]/[Empezar] (primary), all 52.</summary>
    private DockPanel Footer(WelcomeScreen screen)
    {
        var footer = new DockPanel { LastChildFill = false };
        if (screen.CanBack)
        {
            var back = SettingsUi.Button(
                screen.BackText,
                ButtonVariant.Secondary,
                _viewModel.Back,
                "arrow_back",
                ControlSize.Medium
            );
            AutomationProperties.SetAutomationId(back, "back");
            DockPanel.SetDock(back, Dock.Left);
            footer.Children.Add(back);
        }

        var next = SettingsUi.Button(
            screen.NextText,
            ButtonVariant.Primary,
            _viewModel.Next,
            size: ControlSize.Medium
        );
        AutomationProperties.SetAutomationId(next, "next");
        DockPanel.SetDock(next, Dock.Right);
        footer.Children.Add(next);
        var skip = SettingsUi.Button(
            screen.SkipText,
            ButtonVariant.Tertiary,
            _viewModel.Skip,
            size: ControlSize.Medium
        );
        skip.Margin = new Thickness(0, 0, Space.S2, 0);
        AutomationProperties.SetAutomationId(skip, "skip");
        DockPanel.SetDock(skip, Dock.Right);
        footer.Children.Add(skip);
        return footer;
    }

    private static TextBlock Sample(string text, double px)
    {
        // The sample of each size is drawn at real scale: its type size is the panel's, not the text size setting.
        var block = new TextBlock
        {
            Text = text,
            FontWeight = FontWeights.Bold,
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        block.FontSize = px;
        block.SetResourceReference(TextBlock.FontFamilyProperty, ThemeKeys.UiFont);
        Ui.Ink(block, TextBlock.ForegroundProperty, ColorToken.Text);
        return block;
    }
}
