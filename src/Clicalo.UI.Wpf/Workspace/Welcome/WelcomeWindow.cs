using System.ComponentModel;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using Clicalo.Application.Ports;
using Clicalo.Domain.Primitives;
using Clicalo.Presentation.Welcome;
using Clicalo.UI.Wpf.Theming;
using Clicalo.UI.Wpf.Theming.Generated;
using Clicalo.UI.Wpf.Workspace.Internal;

namespace Clicalo.UI.Wpf.Workspace.Welcome;

/// <summary>
/// The welcome (docs/06, BIE-001): a window 600 wide with padding 32 and radius 20, always on top, with five progress
/// bars, the step and [Atrás] · [Omitir] · [Siguiente]/[Empezar] of 52. Esc does not close it; Alt+F4 is [Omitir].
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

    private const double Padding32 = 32;
    private const double Radius = 20;
    private const double FooterHeight = 52;

    private readonly WelcomeViewModel _viewModel;
    private readonly ContentControl _body = new() { Focusable = false };
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
        SizeToContent = SizeToContent.Height;
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.Manual;
        ShowActivated = false;
        ShowInTaskbar = true;
        Topmost = true;
        UseLayoutRounding = true;
        var frame = new Border
        {
            Child = _body,
            CornerRadius = new CornerRadius(Radius),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(Padding32),
            SnapsToDevicePixels = true,
        };
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
        UpdateLayout();
        var height = double.IsNaN(ActualHeight) || ActualHeight <= 0 ? 560 : ActualHeight;
        Left = workArea.Left + ((workArea.Width - Width) / 2);
        Top = workArea.Top + Math.Max(0, (workArea.Height - height) / 2);
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
        var column = Ui.Column(22, Dots(screen), Step(screen), Footer(screen));
        _body.Content = column;
        if (!string.IsNullOrEmpty(focused))
        {
            _ = Dispatcher.BeginInvoke(() => Refocus(column, focused));
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

    private static Grid Dots(WelcomeScreen screen)
    {
        var dots = new Grid();
        AutomationProperties.SetName(dots, screen.StepName);
        for (var i = 0; i < screen.StepCount; i++)
        {
            dots.ColumnDefinitions.Add(
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }
            );
            var bar = new Border
            {
                Height = 6,
                CornerRadius = new CornerRadius(3),
                Margin = new Thickness(i == 0 ? 0 : 3, 0, i == screen.StepCount - 1 ? 0 : 3, 0),
            };
            Ui.Ink(
                bar,
                Border.BackgroundProperty,
                i <= screen.Step ? ColorToken.Accent : ColorToken.CardHi
            );
            Grid.SetColumn(bar, i);
            dots.Children.Add(bar);
        }

        return dots;
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

    private StackPanel Step0(WelcomeScreen screen)
    {
        var brand = Ui.Row(
            14,
            BrandMark.Mark(56),
            Ui.Column(
                2,
                BrandMark.Word(screen.AppName, 24),
                Ui.Text(screen.Tagline, 14, ink: ColorToken.Muted, wrap: true)
            )
        );
        var signature = Ui.Row(
            10,
            BrandMark.Avatar(screen.CreatorInitials, 36, 13),
            Ui.Column(
                0,
                Ui.Text(screen.CreatorName, 14, bold: true),
                Ui.Text(screen.CreatorRole, 12, ink: ColorToken.Muted)
            )
        );
        var story = Ui.Card(
            Ui.Column(12, Paragraph(screen.Story, 16, ColorToken.Text), signature),
            ColorToken.Card,
            null,
            14,
            new Thickness(18)
        );
        var languages = new List<UIElement>();
        foreach (var option in screen.Languages)
        {
            languages.Add(
                Segment(
                    option,
                    52,
                    12,
                    16,
                    "language." + option.Id,
                    () => _viewModel.SetLanguage(option.Id)
                )
            );
        }

        return Ui.Column(
            22,
            brand,
            Heading(screen.Title, 30),
            Paragraph(screen.Body, 16, ColorToken.Muted),
            story,
            screen.Reinstall is { } reinstall ? Reinstall(reinstall) : null,
            Ui.Columns(languages.Count, 8, languages)
        );
    }

    /// <summary>
    /// The question of a reinstallation that found data from before (P6): «Conservar mis datos» is the option in force
    /// and «Empezar de cero» takes two taps (REG-04), both 52 high (REG-02).
    /// </summary>
    private Border Reinstall(WelcomeReinstallCard card)
    {
        var title = Ui.Text(card.Title, 16, bold: true, wrap: true);
        AutomationProperties.SetLiveSetting(title, AutomationLiveSetting.Polite);
        if (card.Done.Length > 0)
        {
            var done = Ui.Text(card.Done, 14, wrap: true);
            AutomationProperties.SetLiveSetting(done, AutomationLiveSetting.Polite);
            return Ui.Card(
                Ui.Row(8, Ui.Icon("check_circle", 20, ColorToken.Accent), done),
                ColorToken.AccentWash,
                null,
                14,
                new Thickness(16)
            );
        }

        var keep = Ui.Choice(
            Centered(Ui.Text(card.KeepText, 15, bold: true, ink: ColorToken.OnAccent)),
            card.KeepText,
            true,
            () => { },
            52,
            12
        );
        CcChrome.Paint(keep, ColorToken.Accent, ColorToken.OnAccent, null);
        keep.BorderThickness = new Thickness(0);
        keep.HorizontalContentAlignment = HorizontalAlignment.Center;
        AutomationProperties.SetAutomationId(keep, "reinstall.keep");
        var fresh = Ui.Button(
            Centered(
                Ui.Text(
                    card.FreshText,
                    15,
                    bold: true,
                    ink: card.FreshArmed ? ColorToken.OnWarn : ColorToken.Text
                )
            ),
            card.FreshText,
            _viewModel.StartFromScratch,
            card.FreshArmed ? ColorToken.Warn : null,
            card.FreshArmed ? ColorToken.OnWarn : ColorToken.Text,
            card.FreshArmed ? null : ColorToken.Border,
            52,
            12
        );
        AutomationProperties.SetAutomationId(fresh, "reinstall.fresh");
        AutomationProperties.SetHelpText(fresh, card.Description);
        return Ui.Card(
            Ui.Column(
                10,
                title,
                Ui.Text(card.Description, 13, ink: ColorToken.Muted, wrap: true),
                Ui.Columns(2, 8, [keep, fresh])
            ),
            ColorToken.Card,
            ColorToken.Accent,
            14,
            new Thickness(16)
        );
    }

    /// <summary>What [Siguiente] changes and what it leaves as the person set it (BIE-010), read out when it changes.</summary>
    private static Border Changes(WelcomeChangesNote note)
    {
        var column = Ui.Column(6);
        void Add(string title, ValueList<string> lines, string icon, ColorToken ink)
        {
            if (lines.IsEmpty)
            {
                return;
            }

            column.Children.Add(Ui.Text(title, 13, bold: true, wrap: true));
            foreach (var line in lines)
            {
                column.Children.Add(
                    Ui.Row(8, Ui.Icon(icon, 16, ink), Ui.Text(line, 13, wrap: true))
                );
            }
        }

        Add(note.ChangesTitle, note.Changes, "swap_horiz", ColorToken.Accent);
        Add(note.KeptTitle, note.Kept, "lock", ColorToken.Muted);
        var card = Ui.Card(column, ColorToken.Card, null, 12, new Thickness(14, 12, 14, 12));
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

    private StackPanel Step1(WelcomeScreen screen)
    {
        var chips = new List<UIElement>();
        foreach (var option in screen.Uses)
        {
            var content = Ui.Column(
                6,
                Ui.Icon(option.Icon, 26, ColorToken.Accent),
                Ui.Text(option.Label, 16, bold: true, wrap: true)
            );
            ((FrameworkElement)content.Children[0]).HorizontalAlignment = HorizontalAlignment.Left;
            var chip = Ui.Choice(
                content,
                option.Label,
                option.Selected,
                () => _viewModel.ToggleUse(option.Id),
                84,
                14
            );
            chip.Height = double.NaN;
            chip.MinHeight = 84;
            chip.Padding = new Thickness(14);
            chip.HorizontalContentAlignment = HorizontalAlignment.Stretch;
            chip.VerticalContentAlignment = VerticalAlignment.Top;
            AutomationProperties.SetAutomationId(chip, "use." + option.Id);
            chips.Add(chip);
        }

        return Ui.Column(
            22,
            Heading(screen.Title, 28),
            Paragraph(screen.Body, 16, ColorToken.Muted),
            Ui.Columns(2, 10, chips),
            screen.Changes is { } changes ? Changes(changes) : null
        );
    }

    private StackPanel Step2(WelcomeScreen screen)
    {
        var keyboard = Ui.Card(
            Ui.Row(
                8,
                Ui.Icon("keyboard", 18, ColorToken.Accent),
                Ui.Text(screen.KeyboardLine, 13, wrap: true)
            ),
            ColorToken.Card,
            null,
            10,
            new Thickness(12, 10, 12, 10)
        );
        var chips = new List<UIElement>();
        foreach (var option in screen.Kit)
        {
            var label = Ui.Text(option.Label, 15, bold: true);
            UIElement text =
                option.Description.Length == 0
                    ? label
                    : Ui.Column(
                        0,
                        label,
                        Ui.Text(option.Description, 12, ink: ColorToken.Muted, wrap: true)
                    );
            var chip = Ui.Choice(
                Ui.Row(8, Ui.Icon(option.Icon, 20), text),
                option.Label,
                option.Selected,
                () => _viewModel.ToggleKit(option.Id),
                48,
                24
            );
            chip.Height = double.NaN;
            chip.MinHeight = 48;
            chip.Padding = new Thickness(16, 6, 16, 6);
            if (option.Description.Length > 0)
            {
                chip.MaxWidth = 534;
                AutomationProperties.SetHelpText(chip, option.Description);
            }

            AutomationProperties.SetAutomationId(chip, "kit." + option.Id);
            chips.Add(chip);
        }

        return Ui.Column(
            22,
            Heading(screen.Title, 28),
            Paragraph(screen.Body, 16, ColorToken.Muted),
            keyboard,
            Ui.Wrap(8, chips)
        );
    }

    private StackPanel Step3(WelcomeScreen screen)
    {
        var cards = new List<UIElement>();
        foreach (var view in screen.Views)
        {
            var miniature = Ui.Card(
                Ui.Icon(view.Icon, 18, ColorToken.Accent),
                ColorToken.Card,
                ColorToken.Border,
                6,
                new Thickness(0)
            );
            miniature.Width = view.MiniatureWidth;
            miniature.Height = view.MiniatureHeight;
            var holder = new Grid { Height = 56 };
            holder.Children.Add(miniature);
            var label = Ui.Text(view.Label, 14, bold: true);
            label.HorizontalAlignment = HorizontalAlignment.Center;
            var description = Ui.Text(view.Description, 12, ink: ColorToken.Muted, wrap: true);
            description.TextAlignment = TextAlignment.Center;
            var card = Ui.Choice(
                Ui.Column(8, holder, label, description),
                view.Label,
                view.Selected,
                () => _viewModel.SetDensity(view.Density),
                44,
                14
            );
            card.Height = double.NaN;
            card.Padding = new Thickness(8, 12, 8, 12);
            card.HorizontalContentAlignment = HorizontalAlignment.Stretch;
            card.VerticalContentAlignment = VerticalAlignment.Top;
            card.VerticalAlignment = VerticalAlignment.Stretch;
            AutomationProperties.SetHelpText(card, view.Description);
            AutomationProperties.SetAutomationId(card, "view." + view.Density);
            cards.Add(card);
        }

        return Ui.Column(
            22,
            Heading(screen.Title, 28),
            Paragraph(screen.Body, 16, ColorToken.Muted),
            Ui.Columns(3, 10, cards)
        );
    }

    private StackPanel Step4(WelcomeScreen screen)
    {
        var sizes = new List<UIElement>();
        foreach (var size in screen.Sizes)
        {
            var sample = Ui.Card(
                Ui.Column(
                    3,
                    Ui.Icon("content_copy", size.IconSize, ColorToken.Accent),
                    Centered(Fixed(screen.CopyLabel, size.TextSize))
                ),
                ColorToken.Card,
                ColorToken.Border,
                10,
                new Thickness(0)
            );
            sample.Width = size.TileWidth;
            sample.Height = size.TileHeight;
            if (sample.Child is FrameworkElement inner)
            {
                inner.VerticalAlignment = VerticalAlignment.Center;
            }

            var label = Ui.Text(size.Label, 14, bold: true);
            label.HorizontalAlignment = HorizontalAlignment.Center;
            var card = Ui.Choice(
                Ui.Column(10, sample, label),
                size.Label,
                size.Selected,
                () => _viewModel.SetSize(size.Size),
                44,
                14
            );
            card.Height = double.NaN;
            card.Padding = new Thickness(8, 14, 8, 14);
            card.VerticalAlignment = VerticalAlignment.Bottom;
            AutomationProperties.SetAutomationId(card, "size." + size.Size);
            sizes.Add(card);
        }

        var themes = new List<UIElement>();
        foreach (var option in screen.Themes)
        {
            themes.Add(
                Segment(
                    option,
                    44,
                    10,
                    14,
                    "theme." + option.Id,
                    () => _viewModel.SetTheme(option.Id)
                )
            );
        }

        var sizeRow = Ui.Columns(3, 10, sizes);
        return Ui.Column(
            22,
            Heading(screen.Title, 28),
            sizeRow,
            Ui.Columns(themes.Count, 6, themes)
        );
    }

    private DockPanel Footer(WelcomeScreen screen)
    {
        var footer = new DockPanel { LastChildFill = false, Height = FooterHeight };
        if (screen.CanBack)
        {
            var back = Ui.Button(
                Ui.Text(screen.BackText, 16),
                screen.BackText,
                _viewModel.Back,
                null,
                ColorToken.Text,
                ColorToken.Border,
                FooterHeight,
                12
            );
            back.Padding = new Thickness(20, 0, 20, 0);
            AutomationProperties.SetAutomationId(back, "back");
            DockPanel.SetDock(back, Dock.Left);
            footer.Children.Add(back);
        }

        var next = Ui.Button(
            Ui.Text(screen.NextText, 16, bold: true, ink: ColorToken.OnAccent),
            screen.NextText,
            _viewModel.Next,
            ColorToken.Accent,
            ColorToken.OnAccent,
            null,
            FooterHeight,
            12
        );
        next.Padding = new Thickness(24, 0, 24, 0);
        next.BorderThickness = new Thickness(0);
        AutomationProperties.SetAutomationId(next, "next");
        DockPanel.SetDock(next, Dock.Right);
        footer.Children.Add(next);
        var skip = Ui.Button(
            Ui.Text(screen.SkipText, 15, ink: ColorToken.Muted),
            screen.SkipText,
            _viewModel.Skip,
            null,
            ColorToken.Muted,
            null,
            FooterHeight,
            12
        );
        skip.Padding = new Thickness(16, 0, 16, 0);
        skip.BorderThickness = new Thickness(0);
        skip.Margin = new Thickness(0, 0, 10, 0);
        AutomationProperties.SetAutomationId(skip, "skip");
        DockPanel.SetDock(skip, Dock.Right);
        footer.Children.Add(skip);
        return footer;
    }

    private static CcToggle Segment(
        WelcomeOption option,
        double height,
        double radius,
        double px,
        string automationId,
        Action click
    )
    {
        var text = Ui.Text(
            option.Label,
            px,
            bold: true,
            ink: option.Selected ? ColorToken.OnAccent : ColorToken.Text
        );
        text.HorizontalAlignment = HorizontalAlignment.Center;
        var segment = Ui.Choice(text, option.Label, option.Selected, click, height, radius);
        if (option.Selected)
        {
            CcChrome.Paint(segment, ColorToken.Accent, ColorToken.OnAccent, null);
        }
        else
        {
            CcChrome.Paint(segment, ColorToken.CardHi, ColorToken.Text, null);
        }

        segment.BorderThickness = new Thickness(0);
        segment.HorizontalContentAlignment = HorizontalAlignment.Center;
        AutomationProperties.SetAutomationId(segment, automationId);
        return segment;
    }

    private static TextBlock Heading(string text, double px)
    {
        var heading = Ui.Text(text, px, bold: true, wrap: true);
        heading.LineHeight = px * 1.15;
        AutomationProperties.SetHeadingLevel(heading, AutomationHeadingLevel.Level1);
        return heading;
    }

    private static TextBlock Paragraph(string text, double px, ColorToken ink)
    {
        var paragraph = Ui.Text(text, px, ink: ink, wrap: true);
        paragraph.LineHeight = px * 1.5;
        return paragraph;
    }

    private static TextBlock Fixed(string text, double px)
    {
        // The sample of each size is drawn at real scale: its type size does not follow the text size setting.
        var block = new TextBlock
        {
            Text = text,
            FontSize = px,
            FontWeight = FontWeights.Bold,
            VerticalAlignment = VerticalAlignment.Center,
        };
        block.SetResourceReference(TextBlock.FontFamilyProperty, ThemeKeys.UiFont);
        Ui.Ink(block, TextBlock.ForegroundProperty, ColorToken.Text);
        return block;
    }

    private static TextBlock Centered(TextBlock block)
    {
        block.HorizontalAlignment = HorizontalAlignment.Center;
        return block;
    }
}
