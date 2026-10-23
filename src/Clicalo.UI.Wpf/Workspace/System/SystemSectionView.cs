using System.ComponentModel;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using Clicalo.Presentation.ControlCenter.SystemSection;
using Clicalo.UI.Wpf.Controls;
using Clicalo.UI.Wpf.Theming.Generated;
using Clicalo.UI.Wpf.Workspace.General;
using Clicalo.UI.Wpf.Workspace.Internal;

namespace Clicalo.UI.Wpf.Workspace.SystemSection;

/// <summary>
/// «Sistema» (docs/05 §5, SIS-001) in direction C (D12): the title, the three tabs as the tab pattern of the mockup (a
/// track on <c>side</c> whose tabs have the single look of «elegido»: wash, ring and filled icon, with their state in a
/// second line) and, under it, the content of the tab in view, made of the same outlined groups as «General y panel».
/// It only draws <see cref="SystemSectionViewModel.Screen"/> and forwards taps; every target is at least 44 × 44
/// (REG-02) and every control has its name for UI Automation (REG-06). The tabs are a Tab control with three TabItems
/// for UI Automation (ACC-001).
/// </summary>
public sealed class SystemSectionView : Border
{
    private readonly SystemSectionViewModel _viewModel;
    private readonly ContentControl _content = new() { Focusable = false };

    /// <summary>Creates the view of <paramref name="viewModel"/>.</summary>
    /// <param name="viewModel">The section.</param>
    public SystemSectionView(SystemSectionViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        _viewModel = viewModel;
        _content.Margin = new Thickness(Space.S6);
        Child = new ThemedScrollViewer { Content = _content };
        viewModel.PropertyChanged += OnChanged;
        Render();
    }

    /// <summary>Stops following the view model (the window is closing for good).</summary>
    public void Detach() => _viewModel.PropertyChanged -= OnChanged;

    private void OnChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (
            string.Equals(
                e.PropertyName,
                nameof(SystemSectionViewModel.Screen),
                StringComparison.Ordinal
            )
        )
        {
            Render();
        }
    }

    private void Render()
    {
        var screen = _viewModel.Screen;
        var (title, subtitle) = SettingsUi.NewHeader();
        title.Text = screen.Title;
        subtitle.Text = screen.Subtitle;
        UIElement body = screen.Tab switch
        {
            SystemTab.Backups => Backups(screen.Backups),
            SystemTab.Start => Start(screen.Start),
            _ => Updates(screen.Updates),
        };
        _content.Content = Ui.Column(
            Space.S6,
            SettingsUi.PageHeader(title, subtitle),
            Ui.Column(Space.S4, Tabs(screen), body)
        );
    }

    /// <summary>
    /// The tabs (SIS-001): a track of <see cref="ControlHeight.Large"/> tabs, each with its icon, its name and its state
    /// in a second line («Nueva versión» in the warning ink).
    /// </summary>
    private Border Tabs(SystemScreen screen)
    {
        var strip = new SystemTabStrip();
        AutomationProperties.SetName(strip, screen.Title);
        AutomationProperties.SetAutomationId(strip, "system.tabs");
        foreach (var tab in screen.Tabs)
        {
            var texts = Ui.Column(
                0,
                SettingsUi.Styled(tab.Label, TextStyleToken.Label),
                SettingsUi.Styled(
                    tab.Status,
                    TextStyleToken.Caption,
                    tab.Warn ? ColorToken.WarnText : ColorToken.Muted
                )
            );
            var button = new SystemTabItem
            {
                Content = texts,
                IsChecked = tab.Selected,
                Height = ControlHeight.Large,
                Margin = new Thickness(Space.S1 / 2),
                Padding = new Thickness(Space.S3, 0, Space.S3, 0),
                HorizontalContentAlignment = HorizontalAlignment.Left,
                VerticalContentAlignment = VerticalAlignment.Center,
            };
            button.SetValue(OptionButton.SymbolProperty, tab.Icon);
            AutomationProperties.SetName(button, tab.Label);
            AutomationProperties.SetItemStatus(button, tab.Status);
            AutomationProperties.SetAutomationId(button, "system.tab." + tab.Tab);
            AutomationProperties.SetPositionInSet(button, strip.Children.Count + 1);
            AutomationProperties.SetSizeOfSet(button, screen.Tabs.Count);
            var target = tab.Tab;
            button.Click += (_, _) => _viewModel.SelectTab(target);
            strip.ColumnDefinitions.Add(
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }
            );
            Grid.SetColumn(button, strip.Children.Count);
            strip.Children.Add(button);
        }

        var track = new Border
        {
            Child = strip,
            CornerRadius = new CornerRadius(Radii.Medium),
            Padding = new Thickness(Space.S1 / 2),
        };
        track.SetResourceReference(BorderThicknessProperty, ThemeScope.BorderThicknessKey);
        Ui.Ink(track, BackgroundProperty, ColorToken.Side);
        Ui.Ink(track, BorderBrushProperty, ColorToken.Border);
        return track;
    }

    // ---- Actualizaciones -----------------------------------------------------------------------------------------

    private StackPanel Updates(UpdatesModel model)
    {
        var card = model.Card;
        var (wash, ink) =
            card.Warning ? (ColorToken.DangerWash, ColorToken.DangerText)
            : card.Hot ? (ColorToken.WarnWash, ColorToken.WarnText)
            : (ColorToken.AccentWash, ColorToken.AccentInk);
        var title = SettingsUi.Styled(card.Title, TextStyleToken.Heading, lines: 2);
        AutomationProperties.SetLiveSetting(title, AutomationLiveSetting.Polite);
        var head = Ui.Row(
            Space.S3,
            StateIcon(card.Icon, wash, ink),
            Ui.Column(
                0,
                title,
                SettingsUi.Styled(card.Subtitle, TextStyleToken.BodySmall, ColorToken.Muted, 0)
            )
        );
        var button = SettingsUi.Button(
            card.Button,
            card.Hot ? ButtonVariant.Primary : ButtonVariant.Secondary,
            _viewModel.UpdateAction
        );
        button.IsEnabled = card.ButtonEnabled;
        button.HorizontalAlignment = HorizontalAlignment.Left;
        var state = SettingsUi.Frame(
            Ui.Column(Space.S4, head, card.ShowBar ? Progress(card.Percent) : null, button),
            card.Title
        );

        var rows = new List<UIElement>();
        for (var i = 0; i < model.Switches.Count; i++)
        {
            var index = i;
            var item = model.Switches[i];
            rows.Add(
                Ui.SwitchRow(
                    item.Icon,
                    item.Label,
                    item.Description,
                    item.On,
                    () => _viewModel.ToggleUpdateSwitch(index)
                )
            );
        }

        var channels = new List<OptionButton>();
        foreach (var option in model.Channels)
        {
            channels.Add(
                SettingsUi.Option(
                    OptionKind.Segment,
                    option.Label,
                    model.Channel + ": " + option.Label,
                    option.Selected,
                    () => _viewModel.SetChannel(option.Channel)
                )
            );
        }

        rows.Add(
            SettingsUi.SettingRow(
                "science",
                model.Channel,
                model.ChannelDescription,
                SettingsUi.Track(model.Channel, channels)
            )
        );
        // «Lo nuevo» is one group with its Overline inside, like every group of the page; its notes are plain columns
        // in it, so no frame sits inside another.
        return Ui.Column(
            Space.S4,
            state,
            SettingsUi.Frame(Ui.Column(0, [.. rows]), model.Channel),
            model.Notes.IsEmpty
                ? null
                : SettingsUi.Group(model.WhatsNew, [.. model.Notes.Select(Notes)]),
            model.Rollback is { } rollback ? Rollback(rollback) : null
        );
    }

    /// <summary>The square of 44 with the icon of a state: its wash and its ink say what kind of state it is.</summary>
    private static Border StateIcon(string icon, ColorToken wash, ColorToken ink)
    {
        var box = Ui.Card(
            Ui.Icon(icon, IconBox.Medium, ink),
            wash,
            null,
            Radii.Small,
            new Thickness(0)
        );
        box.Width = ControlHeight.Small;
        box.Height = ControlHeight.Small;
        box.VerticalAlignment = VerticalAlignment.Top;
        return box;
    }

    private static Border Progress(int percent)
    {
        var fill = new Border
        {
            HorizontalAlignment = HorizontalAlignment.Left,
            CornerRadius = new CornerRadius(Space.S1),
        };
        Ui.Ink(fill, BackgroundProperty, ColorToken.Accent);
        var track = new Border
        {
            Height = Space.S2,
            CornerRadius = new CornerRadius(Space.S1),
            Child = fill,
        };
        Ui.Ink(track, BackgroundProperty, ColorToken.Line);
        track.SizeChanged += (_, e) =>
            fill.Width = e.NewSize.Width * Math.Clamp(percent, 0, 100) / 100.0;
        AutomationProperties.SetName(
            track,
            percent.ToString(System.Globalization.CultureInfo.CurrentCulture) + " %"
        );
        return track;
    }

    private static StackPanel Notes(NotesModel notes)
    {
        var top = Ui.Row(
            Space.S2,
            SettingsUi.Styled(notes.Version, TextStyleToken.Label),
            notes.Date.Length == 0
                ? null
                : SettingsUi.Styled(notes.Date, TextStyleToken.Caption, ColorToken.Muted),
            notes.NewBadge.Length == 0
                ? null
                : new Badge
                {
                    Text = notes.NewBadge,
                    Tone = BadgeTone.Warn,
                    VerticalAlignment = VerticalAlignment.Center,
                }
        );
        List<UIElement?> column = [top];
        foreach (var item in notes.Items)
        {
            var check = Ui.Icon("check", IconBox.Small, ColorToken.Muted);
            check.VerticalAlignment = VerticalAlignment.Top;
            column.Add(
                Ui.Row(Space.S2, check, SettingsUi.Styled(item, TextStyleToken.BodySmall, lines: 0))
            );
        }

        var note = Ui.Column(Space.S2, [.. column]);
        AutomationProperties.SetName(note, notes.Version);
        return note;
    }

    private Border Rollback(RollbackModel rollback)
    {
        var button = SettingsUi.Button(
            rollback.Button,
            ButtonVariant.Secondary,
            _viewModel.Rollback,
            name: rollback.Title
        );
        button.IsActive = rollback.Armed;
        return SettingsUi.Frame(
            SettingsUi.SettingRow("history", rollback.Title, rollback.Description, button),
            rollback.Title
        );
    }

    // ---- Copias de seguridad -------------------------------------------------------------------------------------

    private StackPanel Backups(BackupsModel model)
    {
        // The folder and the format in the interface font (only keys go in monospace); the path ends in «…».
        var folderIcon = Ui.Icon("folder", IconBox.Small, ColorToken.Muted);
        var folder = new DockPanel { LastChildFill = true };
        folderIcon.Margin = new Thickness(0, 0, Space.S2, 0);
        DockPanel.SetDock(folderIcon, Dock.Left);
        folder.Children.Add(folderIcon);
        var format = SettingsUi.Styled(
            " · " + model.Format,
            TextStyleToken.BodySmall,
            ColorToken.Muted
        );
        DockPanel.SetDock(format, Dock.Right);
        var path = SettingsUi.Styled(model.Folder, TextStyleToken.BodySmall, ColorToken.Muted);
        var line = new StackPanel { Orientation = Orientation.Horizontal };
        line.Children.Add(path);
        line.Children.Add(format);
        folder.Children.Add(line);

        List<UIElement?> column = [];
        var now = SettingsUi.Button(
            model.BackupNow,
            ButtonVariant.Primary,
            _viewModel.BackupNow,
            "backup"
        );
        var export = SettingsUi.Button(
            model.Export,
            ButtonVariant.Secondary,
            _viewModel.Export,
            "upload"
        );
        var importButton = SettingsUi.Button(
            model.Import,
            ButtonVariant.Secondary,
            _viewModel.Import,
            "download"
        );
        importButton.IsActive = model.ImportCard is { CanMerge: true };
        foreach (var button in new[] { now, export, importButton })
        {
            button.IsEnabled = !model.Busy;
        }

        // The folder and what can be done with it are one group, like the switches below; the card of a file being
        // imported opens under its button, outside the group (a card never goes inside a frame).
        column.Add(
            SettingsUi.Frame(
                Ui.Column(Space.S3, folder, Ui.Wrap(Space.S2, [now, export, importButton])),
                model.Folder
            )
        );
        if (model.ImportCard is { CanMerge: true } import)
        {
            column.Add(Import(import));
        }

        var auto = model.Auto;
        column.Add(
            SettingsUi.Frame(
                Ui.SwitchRow(
                    auto.Icon,
                    auto.Label,
                    auto.Description,
                    auto.On,
                    _viewModel.ToggleAutoBackup
                ),
                auto.Label
            )
        );
        if (model.ImportCard is { CanMerge: false } review)
        {
            // LOG-008: the review of the backup being restored opens right above the history it belongs to, in view.
            var card = Import(review);
            card.Loaded += (_, _) => card.BringIntoView();
            column.Add(card);
        }

        UIElement history;
        if (model.Rows.IsEmpty)
        {
            history = SettingsUi.Styled(model.Empty, TextStyleToken.Body, ColorToken.Muted, 0);
        }
        else
        {
            var rows = Ui.Column(0);
            for (var i = 0; i < model.Rows.Count; i++)
            {
                rows.Children.Add(History(model.Rows[i], last: i == model.Rows.Count - 1));
            }

            history = rows;
        }

        column.Add(SettingsUi.Group(model.History, history));
        return Ui.Column(Space.S4, [.. column]);
    }

    private Card Import(ImportCardModel import)
    {
        var merge = SettingsUi.Button(
            import.Merge,
            ButtonVariant.Primary,
            _viewModel.ImportMerge,
            size: ControlSize.Medium
        );
        AutomationProperties.SetHelpText(merge, import.MergeDescription);
        var replace = SettingsUi.Button(
            import.Replace,
            ButtonVariant.Danger,
            _viewModel.ImportReplace,
            import.ReplaceArmed ? "warning" : null,
            ControlSize.Medium
        );
        replace.IsActive = import.ReplaceArmed;
        AutomationProperties.SetHelpText(replace, import.ReplaceDescription);
        AutomationProperties.SetAutomationId(merge, "system.import.merge");
        AutomationProperties.SetAutomationId(replace, "system.import.replace");
        UIElement choices = import.CanMerge
            ? SettingsUi.Options(
                2,
                [Choice(merge, import.MergeDescription), Choice(replace, import.ReplaceDescription)]
            )
            : Choice(replace, import.ReplaceDescription);
        var column = Ui.Column(
            Space.S3,
            SettingsUi.Styled(import.Title, TextStyleToken.Label, lines: 0),
            import.Summary.Length == 0
                ? null
                : SettingsUi.Styled(import.Summary, TextStyleToken.BodySmall, ColorToken.Muted, 0),
            import.Warning.Length == 0
                ? null
                : Ui.Row(
                    Space.S2,
                    Ui.Icon("warning", IconBox.Small, ColorToken.WarnText),
                    SettingsUi.Styled(import.Warning, TextStyleToken.BodySmall, lines: 0)
                ),
            import.Review.IsEmpty ? null : Review(import),
            choices
        );
        var card = new Card { Content = column, Tone = CardTone.Accent };
        AutomationProperties.SetName(card, import.Title);
        return card;

        static StackPanel Choice(TouchButton button, string description) =>
            Ui.Column(
                Space.S1,
                button,
                SettingsUi.Styled(description, TextStyleToken.Caption, ColorToken.Muted, 0)
            );
    }

    /// <summary>
    /// The Web, App and Macro shortcuts of the backup that the document does not have yet (LOG-008): one row of 52 or
    /// more each, unticked by default, with what it opens or runs.
    /// </summary>
    private StackPanel Review(ImportCardModel import)
    {
        var note = Ui.Row(
            Space.S2,
            Ui.Icon("shield", IconBox.Small, ColorToken.WarnText),
            SettingsUi.Styled(import.ReviewNote, TextStyleToken.BodySmall, lines: 0)
        );
        List<UIElement?> rows = [note];
        foreach (var row in import.Review)
        {
            var texts = Ui.Column(
                0,
                SettingsUi.Styled(row.Name, TextStyleToken.Label),
                // LOG-008: the whole address, target or steps, wrapped, never cut with «…».
                row.Detail.Length == 0
                    ? null
                    : SettingsUi.Styled(
                        row.Detail,
                        TextStyleToken.Caption,
                        ColorToken.Muted,
                        lines: 0
                    )
            );
            var id = row.Id;
            var toggle = SettingsUi.Tall(
                SettingsUi.Option(
                    OptionKind.Tile,
                    texts,
                    row.Name,
                    row.Checked,
                    () => _viewModel.ToggleReview(id),
                    row.Icon,
                    OptionRole.Toggle
                ),
                ControlHeight.Medium
            );
            AutomationProperties.SetHelpText(toggle, row.Detail);
            toggle.HorizontalContentAlignment = HorizontalAlignment.Left;
            AutomationProperties.SetHelpText(toggle, row.Detail);
            AutomationProperties.SetAutomationId(toggle, "system.review." + row.Id.Value);
            rows.Add(toggle);
        }

        return Ui.Column(Space.S2, [.. rows]);
    }

    private Border History(BackupRowModel row, bool last)
    {
        var button = SettingsUi.Button(
            row.Button,
            ButtonVariant.Secondary,
            () => _viewModel.Restore(row.Id),
            name: row.Button + " " + row.Date
        );
        button.IsActive = row.Armed;
        var line = SettingsUi.SettingRow("history", row.Date, row.Meta, button);
        var border = new Border
        {
            Child = line,
            BorderThickness = new Thickness(0, 0, 0, last ? 0 : Stroke.Line),
        };
        Ui.Ink(border, BorderBrushProperty, ColorToken.Border);
        return border;
    }

    // ---- Inicio y estabilidad ------------------------------------------------------------------------------------

    private StackPanel Start(StartModel model)
    {
        var start = model.StartWithWindows;
        var admin = model.Admin;
        UIElement trailing;
        if (admin.CanReopen)
        {
            trailing = SettingsUi.Button(
                admin.Button,
                ButtonVariant.Secondary,
                _viewModel.ReopenAsAdmin,
                "admin_panel_settings",
                name: admin.Title
            );
        }
        else
        {
            trailing = Ui.Icon("check_circle", IconBox.Medium, ColorToken.Success);
        }

        var status = SettingsUi.Styled(model.CrashStatus, TextStyleToken.Label);
        var rows = SettingsUi.Frame(
            Ui.Column(
                0,
                Ui.SwitchRow(
                    start.Icon,
                    start.Label,
                    start.Description,
                    start.On,
                    _viewModel.ToggleStartWithWindows
                ),
                Named(
                    SettingsUi.SettingRow(
                        "admin_panel_settings",
                        admin.Title,
                        admin.Description,
                        trailing
                    ),
                    admin.Title
                ),
                Named(
                    SettingsUi.SettingRow("healing", model.Crash, model.CrashDescription, status),
                    model.Crash
                )
            ),
            start.Label
        );
        return Ui.Column(Space.S4, rows, Uninstall(model.Uninstall));
    }

    private static T Named<T>(T element, string name)
        where T : FrameworkElement
    {
        AutomationProperties.SetName(element, name);
        return element;
    }

    /// <summary>
    /// «Desinstalar Clícalo» (NFR-010, P6): the data is kept unless the switch is on, and the button takes two taps
    /// (REG-04); it is a danger button, drawn pressed while armed.
    /// </summary>
    private Border Uninstall(UninstallModel model)
    {
        var button = SettingsUi.Button(
            model.Button,
            ButtonVariant.Danger,
            _viewModel.Uninstall,
            model.Armed ? "warning" : "delete",
            name: model.Title
        );
        button.IsActive = model.Armed;
        button.IsEnabled = model.Available;
        AutomationProperties.SetAutomationId(button, "system.uninstall");
        AutomationProperties.SetHelpText(button, model.Description);
        var wipe = Ui.SwitchRow(
            model.DeleteData.Icon,
            model.DeleteData.Label,
            model.DeleteData.Description,
            model.DeleteData.On,
            _viewModel.ToggleDeleteData
        );
        wipe.IsEnabled = model.Available;
        AutomationProperties.SetAutomationId(wipe, "system.uninstall.deleteData");
        return SettingsUi.Frame(
            Ui.Column(
                0,
                SettingsUi.SettingRow("delete", model.Title, model.Description, button),
                wipe
            ),
            model.Title
        );
    }
}
