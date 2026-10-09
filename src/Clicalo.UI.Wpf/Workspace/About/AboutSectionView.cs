using System.ComponentModel;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Clicalo.Presentation.ControlCenter.About;
using Clicalo.UI.Wpf.Theming.Generated;
using Clicalo.UI.Wpf.Welcome;
using Clicalo.UI.Wpf.Workspace.Internal;

namespace Clicalo.UI.Wpf.Workspace.About;

/// <summary>
/// «Acerca de y contacto» (docs/05 §6, ACE-001 to ACE-005): title, the story card with the signature and the app card,
/// then the feedback form and «Escríbeme directamente». From 1240 of window width (this view at least 1020 wide) the
/// story and the app card sit in two columns (1.4 : 1), and so do the form and the direct contact; below it everything
/// is one column. The view is rebuilt from <see cref="AboutViewModel.Screen"/>, except while only the message changes,
/// so typing is never interrupted; the keyboard stays on the control with the same automation id.
/// </summary>
public sealed class AboutSectionView : Border
{
    private const double WideFrom = 1020;
    private const string MessageId = "about.message";

    private readonly AboutViewModel _viewModel;
    private readonly TouchPanScrollViewer _scroll = new() { Padding = new Thickness(24) };
    private readonly TextField _message;
    private readonly Grid _messageBox = new();
    private AboutScreen? _shown;
    private bool _wide;

    /// <summary>Creates the view of <paramref name="viewModel"/>.</summary>
    /// <param name="viewModel">The section.</param>
    public AboutSectionView(AboutViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        _viewModel = viewModel;
        var screen = viewModel.Screen;
        _message = new TextField(screen.MessageName, screen.MessagePlaceholder, multiline: true);
        _message.Box.MinHeight = 130;
        _message.Box.Padding = new Thickness(14, 12, 56, 12);
        _message.Changed += (_, _) => _viewModel.SetMessage(_message.Text);
        _messageBox.Children.Add(_message);
        Child = _scroll;
        viewModel.PropertyChanged += OnChanged;
        SizeChanged += (_, e) =>
        {
            var wide = e.NewSize.Width >= WideFrom;
            if (wide != _wide)
            {
                _wide = wide;
                Render(force: true);
            }
        };
        Render(force: true);
    }

    /// <summary>Stops following the view model (the window is closing for good).</summary>
    public void Detach() => _viewModel.PropertyChanged -= OnChanged;

    private void OnChanged(object? sender, PropertyChangedEventArgs e) => Render(force: false);

    private void Render(bool force)
    {
        var screen = _viewModel.Screen;
        _message.Show(screen.Message);
        if (!force && _shown is not null && _shown with { Message = screen.Message } == screen)
        {
            _shown = screen;
            return;
        }

        _shown = screen;
        var focused =
            Keyboard.FocusedElement is FrameworkElement element && IsAncestorOf(element)
                ? AutomationProperties.GetAutomationId(element)
                : null;
        if (_messageBox.Parent is Panel parent)
        {
            parent.Children.Remove(_messageBox);
        }

        // The column adds its gap to the margin of each child: the field is reused, so it starts from zero.
        _messageBox.Margin = new Thickness(0);

        AutomationProperties.SetName(_message.Box, screen.MessageName);
        var content = Ui.Column(
            24,
            Ui.Column(
                4,
                Ui.Text(screen.Title, 24, bold: true, wrap: true),
                Ui.Text(screen.Subtitle, 14, ink: ColorToken.Muted, wrap: true)
            ),
            StoryCard(screen),
            Split(Feedback(screen), Direct(screen), 1, 1)
        );
        _scroll.Content = content;
        if (
            !string.IsNullOrEmpty(focused)
            && !string.Equals(focused, MessageId, StringComparison.Ordinal)
        )
        {
            _ = Dispatcher.BeginInvoke(() => Refocus(content, focused));
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

    private Border StoryCard(AboutScreen screen)
    {
        var signature = Ui.Row(
            10,
            BrandMark.Avatar(screen.CreatorInitials, 40, 14),
            Ui.Column(
                0,
                Ui.Text(screen.CreatorName, 15, bold: true),
                Ui.Text(screen.CreatorRole, 12, ink: ColorToken.Muted)
            )
        );
        var story = Ui.Column(
            12,
            Paragraph(screen.Story1, 18, ColorToken.Text),
            Paragraph(screen.Story2, 16, ColorToken.Muted),
            signature
        );
        return Ui.Card(
            Split(story, AppCard(screen), 1.4, 1),
            ColorToken.Card,
            null,
            16,
            new Thickness(22)
        );
    }

    private Border AppCard(AboutScreen screen)
    {
        var identity = Ui.Row(
            12,
            BrandMark.Mark(48),
            Ui.Column(
                2,
                BrandMark.Word(screen.AppName, 18),
                Ui.Text(screen.VersionLine, 12, ink: ColorToken.Muted, mono: true)
            )
        );
        var gitHub = Ui.Button(
            Ui.IconLabel("code", screen.GitHubText, 18, 14),
            screen.GitHubText,
            () => _ = _viewModel.OpenRepositoryAsync(),
            null,
            ColorToken.Text,
            ColorToken.Border
        );
        AutomationProperties.SetAutomationId(gitHub, "about.github");
        var share = Ui.Button(
            Ui.IconLabel(
                "volunteer_activism",
                screen.ShareText,
                18,
                14,
                iconInk: ColorToken.Accent
            ),
            screen.ShareText,
            _viewModel.Share,
            ColorToken.AccentWash
        );
        share.BorderThickness = new Thickness(0);
        AutomationProperties.SetAutomationId(share, "about.share");
        var card = Ui.Card(
            Ui.Column(10, identity, gitHub, share),
            ColorToken.Side,
            null,
            14,
            new Thickness(16)
        );
        card.VerticalAlignment = VerticalAlignment.Center;
        return card;
    }

    private StackPanel Feedback(AboutScreen screen)
    {
        var kinds = new List<UIElement>();
        foreach (var option in screen.Kinds)
        {
            var kind = Ui.Choice(
                Ui.IconLabel(option.Icon, option.Label, 22, 14, iconInk: ColorToken.Accent),
                option.Label,
                option.Selected,
                () => _viewModel.SetKind(option.Kind),
                52,
                12
            );
            kind.Height = double.NaN;
            kind.MinHeight = 52;
            kind.Padding = new Thickness(12, 0, 12, 0);
            kind.HorizontalContentAlignment = HorizontalAlignment.Left;
            AutomationProperties.SetAutomationId(kind, "about.kind." + option.Kind);
            kinds.Add(kind);
        }

        var dictate = Ui.Button(
            Ui.Icon("mic", 22, ColorToken.Accent),
            screen.DictateName,
            () =>
            {
                _ = Keyboard.Focus(_message.Box);
                _viewModel.Dictate();
            },
            ColorToken.AccentWash
        );
        dictate.Width = 44;
        dictate.Padding = new Thickness(0);
        dictate.BorderThickness = new Thickness(0);
        dictate.HorizontalAlignment = HorizontalAlignment.Right;
        dictate.VerticalAlignment = VerticalAlignment.Top;
        dictate.Margin = new Thickness(0, 8, 8, 0);
        AutomationProperties.SetAutomationId(dictate, "about.dictate");
        AutomationProperties.SetAutomationId(_message.Box, MessageId);
        for (var i = _messageBox.Children.Count - 1; i > 0; i--)
        {
            _messageBox.Children.RemoveAt(i);
        }

        _messageBox.Children.Add(dictate);

        var log = Ui.SwitchRow(
            "description",
            screen.LogTitle,
            screen.LogDescription,
            screen.AttachLog,
            _viewModel.ToggleLog
        );
        AutomationProperties.SetAutomationId(log, "about.log");
        var system = Ui.SwitchRow(
            "computer",
            screen.SystemTitle,
            screen.SystemDescription,
            screen.IncludeSystem,
            _viewModel.ToggleSystem
        );
        AutomationProperties.SetAutomationId(system, "about.system");
        var previewRow = new DockPanel { LastChildFill = true };
        var caret = Ui.Icon(
            screen.PreviewOpen ? "expand_less" : "expand_more",
            18,
            ColorToken.Muted
        );
        DockPanel.SetDock(caret, Dock.Right);
        previewRow.Children.Add(caret);
        previewRow.Children.Add(
            Ui.IconLabel("visibility", screen.PreviewTitle, 18, 13, iconInk: ColorToken.Accent)
        );
        var preview = new CcToggle
        {
            Content = previewRow,
            IsChecked = screen.PreviewOpen,
            MinHeight = 44,
            Padding = new Thickness(12, 0, 12, 0),
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
        };
        CcChrome.Paint(preview, null, ColorToken.Text, ColorToken.Border);
        preview.SetValue(CcChrome.RadiusProperty, new CornerRadius(10));
        AutomationProperties.SetName(preview, screen.PreviewTitle);
        AutomationProperties.SetAutomationId(preview, "about.preview");
        preview.Click += (_, _) => _ = _viewModel.TogglePreviewAsync();
        Border? previewText = null;
        if (screen.PreviewOpen)
        {
            var text = Ui.Text(
                screen.PreviewText,
                11,
                ink: ColorToken.Muted,
                mono: true,
                wrap: true
            );
            text.LineHeight = 11 * 1.6;
            AutomationProperties.SetName(text, screen.PreviewText);
            previewText = Ui.Card(
                text,
                ColorToken.Field,
                ColorToken.Border,
                10,
                new Thickness(12, 10, 12, 10)
            );
            previewText.MaxHeight = 320;
        }

        var send = Ui.Button(
            Ui.IconLabel("send", screen.SendText, 22, 16, ink: ColorToken.OnAccent),
            screen.SendText,
            () => _ = _viewModel.SendAsync(),
            ColorToken.Accent,
            ColorToken.OnAccent,
            null,
            52,
            12
        );
        send.BorderThickness = new Thickness(0);
        send.IsEnabled = !screen.Sending;
        AutomationProperties.SetAutomationId(send, "about.send");
        AutomationProperties.SetHelpText(send, screen.SendNote);
        var note = Ui.Text(screen.SendNote, 12, ink: ColorToken.Muted, wrap: true);
        note.TextAlignment = TextAlignment.Center;
        return Ui.Column(
            12,
            Ui.Caption(screen.FeedbackTitle),
            Ui.Columns(2, 8, kinds),
            _messageBox,
            log,
            system,
            preview,
            previewText,
            send,
            note
        );
    }

    private StackPanel Direct(AboutScreen screen)
    {
        var email = Ui.Text(
            screen.Email,
            13,
            ink: screen.EmailPending ? ColorToken.Muted : ColorToken.Text,
            mono: !screen.EmailPending
        );
        var emailRow = new DockPanel { LastChildFill = true };
        var mail = Ui.Icon("mail", 20, ColorToken.Accent);
        mail.Margin = new Thickness(0, 0, 8, 0);
        DockPanel.SetDock(mail, Dock.Left);
        emailRow.Children.Add(mail);
        if (!screen.EmailPending)
        {
            var copy = Ui.Button(
                Ui.Icon("content_copy", 18),
                screen.CopyName,
                _viewModel.CopyEmail,
                ColorToken.CardHi
            );
            copy.Width = 44;
            copy.Padding = new Thickness(0);
            copy.BorderThickness = new Thickness(0);
            AutomationProperties.SetAutomationId(copy, "about.copyEmail");
            DockPanel.SetDock(copy, Dock.Right);
            emailRow.Children.Add(copy);
        }

        emailRow.Children.Add(email);
        var emailBox = Ui.Card(
            emailRow,
            ColorToken.Field,
            ColorToken.Border,
            10,
            new Thickness(12, 6, 6, 6)
        );
        emailBox.MinHeight = 56;
        var card = Ui.Card(
            Ui.Column(10, emailBox, Ui.Text(screen.Promise, 13, ink: ColorToken.Muted, wrap: true)),
            ColorToken.Card,
            null,
            12,
            new Thickness(16)
        );
        return Ui.Column(
            10,
            Ui.Caption(screen.DirectTitle),
            card,
            LinkRow(
                "bug_report",
                screen.IssuesTitle,
                screen.IssuesDescription,
                "about.issues",
                () => _ = _viewModel.OpenIssuesAsync()
            ),
            LinkRow(
                "code",
                screen.ContributeTitle,
                screen.ContributeDescription,
                "about.contribute",
                () => _ = _viewModel.OpenContributeAsync()
            )
        );
    }

    private static CcButton LinkRow(
        string icon,
        string title,
        string description,
        string automationId,
        Action click
    )
    {
        var row = new DockPanel { LastChildFill = true };
        var lead = Ui.Icon(icon, 22, ColorToken.Accent);
        lead.Margin = new Thickness(0, 0, 12, 0);
        DockPanel.SetDock(lead, Dock.Left);
        row.Children.Add(lead);
        var open = Ui.Icon("open_in_new", 20, ColorToken.Muted);
        open.Margin = new Thickness(12, 0, 0, 0);
        DockPanel.SetDock(open, Dock.Right);
        row.Children.Add(open);
        row.Children.Add(
            Ui.Column(
                2,
                Ui.Text(title, 15, bold: true, wrap: true),
                Ui.Text(description, 13, ink: ColorToken.Muted, wrap: true)
            )
        );
        var button = Ui.Button(row, title, click, null, ColorToken.Text, ColorToken.Border, 44, 12);
        button.Height = double.NaN;
        button.MinHeight = 56;
        button.Padding = new Thickness(16, 14, 16, 14);
        button.HorizontalContentAlignment = HorizontalAlignment.Stretch;
        AutomationProperties.SetHelpText(button, description);
        AutomationProperties.SetAutomationId(button, automationId);
        return button;
    }

    private Grid Split(UIElement first, UIElement second, double firstWeight, double secondWeight)
    {
        var grid = new Grid();
        if (_wide)
        {
            grid.ColumnDefinitions.Add(
                new ColumnDefinition { Width = new GridLength(firstWeight, GridUnitType.Star) }
            );
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(24) });
            grid.ColumnDefinitions.Add(
                new ColumnDefinition { Width = new GridLength(secondWeight, GridUnitType.Star) }
            );
            Grid.SetColumn(second, 2);
        }
        else
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(24) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            Grid.SetRow(second, 2);
        }

        grid.Children.Add(first);
        grid.Children.Add(second);
        return grid;
    }

    private static TextBlock Paragraph(string text, double px, ColorToken ink)
    {
        var paragraph = Ui.Text(text, px, ink: ink, wrap: true);
        paragraph.LineHeight = px * 1.55;
        return paragraph;
    }
}
