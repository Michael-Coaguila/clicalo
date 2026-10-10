using System.ComponentModel;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using Clicalo.Presentation.ControlCenter.SystemSection;
using Clicalo.UI.Wpf.Theming.Generated;
using Clicalo.UI.Wpf.Workspace.Internal;

namespace Clicalo.UI.Wpf.Workspace.SystemSection;

/// <summary>
/// «Sistema» (docs/05 §5, SIS-001): the title, then one card with the three tabs joined to their content. The active
/// tab has the background of the content, a 3 px accent line under it, the filled icon and ▴; the others have the
/// side background, a bottom border and ▾. It only draws <see cref="SystemSectionViewModel.Screen"/> and forwards
/// taps; every target is at least 44 × 44 (REG-02) and every control has its name for UI Automation (REG-06). The
/// tabs are a Tab control with three TabItems for UI Automation (ACC-001).
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
        Child = new TouchPanScrollViewer { Content = _content, Padding = new Thickness(24) };
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
        var heading = Ui.Column(
            4,
            Ui.Text(screen.Title, 24, bold: true),
            Ui.Text(screen.Subtitle, 14, ink: ColorToken.Muted, wrap: true)
        );
        var body = screen.Tab switch
        {
            SystemTab.Backups => Backups(screen.Backups),
            SystemTab.Start => Start(screen.Start),
            _ => Updates(screen.Updates),
        };
        var inner = new Border { Child = body, Padding = new Thickness(20) };
        Ui.Ink(inner, BackgroundProperty, ColorToken.Win);
        var card = Ui.Card(
            Ui.Column(0, Tabs(screen), inner),
            null,
            ColorToken.Border,
            16,
            new Thickness(0)
        );
        card.ClipToBounds = true;
        _content.Content = Ui.Column(20, heading, card);
    }

    private SystemTabStrip Tabs(SystemScreen screen)
    {
        var strip = new SystemTabStrip();
        AutomationProperties.SetName(strip, screen.Title);
        AutomationProperties.SetAutomationId(strip, "system.tabs");
        Ui.Ink(strip, Panel.BackgroundProperty, ColorToken.Side);
        foreach (var tab in screen.Tabs)
        {
            var iconFill =
                tab.Warn ? ColorToken.WarnWash
                : tab.Selected ? ColorToken.Accent
                : ColorToken.Card;
            var iconInk =
                tab.Warn ? ColorToken.Warn
                : tab.Selected ? ColorToken.OnAccent
                : ColorToken.Muted;
            var icon = Ui.Card(
                Ui.Icon(tab.Icon, 22, iconInk),
                iconFill,
                null,
                11,
                new Thickness(0)
            );
            icon.Width = 40;
            icon.Height = 40;
            var texts = Ui.Column(
                2,
                Ui.Text(
                    tab.Label,
                    15,
                    bold: true,
                    ink: tab.Selected ? ColorToken.Text : ColorToken.Muted
                ),
                Ui.Text(tab.Status, 13, ink: tab.Warn ? ColorToken.Warn : ColorToken.Muted)
            );
            var chevron = Ui.Icon(
                tab.Selected ? "expand_less" : "expand_more",
                20,
                ColorToken.Muted
            );
            var row = new DockPanel { LastChildFill = true };
            DockPanel.SetDock(icon, Dock.Left);
            row.Children.Add(icon);
            chevron.Margin = new Thickness(8, 0, 0, 0);
            DockPanel.SetDock(chevron, Dock.Right);
            row.Children.Add(chevron);
            texts.Margin = new Thickness(12, 0, 0, 0);
            texts.VerticalAlignment = VerticalAlignment.Center;
            row.Children.Add(texts);
            var bar = new Border
            {
                Height = 3,
                CornerRadius = new CornerRadius(3, 3, 0, 0),
                Margin = new Thickness(12, 0, 12, 0),
                VerticalAlignment = VerticalAlignment.Bottom,
            };
            if (tab.Selected)
            {
                Ui.Ink(bar, BackgroundProperty, ColorToken.Accent);
            }

            var layers = new Grid();
            layers.Children.Add(new Border { Child = row, Padding = new Thickness(16) });
            layers.Children.Add(bar);
            var button = new SystemTabItem
            {
                Content = layers,
                IsChecked = tab.Selected,
                Padding = new Thickness(0),
                BorderThickness = new Thickness(0, 0, 0, tab.Selected ? 0 : 1),
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                VerticalContentAlignment = VerticalAlignment.Stretch,
                MinHeight = 72,
            };
            CcChrome.Paint(
                button,
                tab.Selected ? ColorToken.Win : ColorToken.Side,
                ColorToken.Text,
                ColorToken.Border
            );
            button.SetValue(CcChrome.RadiusProperty, new CornerRadius(0));
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

        return strip;
    }

    private StackPanel Updates(UpdatesModel model)
    {
        var card = model.Card;
        var iconFill =
            card.Warning ? ColorToken.DangerWash
            : card.Hot ? ColorToken.WarnWash
            : ColorToken.AccentWash;
        var iconInk =
            card.Warning ? ColorToken.DangerText
            : card.Hot ? ColorToken.Warn
            : ColorToken.Accent;
        var icon = Ui.Card(Ui.Icon(card.Icon, 26, iconInk), iconFill, null, 12, new Thickness(0));
        icon.Width = 48;
        icon.Height = 48;
        var title = Ui.Text(card.Title, 18, bold: true, wrap: true);
        AutomationProperties.SetLiveSetting(title, AutomationLiveSetting.Polite);
        var head = Ui.Row(
            12,
            icon,
            Ui.Column(2, title, Ui.Text(card.Subtitle, 13, ink: ColorToken.Muted, wrap: true))
        );
        var button = Ui.Button(
            Ui.Text(
                card.Button,
                15,
                bold: true,
                ink: card.Hot ? ColorToken.OnAccent : ColorToken.Text
            ),
            card.Button,
            _viewModel.UpdateAction,
            card.Hot ? ColorToken.Accent : ColorToken.CardHi,
            card.Hot ? ColorToken.OnAccent : ColorToken.Text,
            height: 48
        );
        button.BorderThickness = new Thickness(0);
        button.IsEnabled = card.ButtonEnabled;
        var cardColumn = Ui.Column(14, head, card.ShowBar ? Progress(card.Percent) : null, button);
        var column = Ui.Column(
            10,
            Ui.Card(cardColumn, ColorToken.Card, null, 14, new Thickness(18))
        );
        for (var i = 0; i < model.Switches.Count; i++)
        {
            var index = i;
            var item = model.Switches[i];
            column.Children.Add(
                Spaced(
                    Ui.SwitchRow(
                        item.Icon,
                        item.Label,
                        item.Description,
                        item.On,
                        () => _viewModel.ToggleUpdateSwitch(index)
                    )
                )
            );
        }

        column.Children.Add(Spaced(Channel(model)));
        column.Children.Add(Spaced(Caption(model.WhatsNew)));
        foreach (var notes in model.Notes)
        {
            column.Children.Add(Spaced(Notes(notes)));
        }

        if (model.Rollback is { } rollback)
        {
            column.Children.Add(Spaced(Rollback(rollback)));
        }

        return column;
    }

    private static Border Progress(int percent)
    {
        var fill = new Border
        {
            HorizontalAlignment = HorizontalAlignment.Left,
            CornerRadius = new CornerRadius(4),
        };
        Ui.Ink(fill, BackgroundProperty, ColorToken.Accent);
        var track = new Border
        {
            Height = 8,
            CornerRadius = new CornerRadius(4),
            Child = fill,
        };
        Ui.Ink(track, BackgroundProperty, ColorToken.CardHi);
        track.SizeChanged += (_, e) =>
            fill.Width = e.NewSize.Width * Math.Clamp(percent, 0, 100) / 100.0;
        AutomationProperties.SetName(
            track,
            percent.ToString(System.Globalization.CultureInfo.CurrentCulture) + " %"
        );
        return track;
    }

    private Border Channel(UpdatesModel model)
    {
        var options = Ui.Row(4);
        foreach (var option in model.Channels)
        {
            var button = Ui.Choice(
                Ui.Text(
                    option.Label,
                    14,
                    bold: true,
                    ink: option.Selected ? ColorToken.OnAccent : ColorToken.Text
                ),
                model.Channel + ": " + option.Label,
                option.Selected,
                () => _viewModel.SetChannel(option.Channel),
                44,
                10,
                offFill: ColorToken.CardHi
            );
            if (option.Selected)
            {
                CcChrome.Paint(button, ColorToken.Accent, ColorToken.OnAccent, null);
            }
            else
            {
                CcChrome.Paint(button, ColorToken.CardHi, ColorToken.Text, null);
            }

            button.BorderThickness = new Thickness(0);
            options.Children.Add(button);
        }

        var icon = Ui.Icon("science", 22, ColorToken.Accent);
        var texts = Ui.Column(
            2,
            Ui.Text(model.Channel, 15, bold: true),
            Ui.Text(model.ChannelDescription, 13, ink: ColorToken.Muted, wrap: true)
        );
        var row = new DockPanel { LastChildFill = true };
        DockPanel.SetDock(icon, Dock.Left);
        row.Children.Add(icon);
        DockPanel.SetDock(options, Dock.Right);
        options.Margin = new Thickness(10, 0, 0, 0);
        row.Children.Add(options);
        texts.Margin = new Thickness(10, 0, 0, 0);
        row.Children.Add(texts);
        return Ui.Card(row, ColorToken.Card, null, 12, new Thickness(14, 12, 14, 12));
    }

    private static Border Notes(NotesModel notes)
    {
        var top = Ui.Row(
            8,
            Ui.Text(notes.Version, 14, ink: ColorToken.Accent, mono: true),
            notes.Date.Length == 0 ? null : Ui.Text(notes.Date, 12, ink: ColorToken.Muted),
            notes.NewBadge.Length == 0
                ? null
                : Ui.Card(
                    Ui.Text(notes.NewBadge, 11, bold: true, ink: ColorToken.Warn),
                    ColorToken.WarnWash,
                    null,
                    5,
                    new Thickness(6, 2, 6, 2)
                )
        );
        var column = Ui.Column(8, top);
        foreach (var item in notes.Items)
        {
            var check = Ui.Icon("check", 16, ColorToken.Muted);
            check.VerticalAlignment = VerticalAlignment.Top;
            check.Margin = new Thickness(0, 2, 0, 0);
            column.Children.Add(Ui.Row(8, check, Ui.Text(item, 14, wrap: true)));
        }

        return Ui.Card(column, null, ColorToken.Border, 12, new Thickness(16, 14, 16, 14));
    }

    private Border Rollback(RollbackModel rollback)
    {
        var button = Ui.Button(
            Ui.Text(
                rollback.Button,
                13,
                bold: true,
                ink: rollback.Armed ? ColorToken.OnWarn : ColorToken.Text
            ),
            rollback.Title,
            _viewModel.Rollback,
            rollback.Armed ? ColorToken.Warn : ColorToken.Card,
            rollback.Armed ? ColorToken.OnWarn : ColorToken.Text
        );
        button.BorderThickness = new Thickness(0);
        return Row("history", rollback.Title, rollback.Description, button, ColorToken.Border);
    }

    private StackPanel Backups(BackupsModel model)
    {
        var folder = Ui.Row(
            6,
            Ui.Icon("folder", 16, ColorToken.Muted),
            Ui.Text(model.Folder, 12, ink: ColorToken.Muted, mono: true),
            Ui.Text("· " + model.Format, 12, ink: ColorToken.Muted)
        );
        var column = Ui.Column(10, folder);
        if (model.ImportCard is { CanMerge: true } import)
        {
            column.Children.Add(Spaced(Import(import)));
        }

        var now = Ui.Button(
            Ui.IconLabel(
                "backup",
                model.BackupNow,
                20,
                14,
                ink: ColorToken.OnAccent,
                iconInk: ColorToken.OnAccent
            ),
            model.BackupNow,
            _viewModel.BackupNow,
            ColorToken.Accent,
            ColorToken.OnAccent,
            height: 48
        );
        now.BorderThickness = new Thickness(0);
        var export = Ui.Button(
            Ui.IconLabel("upload", model.Export, 18, 14, bold: false),
            model.Export,
            _viewModel.Export,
            null,
            stroke: ColorToken.Border,
            height: 48
        );
        var importButton = Ui.Button(
            Ui.IconLabel("download", model.Import, 18, 14, bold: false),
            model.Import,
            _viewModel.Import,
            model.ImportCard is { CanMerge: true } ? ColorToken.AccentWash : null,
            stroke: model.ImportCard is { CanMerge: true } ? ColorToken.Accent : ColorToken.Border,
            height: 48
        );
        foreach (var button in new[] { now, export, importButton })
        {
            button.IsEnabled = !model.Busy;
        }

        var actions = new Grid();
        actions.ColumnDefinitions.Add(
            new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }
        );
        actions.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        actions.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        export.Margin = new Thickness(8, 0, 0, 0);
        importButton.Margin = new Thickness(8, 0, 0, 0);
        Grid.SetColumn(export, 1);
        Grid.SetColumn(importButton, 2);
        actions.Children.Add(now);
        actions.Children.Add(export);
        actions.Children.Add(importButton);
        column.Children.Add(Spaced(actions));
        var auto = model.Auto;
        column.Children.Add(
            Spaced(
                Ui.SwitchRow(
                    auto.Icon,
                    auto.Label,
                    auto.Description,
                    auto.On,
                    _viewModel.ToggleAutoBackup
                )
            )
        );
        column.Children.Add(Spaced(Caption(model.History)));
        if (model.ImportCard is { CanMerge: false } review)
        {
            // LOG-008: the review of the backup being restored opens next to the history it belongs to, in view.
            var card = Import(review);
            card.Loaded += (_, _) => card.BringIntoView();
            column.Children.Add(Spaced(card));
        }

        if (model.Rows.IsEmpty)
        {
            column.Children.Add(
                Spaced(
                    Ui.Card(
                        Ui.Text(model.Empty, 14, ink: ColorToken.Muted),
                        null,
                        ColorToken.Border,
                        12,
                        new Thickness(14)
                    )
                )
            );
            return column;
        }

        var history = Ui.Column(0);
        for (var i = 0; i < model.Rows.Count; i++)
        {
            history.Children.Add(History(model.Rows[i], last: i == model.Rows.Count - 1));
        }

        column.Children.Add(
            Spaced(Ui.Card(history, null, ColorToken.Border, 12, new Thickness(0)))
        );
        return column;
    }

    private Border Import(ImportCardModel import)
    {
        var merge = Ui.Button(
            Ui.Column(
                2,
                Centered(Ui.Text(import.Merge, 14, bold: true, ink: ColorToken.OnAccent)),
                Centered(Ui.Text(import.MergeDescription, 11, ink: ColorToken.OnAccent))
            ),
            import.Merge,
            _viewModel.ImportMerge,
            ColorToken.Accent,
            ColorToken.OnAccent,
            height: 52
        );
        merge.BorderThickness = new Thickness(0);
        AutomationProperties.SetHelpText(merge, import.MergeDescription);
        var replace = Ui.Button(
            Ui.Column(
                2,
                Centered(
                    Ui.Text(
                        import.Replace,
                        14,
                        bold: true,
                        ink: import.ReplaceArmed ? ColorToken.OnWarn : ColorToken.Text
                    )
                ),
                Centered(
                    Ui.Text(
                        import.ReplaceDescription,
                        11,
                        ink: import.ReplaceArmed ? ColorToken.OnWarn : ColorToken.Muted
                    )
                )
            ),
            import.Replace,
            _viewModel.ImportReplace,
            import.ReplaceArmed ? ColorToken.Warn : null,
            import.ReplaceArmed ? ColorToken.OnWarn : ColorToken.Text,
            import.ReplaceArmed ? null : ColorToken.Border,
            height: 52
        );
        AutomationProperties.SetHelpText(replace, import.ReplaceDescription);
        AutomationProperties.SetAutomationId(merge, "system.import.merge");
        AutomationProperties.SetAutomationId(replace, "system.import.replace");
        var choices = import.CanMerge ? Ui.Columns(2, 6, [merge, replace]) : (UIElement)replace;
        var column = Ui.Column(
            8,
            Ui.Text(import.Title, 14, bold: true),
            import.Summary.Length == 0
                ? null
                : Ui.Text(import.Summary, 13, ink: ColorToken.Muted, wrap: true),
            import.Warning.Length == 0
                ? null
                : Ui.Row(
                    6,
                    Ui.Icon("warning", 16, ColorToken.Warn),
                    Ui.Text(import.Warning, 13, wrap: true)
                ),
            import.Review.IsEmpty ? null : Review(import),
            choices
        );
        return Ui.Card(column, ColorToken.Card, ColorToken.Accent, 12, new Thickness(12));
    }

    /// <summary>
    /// The Web, App and Macro shortcuts of the backup that the document does not have yet (LOG-008): one row of 48 or
    /// more each, unticked by default, with what it opens or runs.
    /// </summary>
    private StackPanel Review(ImportCardModel import)
    {
        var note = Ui.Row(
            6,
            Ui.Icon("shield", 16, ColorToken.Warn),
            Ui.Text(import.ReviewNote, 13, wrap: true)
        );
        var rows = Ui.Column(6, note);
        foreach (var row in import.Review)
        {
            var box = Ui.Icon(
                row.Checked ? "check_box" : "check_box_outline_blank",
                22,
                row.Checked ? ColorToken.Accent : ColorToken.Muted
            );
            var texts = Ui.Column(
                2,
                Ui.Text(row.Name, 14, bold: true, wrap: true),
                row.Detail.Length == 0
                    ? null
                    : Ui.Text(row.Detail, 12, ink: ColorToken.Muted, mono: true, wrap: true)
            );
            var line = new DockPanel { LastChildFill = true };
            DockPanel.SetDock(box, Dock.Left);
            line.Children.Add(box);
            var icon = Ui.Icon(row.Icon, 20, ColorToken.Accent);
            icon.Margin = new Thickness(10, 0, 0, 0);
            DockPanel.SetDock(icon, Dock.Left);
            line.Children.Add(icon);
            texts.Margin = new Thickness(10, 0, 0, 0);
            texts.VerticalAlignment = VerticalAlignment.Center;
            line.Children.Add(texts);
            var id = row.Id;
            var toggle = Ui.Choice(
                line,
                row.Name,
                row.Checked,
                () => _viewModel.ToggleReview(id),
                48,
                10,
                offFill: ColorToken.Field
            );
            toggle.Height = double.NaN;
            toggle.MinHeight = 48;
            toggle.Padding = new Thickness(12, 6, 12, 6);
            toggle.HorizontalContentAlignment = HorizontalAlignment.Stretch;
            AutomationProperties.SetHelpText(toggle, row.Detail);
            AutomationProperties.SetAutomationId(toggle, "system.review." + row.Id.Value);
            rows.Children.Add(Spaced(toggle, 6));
        }

        return rows;
    }

    private Border History(BackupRowModel row, bool last)
    {
        var button = Ui.Button(
            Ui.Text(
                row.Button,
                13,
                bold: true,
                ink: row.Armed ? ColorToken.OnWarn : ColorToken.Text
            ),
            row.Button + " " + row.Date,
            () => _viewModel.Restore(row.Id),
            row.Armed ? ColorToken.Warn : null,
            row.Armed ? ColorToken.OnWarn : ColorToken.Text,
            row.Armed ? null : ColorToken.Border,
            radius: 8
        );
        var icon = Ui.Icon("history", 20, ColorToken.Muted);
        var texts = Ui.Column(
            2,
            Ui.Text(row.Date, 14, bold: true),
            Ui.Text(row.Meta, 12, ink: ColorToken.Muted)
        );
        var line = new DockPanel { LastChildFill = true };
        DockPanel.SetDock(icon, Dock.Left);
        line.Children.Add(icon);
        DockPanel.SetDock(button, Dock.Right);
        button.Margin = new Thickness(12, 0, 0, 0);
        line.Children.Add(button);
        texts.Margin = new Thickness(12, 0, 0, 0);
        line.Children.Add(texts);
        var border = new Border
        {
            Child = line,
            Padding = new Thickness(14, 10, 14, 10),
            BorderThickness = new Thickness(0, 0, 0, last ? 0 : 1),
        };
        Ui.Ink(border, BorderBrushProperty, ColorToken.Border);
        return border;
    }

    private StackPanel Start(StartModel model)
    {
        var start = model.StartWithWindows;
        var column = Ui.Column(
            10,
            Ui.SwitchRow(
                start.Icon,
                start.Label,
                start.Description,
                start.On,
                _viewModel.ToggleStartWithWindows
            )
        );
        var admin = model.Admin;
        UIElement trailing;
        if (admin.CanReopen)
        {
            var button = Ui.Button(
                Ui.Text(admin.Button, 13, bold: true),
                admin.Title,
                _viewModel.ReopenAsAdmin,
                ColorToken.CardHi
            );
            button.BorderThickness = new Thickness(0);
            trailing = button;
        }
        else
        {
            trailing = Ui.Icon("check_circle", 22, ColorToken.Accent);
        }

        column.Children.Add(
            Spaced(Row("admin_panel_settings", admin.Title, admin.Description, trailing, null))
        );
        var status = Ui.Text(model.CrashStatus, 13, bold: true, ink: ColorToken.Accent);
        column.Children.Add(
            Spaced(Row("healing", model.Crash, model.CrashDescription, status, null))
        );
        column.Children.Add(Spaced(Uninstall(model.Uninstall)));
        return column;
    }

    /// <summary>
    /// «Desinstalar Clícalo» (NFR-010, P6): the data is kept unless the switch is on, and the button takes two taps
    /// (REG-04); armed, it is drawn in the warning color like the other destructive buttons.
    /// </summary>
    private Border Uninstall(UninstallModel model)
    {
        var button = Ui.Button(
            Ui.Text(
                model.Button,
                13,
                bold: true,
                ink: model.Armed ? ColorToken.OnWarn : ColorToken.Text
            ),
            model.Title,
            _viewModel.Uninstall,
            model.Armed ? ColorToken.Warn : ColorToken.CardHi,
            model.Armed ? ColorToken.OnWarn : ColorToken.Text
        );
        button.BorderThickness = new Thickness(0);
        button.IsEnabled = model.Available;
        AutomationProperties.SetAutomationId(button, "system.uninstall");
        AutomationProperties.SetHelpText(button, model.Description);
        var head = Row("delete", model.Title, model.Description, button, null);
        head.Padding = new Thickness(0);
        head.Background = null;
        var wipe = Ui.SwitchRow(
            model.DeleteData.Icon,
            model.DeleteData.Label,
            model.DeleteData.Description,
            model.DeleteData.On,
            _viewModel.ToggleDeleteData
        );
        wipe.IsEnabled = model.Available;
        AutomationProperties.SetAutomationId(wipe, "system.uninstall.deleteData");
        var card = Ui.Card(
            Ui.Column(10, head, wipe),
            null,
            ColorToken.Border,
            12,
            new Thickness(14)
        );
        AutomationProperties.SetName(card, model.Title);
        return card;
    }

    private static Border Row(
        string icon,
        string title,
        string description,
        UIElement trailing,
        ColorToken? stroke
    )
    {
        var symbol = Ui.Icon(icon, 22, ColorToken.Accent);
        var texts = Ui.Column(
            2,
            Ui.Text(title, 15, bold: true, wrap: true),
            Ui.Text(description, 13, ink: ColorToken.Muted, wrap: true)
        );
        var row = new DockPanel { LastChildFill = true, MinHeight = 44 };
        DockPanel.SetDock(symbol, Dock.Left);
        row.Children.Add(symbol);
        if (trailing is FrameworkElement element)
        {
            element.Margin = new Thickness(12, 0, 0, 0);
            element.VerticalAlignment = VerticalAlignment.Center;
        }

        DockPanel.SetDock(trailing, Dock.Right);
        row.Children.Add(trailing);
        texts.Margin = new Thickness(12, 0, 0, 0);
        texts.VerticalAlignment = VerticalAlignment.Center;
        row.Children.Add(texts);
        var card = Ui.Card(
            row,
            stroke is null ? ColorToken.Card : null,
            stroke,
            12,
            new Thickness(14)
        );
        AutomationProperties.SetName(card, title);
        return card;
    }

    private static TextBlock Caption(string text)
    {
        var caption = Ui.Text(
            text.ToUpper(System.Globalization.CultureInfo.CurrentCulture),
            13,
            bold: true,
            ink: ColorToken.Muted
        );
        caption.Margin = new Thickness(0, 10, 0, 0);
        return caption;
    }

    /// <summary>The gap of 10 of the content (children added after the column was built get no gap of their own).</summary>
    private static T Spaced<T>(T element, double gap = 10)
        where T : FrameworkElement
    {
        var margin = element.Margin;
        element.Margin = new Thickness(margin.Left, margin.Top + gap, margin.Right, margin.Bottom);
        return element;
    }

    private static TextBlock Centered(TextBlock text)
    {
        text.HorizontalAlignment = HorizontalAlignment.Center;
        return text;
    }
}
