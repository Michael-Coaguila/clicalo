using System.ComponentModel;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Clicalo.Domain.Guides;
using Clicalo.Presentation.ControlCenter.About;
using Clicalo.UI.Wpf.Controls;
using Clicalo.UI.Wpf.Guides;
using Clicalo.UI.Wpf.Theming.Generated;
using Clicalo.UI.Wpf.Workspace.General;
using Clicalo.UI.Wpf.Workspace.Internal;
using Clicalo.UI.Wpf.Workspace.Welcome;

namespace Clicalo.UI.Wpf.Workspace.About;

/// <summary>
/// «Acerca de y contacto» (docs/05 §6, ACE-001 to ACE-005) in direction C (D12): the title, the story with the
/// signature and the card of the app, then the feedback form and «Escríbeme directamente», all in the same outlined
/// groups as «General y panel». From 1240 of window width (this view at least 1000 wide) the story and the app sit in
/// two columns (1.4 : 1), and so do the form and the direct contact; below it everything is one column. The buttons
/// follow the four variants: the app's links are secondary, sending is the primary. The view is rebuilt from
/// <see cref="AboutViewModel.Screen"/>, except while only the message changes, so typing is never interrupted; the
/// keyboard stays on the control with the same automation id.
/// </summary>
public sealed class AboutSectionView : Border
{
    /// <summary>The window width from which the page has two columns (ACE-001).</summary>
    private const double WideFrom = 1240;

    private const string MessageId = "about.message";

    private readonly AboutViewModel _viewModel;
    private readonly ThemedScrollViewer _scroll = new();
    private readonly TextInput _message;
    private AboutScreen? _shown;
    private bool _wide;
    private bool _previewShown;

    /// <summary>Creates the view of <paramref name="viewModel"/>.</summary>
    /// <param name="viewModel">The section.</param>
    public AboutSectionView(AboutViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        _viewModel = viewModel;
        var screen = viewModel.Screen;
        _message = new TextInput(screen.MessageName, screen.MessagePlaceholder, multiline: true)
        {
            DictateName = screen.DictateName,
        };

        // ACE-002: an area of five lines.
        _message.Box.MinHeight = (TextStyles.Body.LineHeight * 5) + (2 * Space.S3);
        _message.Changed += (_, _) => _viewModel.SetMessage(_message.Text);
        _message.DictateRequested += (_, _) => _viewModel.Dictate();
        AutomationProperties.SetAutomationId(_message.Box, MessageId);
        AutomationProperties.SetAutomationId(_message.Dictate, "about.dictate");
        Child = _scroll;
        viewModel.PropertyChanged += OnChanged;
        SizeChanged += (_, _) =>
        {
            var wide = SettingsUi.WindowWidth(this) >= WideFrom;
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
        if (_message.Parent is Panel parent)
        {
            parent.Children.Remove(_message);
        }

        // The column adds its gap to the margin of each child: the field is reused, so it starts from zero.
        _message.Margin = new Thickness(0);
        AutomationProperties.SetName(_message.Box, screen.MessageName);
        _message.DictateName = screen.DictateName;
        var (title, subtitle) = SettingsUi.NewHeader();
        title.Text = screen.Title;
        subtitle.Text = screen.Subtitle;
        var content = Ui.Column(
            Space.S6,
            SettingsUi.PageHeader(title, subtitle),
            Split(Story(screen), AppCard(screen), 1.4, 1),
            Split(
                GuideTargets.Mark(Feedback(screen), GuideTarget.ControlCenterFeedback),
                Direct(screen),
                1.4,
                1
            )
        );
        content.Margin = new Thickness(Space.S6);
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

    /// <summary>ACE-001: the story in two paragraphs and the signature.</summary>
    private static Border Story(AboutScreen screen)
    {
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
        var story = Ui.Column(
            Space.S3,
            SettingsUi.Styled(screen.Story1, TextStyleToken.Body, lines: 0),
            SettingsUi.Styled(screen.Story2, TextStyleToken.Body, ColorToken.Muted, lines: 0),
            signature
        );
        return SettingsUi.Frame(story, screen.CreatorName);
    }

    /// <summary>
    /// ACE-001: the card of the app: the logo, the name and «v{version} · MIT · código abierto» in the interface font,
    /// and its links as secondary buttons of the same width ([GitHub], [LinkedIn] while it has an address, [shareShort]).
    /// </summary>
    private Border AppCard(AboutScreen screen)
    {
        var identity = Ui.Row(
            Space.S3,
            BrandMark.Mark(ControlHeight.Medium),
            Ui.Column(
                0,
                BrandMark.Word(screen.AppName, TextStyles.Get(TextStyleToken.Title).Size),
                SettingsUi.Styled(screen.VersionLine, TextStyleToken.Caption, ColorToken.Muted)
            )
        );
        var gitHub = Link(
            screen.GitHubText,
            "code",
            "about.github",
            () => _ = _viewModel.OpenRepositoryAsync()
        );
        TouchButton? linkedIn = null;
        if (screen.LinkedInText is { } linkedInText)
        {
            // ACE-001, D11: only while there is a real address.
            linkedIn = Link(
                linkedInText,
                "work",
                "about.linkedin",
                () => _ = _viewModel.OpenLinkedInAsync()
            );
        }

        var share = Link(screen.ShareText, "volunteer_activism", "about.share", _viewModel.Share);
        if (_wide)
        {
            // In its narrow column the links fill its width, one under the other.
            return SettingsUi.Frame(
                Ui.Column(Space.S3, identity, gitHub, linkedIn, share),
                screen.AppName
            );
        }

        // Across the whole page the links sit side by side at their own width (they wrap when the window is narrow),
        // never a button as wide as the window.
        List<UIElement> row = [gitHub];
        if (linkedIn is not null)
        {
            row.Add(linkedIn);
        }

        row.Add(share);
        var links = Ui.Wrap(Space.S2, row);
        return SettingsUi.Frame(Ui.Column(Space.S3, identity, links), screen.AppName);

        static TouchButton Link(string text, string symbol, string id, Action click)
        {
            var button = SettingsUi.Button(text, ButtonVariant.Secondary, click, symbol);
            AutomationProperties.SetAutomationId(button, id);
            return button;
        }
    }

    private Border Feedback(AboutScreen screen)
    {
        var kinds = new List<UIElement>();
        foreach (var option in screen.Kinds)
        {
            var kind = SettingsUi.Option(
                OptionKind.Tile,
                option.Label,
                option.Label,
                option.Selected,
                () => _viewModel.SetKind(option.Kind),
                option.Icon
            );
            kind.Height = ControlHeight.Medium;
            kind.HorizontalContentAlignment = HorizontalAlignment.Left;
            AutomationProperties.SetAutomationId(kind, "about.kind." + option.Kind);
            kinds.Add(kind);
        }

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
        var preview = SettingsUi.Button(
            screen.PreviewTitle,
            ButtonVariant.Tertiary,
            () => _ = _viewModel.TogglePreviewAsync(),
            screen.PreviewOpen ? "expand_less" : "expand_more"
        );
        preview.IsExpanded = screen.PreviewOpen;
        preview.HorizontalAlignment = HorizontalAlignment.Left;
        AutomationProperties.SetAutomationId(preview, "about.preview");
        UIElement? previewText = null;
        if (screen.PreviewOpen)
        {
            // The exact log that would be sent (ACE-003): in its own scrolling box, never cut.
            var text = SettingsUi.Styled(
                screen.PreviewText,
                TextStyleToken.Caption,
                ColorToken.Muted,
                lines: 0
            );
            AutomationProperties.SetName(text, screen.PreviewText);
            var box = new Border
            {
                Child = new ThemedScrollViewer { Content = text },
                CornerRadius = new CornerRadius(Radii.Medium),
                Padding = new Thickness(Space.S3),
                MaxHeight = ControlHeight.Large * 5,
            };
            box.SetResourceReference(BorderThicknessProperty, ThemeScope.BorderThicknessKey);
            Ui.Ink(box, BackgroundProperty, ColorToken.Field);
            Ui.Ink(box, BorderBrushProperty, ColorToken.Line);
            previewText = box;
            if (!_previewShown)
            {
                // Only when it opens: the log comes into view under the button that opened it.
                box.Loaded += (_, _) => box.BringIntoView();
            }
        }

        _previewShown = screen.PreviewOpen;

        var send = SettingsUi.Button(
            screen.SendText,
            ButtonVariant.Primary,
            () => _ = _viewModel.SendAsync(),
            "send",
            ControlSize.Medium
        );
        send.IsEnabled = !screen.Sending;
        send.HorizontalAlignment = HorizontalAlignment.Left;
        AutomationProperties.SetAutomationId(send, "about.send");
        AutomationProperties.SetHelpText(send, screen.SendNote);
        return SettingsUi.Group(
            screen.FeedbackTitle,
            SettingsUi.Options(2, kinds),
            _message,
            Ui.Column(0, log, system),
            preview,
            previewText,
            send,
            SettingsUi.Styled(screen.SendNote, TextStyleToken.Caption, ColorToken.Muted, 0)
        );
    }

    /// <summary>
    /// ACE-005: «Escríbeme directamente» is one group like the form: its <see cref="Overline"/> inside, the promise as
    /// text and the places outside the app as navigation rows (the second border).
    /// </summary>
    private Border Direct(AboutScreen screen)
    {
        // ACE-005, D11: the email and its copy button only while there is a real address.
        DockPanel? email = null;
        if (screen.Email is { } address)
        {
            var copy = SettingsUi.IconAction("content_copy", screen.CopyName, _viewModel.CopyEmail);
            AutomationProperties.SetAutomationId(copy, "about.copyEmail");
            email = SettingsUi.SettingRow("mail", address, null, copy);
        }

        var promise = SettingsUi.Styled(
            screen.Promise,
            TextStyleToken.BodySmall,
            ColorToken.Muted,
            lines: 0
        );
        return SettingsUi.Group(
            screen.DirectTitle,
            email,
            promise,
            Link(
                "help",
                screen.GuideTitle,
                screen.GuideDescription,
                "about.guide",
                () => _ = _viewModel.OpenGuideAsync()
            ),
            Link(
                "bug_report",
                screen.IssuesTitle,
                screen.IssuesDescription,
                "about.issues",
                () => _ = _viewModel.OpenIssuesAsync()
            ),
            Link(
                "code",
                screen.ContributeTitle,
                screen.ContributeDescription,
                "about.contribute",
                () => _ = _viewModel.OpenContributeAsync()
            )
        );
    }

    /// <summary>A place outside the app (the guide, the issues, the repository): a navigation row.</summary>
    private static NavRow Link(
        string icon,
        string title,
        string description,
        string automationId,
        Action click
    )
    {
        var row = new NavRow
        {
            Content = title,
            Caption = description,
            Symbol = icon,
        };
        AutomationProperties.SetName(row, title);
        AutomationProperties.SetHelpText(row, description);
        AutomationProperties.SetAutomationId(row, automationId);
        row.Click += (_, _) => click();
        return row;
    }

    private Grid Split(UIElement first, UIElement second, double firstWeight, double secondWeight)
    {
        var grid = new Grid();
        if (_wide)
        {
            grid.ColumnDefinitions.Add(
                new ColumnDefinition { Width = new GridLength(firstWeight, GridUnitType.Star) }
            );
            grid.ColumnDefinitions.Add(
                new ColumnDefinition { Width = new GridLength(secondWeight, GridUnitType.Star) }
            );
            if (first is FrameworkElement left)
            {
                left.Margin = new Thickness(0, 0, Space.S4 / 2, 0);
                left.VerticalAlignment = VerticalAlignment.Top;
            }

            // Each card ends where its content ends: the shorter one never stretches into an empty band.
            if (second is FrameworkElement right)
            {
                right.Margin = new Thickness(Space.S4 / 2, 0, 0, 0);
                right.VerticalAlignment = VerticalAlignment.Top;
            }

            Grid.SetColumn(second, 1);
        }
        else
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            if (second is FrameworkElement below)
            {
                below.Margin = new Thickness(0, Space.S6, 0, 0);
            }

            Grid.SetRow(second, 1);
        }

        grid.Children.Add(first);
        grid.Children.Add(second);
        return grid;
    }
}
