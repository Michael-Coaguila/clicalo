using System.ComponentModel;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Clicalo.Domain.Primitives;
using Clicalo.Presentation.ControlCenter.Editor;
using Clicalo.UI.Wpf.Theming.Generated;
using Clicalo.UI.Wpf.Workspace.Internal;

namespace Clicalo.UI.Wpf.Workspace;

/// <summary>
/// State B of the editor column of «Atajos» (docs/05 §1 B, items 1 to 10): it draws
/// <see cref="ShortcutEditorViewModel.Model"/> region by region, rebuilding only the regions whose model changed, and
/// keeps its text fields alive so typing, dictation and the touch keyboard are never interrupted.
/// </summary>
public sealed class ShortcutEditorView : StackPanel
{
    private const double Gap = 16;

    private readonly ShortcutEditorViewModel _viewModel;
    private readonly ContentControl _duplicate = Region();
    private readonly ContentControl _identity = Region();
    private readonly ContentControl _picker = Region();
    private readonly ContentControl _kinds = Region();
    private readonly ContentControl _combo = Region();
    private readonly ContentControl _fields = Region();
    private readonly ContentControl _pin = Region();
    private readonly ContentControl _more = Region();
    private readonly ContentControl _test = Region();
    private readonly ContentControl _footer = Region();
    private readonly TextField _name;
    private readonly TextField _iconSearch;
    private readonly TextField _text;
    private readonly TextField _target;
    private readonly Dictionary<int, TextField> _stepTexts = [];
    private EditorModel? _shown;
    private string _shownKey = string.Empty;

    /// <summary>Creates the view of <paramref name="viewModel"/>.</summary>
    /// <param name="viewModel">The editor.</param>
    public ShortcutEditorView(ShortcutEditorViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        _viewModel = viewModel;
        _name = new TextField(string.Empty, string.Empty);
        _name.Changed += (_, _) => _viewModel.Rename(_name.Text);
        _iconSearch = new TextField(string.Empty, string.Empty);
        _iconSearch.Changed += (_, _) => _viewModel.SearchIcons(_iconSearch.Text);
        _text = new TextField(string.Empty, string.Empty, multiline: true);
        _text.Changed += (_, _) => _viewModel.SetText(_text.Text);
        _target = new TextField(string.Empty, string.Empty, mono: true);
        _target.Changed += (_, _) => _viewModel.SetTarget(_target.Text);
        foreach (
            var region in new[]
            {
                _duplicate,
                _identity,
                _picker,
                _kinds,
                _combo,
                _fields,
                _pin,
                _more,
                _test,
                _footer,
            }
        )
        {
            Children.Add(region);
        }

        viewModel.PropertyChanged += OnChanged;
        Render();
    }

    /// <summary>Stops following the view model.</summary>
    public void Detach() => _viewModel.PropertyChanged -= OnChanged;

    private static ContentControl Region() =>
        new() { Focusable = false, Margin = new Thickness(0, 0, 0, Gap) };

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

    private static void Show(ContentControl region, UIElement? content)
    {
        region.Content = content;
        region.Visibility = content is null ? Visibility.Collapsed : Visibility.Visible;
    }

    private void OnChanged(object? sender, PropertyChangedEventArgs e) => Render();

    private void Render()
    {
        var model = _viewModel.Model;
        if (model is null)
        {
            _shown = null;
            return;
        }

        var before = _shown;
        var keyChanged = !string.Equals(
            _shownKey,
            _viewModel.ShortcutKey,
            StringComparison.Ordinal
        );
        _shown = model;
        _shownKey = _viewModel.ShortcutKey;
        if (keyChanged)
        {
            before = null;
            _stepTexts.Clear();
        }

        if (before?.Duplicate != model.Duplicate)
        {
            Show(_duplicate, model.Duplicate is { } duplicate ? DuplicateCard(duplicate) : null);
        }

        if (before?.Identity != model.Identity)
        {
            Show(_identity, Identity(model.Identity, keyChanged));
        }

        if (before?.Picker != model.Picker)
        {
            Show(_picker, model.Picker is { } picker ? Picker(picker) : null);
        }

        if (before?.Kinds != model.Kinds)
        {
            Show(_kinds, Kinds(model.Kinds));
        }

        if (before?.Combo != model.Combo)
        {
            Show(_combo, model.Combo is { } combo ? ComboView.Build(combo, _viewModel) : null);
        }

        if (
            before is null
            || before.Text != model.Text
            || before.Mouse != model.Mouse
            || before.Steps != model.Steps
            || before.Target != model.Target
        )
        {
            Show(_fields, Fields(model, keyChanged));
        }

        if (before?.Pin != model.Pin)
        {
            Show(
                _pin,
                Ui.SwitchRow(
                    "push_pin",
                    model.Pin.Title,
                    model.Pin.Description,
                    model.Pin.On,
                    _viewModel.TogglePin
                )
            );
        }

        if (before?.More != model.More)
        {
            Show(_more, More(model.More));
        }

        if (before?.Test != model.Test)
        {
            Show(_test, model.Test is { } test ? Test(test) : null);
        }

        if (before?.Footer != model.Footer)
        {
            Show(_footer, Footer(model.Footer));
        }

        if (keyChanged && model.Identity.Name.Length == 0)
        {
            _ = Dispatcher.BeginInvoke(DispatcherPriority.Input, () => Keyboard.Focus(_name.Box));
        }
    }

    private Border DuplicateCard(DuplicateCardModel model)
    {
        var headContent = new DockPanel { LastChildFill = true };
        headContent.Children.Add(Docked(Ui.Icon("warning", 20, ColorToken.Warn), Dock.Left));
        headContent.Children.Add(
            Docked(
                Ui.Icon(model.Expanded ? "expand_less" : "expand_more", 20, ColorToken.Muted),
                Dock.Right
            )
        );
        headContent.Children.Add(Ui.Text(model.Head, 13, bold: true, wrap: true));
        var head = Ui.Button(
            headContent,
            model.Head,
            _viewModel.ToggleDuplicates,
            height: double.NaN,
            expanded: model.Expanded
        );
        head.MinHeight = 44;
        head.BorderThickness = new Thickness(0);
        head.HorizontalContentAlignment = HorizontalAlignment.Stretch;
        head.Padding = new Thickness(8, 0, 0, 0);
        var previous = IconOnly("chevron_left", model.PrevName, () => _viewModel.ShowRepeated(-1));
        var next = IconOnly("chevron_right", model.NextName, () => _viewModel.ShowRepeated(1));
        var bar = new DockPanel { LastChildFill = true };
        DockPanel.SetDock(next, Dock.Right);
        DockPanel.SetDock(previous, Dock.Right);
        bar.Children.Add(next);
        bar.Children.Add(previous);
        bar.Children.Add(head);
        AutomationProperties.SetLiveSetting(bar, AutomationLiveSetting.Polite);
        var body = Ui.Column(8, bar);
        if (model.Expanded)
        {
            var rows = Ui.Column(4);
            foreach (var row in model.Rows)
            {
                var open = Ui.Button(
                    Ui.Row(
                        10,
                        Ui.CategoryIcon(row.Icon, 20, row.Category),
                        Ui.Text(row.Name, 14, bold: true),
                        Ui.Text("· " + row.Where, 14, ink: ColorToken.Muted),
                        row.Editing
                            ? Ui.Text(model.EditingText, 11, bold: true, ink: ColorToken.WarnText)
                            : null
                    ),
                    row.Name + " · " + row.Where,
                    () => _viewModel.OpenAppearance(row.Id),
                    height: double.NaN
                );
                open.MinHeight = 44;
                open.BorderThickness = new Thickness(0);
                open.HorizontalContentAlignment = HorizontalAlignment.Left;
                var delete = Ui.Button(
                    Ui.IconLabel(
                        "delete",
                        row.Armed ? model.ConfirmText : string.Empty,
                        18,
                        12,
                        iconInk: row.Armed ? ColorToken.OnDanger : ColorToken.DangerText,
                        ink: ColorToken.OnDanger
                    ),
                    row.DeleteName,
                    () => _viewModel.DeleteAppearance(row.Id),
                    row.Armed ? ColorToken.Danger : null
                );
                delete.Padding = new Thickness(8, 0, 8, 0);
                var line = new DockPanel { LastChildFill = true };
                DockPanel.SetDock(delete, Dock.Right);
                line.Children.Add(delete);
                line.Children.Add(open);
                rows.Children.Add(
                    Ui.Card(
                        line,
                        row.Editing ? ColorToken.Card : null,
                        row.Editing ? ColorToken.Warn : ColorToken.Border,
                        10,
                        new Thickness(2),
                        row.Editing ? 2 : 1
                    )
                );
            }

            body.Children.Add(rows);
            body.Children.Add(Ui.Text(model.Advice, 13, ink: ColorToken.Muted, wrap: true));
            if (model.MoveText is { } move)
            {
                body.Children.Add(
                    Ui.Button(
                        Ui.IconLabel(
                            "push_pin",
                            move,
                            ink: model.MoveArmed ? ColorToken.OnDanger : ColorToken.OnWarn,
                            iconInk: model.MoveArmed ? ColorToken.OnDanger : ColorToken.OnWarn
                        ),
                        move,
                        _viewModel.KeepOnlyInAlwaysVisible,
                        model.MoveArmed ? ColorToken.Danger : ColorToken.Warn
                    )
                );
            }

            body.Children.Add(
                Ui.Button(
                    Ui.IconLabel("thumb_up", model.FineText),
                    model.FineText,
                    _viewModel.AcceptRepeated,
                    ColorToken.Card,
                    stroke: ColorToken.Border
                )
            );
            body.Children.Add(
                Ui.Button(
                    Ui.IconLabel("swap_horiz", model.UseOtherText),
                    model.UseOtherText,
                    _viewModel.UseOtherCombination,
                    ColorToken.Card,
                    stroke: ColorToken.Border
                )
            );
        }

        return Ui.Card(
            body,
            ColorToken.WarnWash,
            ColorToken.Warn,
            12,
            new Thickness(model.Expanded ? 8 : 4, 4, 4, model.Expanded ? 12 : 4)
        );
    }

    private DockPanel Identity(IdentityModel model, bool keyChanged)
    {
        var tileContent = new Grid();
        var center = Ui.Column(
            4,
            Ui.CategoryIcon(model.Icon, 30, model.Category),
            Ui.Text(model.TileName, 11, bold: true)
        );
        center.HorizontalAlignment = HorizontalAlignment.Center;
        center.VerticalAlignment = VerticalAlignment.Center;
        tileContent.Children.Add(center);
        var pencil = Ui.Card(
            Ui.Icon("edit", 15, ColorToken.OnAccent),
            ColorToken.Accent,
            null,
            13,
            new Thickness(0)
        );
        pencil.Width = 26;
        pencil.Height = 26;
        pencil.HorizontalAlignment = HorizontalAlignment.Right;
        pencil.VerticalAlignment = VerticalAlignment.Bottom;
        pencil.Margin = new Thickness(0, 0, -10, -10);
        pencil.IsHitTestVisible = false;
        tileContent.Children.Add(pencil);
        var tile = Ui.Choice(
            tileContent,
            model.ChangeIconName,
            model.PickerOpen,
            _viewModel.TogglePicker,
            84,
            14,
            role: CcToggleRole.Expander
        );
        tile.Width = 84;
        tile.Padding = new Thickness(4);
        tile.HorizontalContentAlignment = HorizontalAlignment.Stretch;
        tile.VerticalContentAlignment = VerticalAlignment.Stretch;
        tile.VerticalAlignment = VerticalAlignment.Top;

        Detach(_name);
        AutomationProperties.SetName(_name.Box, model.NameLabel);
        _name.Show(model.Name, force: keyChanged);
        var dictate = Ui.Button(
            Ui.Icon("mic", 22, ColorToken.Accent),
            model.DictateName,
            () =>
            {
                _ = Keyboard.Focus(_name.Box);
                _viewModel.DictateName();
            },
            ColorToken.AccentWash
        );
        dictate.Width = 44;
        dictate.Padding = new Thickness(0);
        dictate.Margin = new Thickness(6, 0, 0, 0);
        var field = new DockPanel { LastChildFill = true };
        DockPanel.SetDock(dictate, Dock.Right);
        field.Children.Add(dictate);
        field.Children.Add(_name);
        SetPlaceholder(_name, model.Placeholder);
        var hint = Ui.Row(
            4,
            Ui.Icon(model.HintIcon, 14, ColorToken.Accent),
            Ui.Text(model.Hint, 12, ink: ColorToken.Muted, wrap: true)
        );
        var right = Ui.Column(6, Ui.Caption(model.NameLabel), field, hint);
        var row = new DockPanel { LastChildFill = true };
        DockPanel.SetDock(tile, Dock.Left);
        tile.Margin = new Thickness(0, 0, 12, 0);
        row.Children.Add(tile);
        row.Children.Add(right);
        return row;
    }

    private static void SetPlaceholder(TextField field, string placeholder)
    {
        if (field.Child is Grid grid && grid.Children[0] is TextBlock block)
        {
            block.Text = placeholder;
        }
    }

    private Border Picker(IconPickerModel model)
    {
        var body = Ui.Column(8);
        if (!model.Suggested.IsEmpty)
        {
            body.Children.Add(
                Ui.Wrap(
                    4,
                    model.Suggested.Select(option =>
                        (UIElement)IconChoice(option.Icon, option.Selected, 44, 24)
                    )
                )
            );
        }

        Detach(_iconSearch);
        AutomationProperties.SetName(_iconSearch.Box, model.SearchPlaceholder);
        SetPlaceholder(_iconSearch, model.SearchPlaceholder);
        var search = new DockPanel { LastChildFill = true };
        var glass = Ui.Icon("search", 20, ColorToken.Muted);
        glass.Margin = new Thickness(0, 0, 6, 0);
        DockPanel.SetDock(glass, Dock.Left);
        search.Children.Add(glass);
        var dictateIcon = Ui.Button(
            Ui.Icon("mic", 22, ColorToken.Accent),
            model.DictateName,
            () => Dictate(_iconSearch),
            ColorToken.AccentWash
        );
        dictateIcon.Width = 44;
        dictateIcon.Padding = new Thickness(0);
        dictateIcon.Margin = new Thickness(6, 0, 0, 0);
        DockPanel.SetDock(dictateIcon, Dock.Right);
        search.Children.Add(dictateIcon);
        search.Children.Add(_iconSearch);
        body.Children.Add(search);
        var grid = new AutoFillGrid
        {
            MinItemWidth = 44,
            ItemHeight = 44,
            Gap = 4,
        };
        foreach (var option in model.Icons)
        {
            grid.Children.Add(IconChoice(option.Icon, option.Selected, 44, 20));
        }

        body.Children.Add(new TouchPanScrollViewer { Content = grid, MaxHeight = 180 });
        return Ui.Card(body, ColorToken.Card, ColorToken.Border, 12, new Thickness(10));
    }

    private CcToggle IconChoice(string icon, bool selected, double size, double glyph)
    {
        var button = Ui.Choice(
            Ui.Icon(icon, glyph),
            icon.Replace('_', ' '),
            selected,
            () => _viewModel.PickIcon(icon),
            size,
            8,
            role: CcToggleRole.Option
        );
        button.Width = size;
        button.Padding = new Thickness(0);
        return button;
    }

    private StackPanel Kinds(KindsModel model)
    {
        var options = model
            .Options.Select(option =>
            {
                var button = Ui.Choice(
                    Ui.Column(3, Ui.Icon(option.Icon, 20), Ui.Text(option.Label, 12, bold: true)),
                    option.Label,
                    option.Selected,
                    () => _viewModel.SetKind(option.Kind),
                    56,
                    10,
                    role: CcToggleRole.Option
                );
                button.Padding = new Thickness(2);
                return (UIElement)button;
            })
            .ToList();
        return Ui.Column(
            6,
            Ui.Caption(model.Title),
            Ui.Columns(4, 6, options),
            Ui.Text(model.Description, 13, ink: ColorToken.Muted, wrap: true)
        );
    }

    private StackPanel? Fields(EditorModel model, bool keyChanged)
    {
        if (model.Text is { } text)
        {
            Detach(_text);
            AutomationProperties.SetName(_text.Box, text.Label);
            _text.Show(_viewModel.RevealText(), force: keyChanged);
            var dictate = Ui.Button(
                Ui.Icon("mic", 22, ColorToken.Accent),
                text.DictateName,
                () => Dictate(_text),
                ColorToken.AccentWash
            );
            dictate.Width = 44;
            dictate.Padding = new Thickness(0);
            dictate.VerticalAlignment = VerticalAlignment.Top;
            dictate.Margin = new Thickness(6, 0, 0, 0);
            var field = new DockPanel { LastChildFill = true };
            DockPanel.SetDock(dictate, Dock.Right);
            field.Children.Add(dictate);
            field.Children.Add(_text);
            return Ui.Column(
                6,
                Ui.Caption(text.Label),
                field,
                Ui.Text(text.Hint, 11, ink: ColorToken.Muted, wrap: true),
                Ui.Row(
                    6,
                    Ui.Icon("lock", 16, ColorToken.Muted),
                    Ui.Text(text.Encrypted, 12, ink: ColorToken.Muted, wrap: true)
                )
            );
        }

        if (model.Mouse is { } mouse)
        {
            var options = mouse
                .Options.Select(option =>
                {
                    var button = Ui.Choice(
                        Ui.IconLabel(option.Icon, option.Label, 20, 14),
                        option.AccessibleName,
                        option.Selected,
                        () => _viewModel.SetMouse(option.Op),
                        48,
                        10,
                        role: CcToggleRole.Option
                    );
                    button.HorizontalContentAlignment = HorizontalAlignment.Left;
                    return (UIElement)button;
                })
                .ToList();
            var column = Ui.Column(6, Ui.Caption(mouse.Label), Ui.Columns(2, 6, options));
            if (!mouse.Speeds.IsEmpty)
            {
                column.Children.Add(Ui.Caption(mouse.SpeedLabel));
                column.Children.Add(
                    Ui.Columns(
                        mouse.Speeds.Count,
                        2,
                        [
                            .. mouse.Speeds.Select(speed =>
                                (UIElement)Segment(
                                    speed.Label,
                                    speed.Selected,
                                    () => _viewModel.SetSpeed(speed.Speed)
                                )
                            ),
                        ]
                    )
                );
            }

            return column;
        }

        if (model.Steps is { } steps)
        {
            return Steps(steps);
        }

        if (model.Target is { } target)
        {
            Detach(_target);
            AutomationProperties.SetName(_target.Box, target.Label);
            _target.Show(target.Value, force: keyChanged);
            _target.Warn(target.Invalid);
            var dictate = Ui.Button(
                Ui.Icon("mic", 22, ColorToken.Accent),
                target.DictateName,
                () => Dictate(_target),
                ColorToken.AccentWash
            );
            dictate.Width = 44;
            dictate.Padding = new Thickness(0);
            dictate.Margin = new Thickness(6, 0, 0, 0);
            var field = new DockPanel { LastChildFill = true };
            DockPanel.SetDock(dictate, Dock.Right);
            field.Children.Add(dictate);
            field.Children.Add(_target);
            var column = Ui.Column(6, Ui.Caption(target.Label), field);
            if (target.Invalid)
            {
                column.Children.Add(
                    Ui.Text(target.InvalidText, 12, ink: ColorToken.WarnText, wrap: true)
                );
            }

            if (target.PickLabel.Length > 0 && !target.Picks.IsEmpty)
            {
                column.Children.Add(Ui.Caption(target.PickLabel));
                column.Children.Add(Ui.Text(target.OpenLabel, 12, ink: ColorToken.Muted));
                column.Children.Add(
                    Ui.Wrap(
                        6,
                        target.Picks.Select(pick =>
                            (UIElement)
                                Ui.Choice(
                                    Ui.IconLabel("apps", pick.Name),
                                    pick.Name,
                                    false,
                                    () => _viewModel.PickProgram(pick.Process),
                                    44,
                                    22
                                )
                        )
                    )
                );
            }

            if (!target.Programs.IsEmpty)
            {
                if (target.Picks.IsEmpty)
                {
                    column.Children.Add(Ui.Caption(target.PickLabel));
                }

                column.Children.Add(Ui.Text(target.ProgramsLabel, 12, ink: ColorToken.Muted));
                var programs = Ui.Wrap(
                    6,
                    target.Programs.Select(program =>
                        (UIElement)
                            Ui.Choice(
                                Ui.Text(program.Name, 13, bold: true),
                                program.Name,
                                string.Equals(
                                    program.Target,
                                    target.Value,
                                    StringComparison.Ordinal
                                ),
                                () => _viewModel.PickInstalled(program.Target),
                                44,
                                22,
                                role: CcToggleRole.Option
                            )
                    )
                );
                column.Children.Add(
                    new TouchPanScrollViewer { Content = programs, MaxHeight = 196 }
                );
            }

            return column;
        }

        return null;
    }

    private StackPanel Steps(StepsModel model)
    {
        var column = Ui.Column(6, Ui.Caption(model.Label));
        foreach (var step in model.Steps)
        {
            var number = Ui.Card(
                Ui.Text(step.Number, 12, bold: true, ink: ColorToken.Accent),
                ColorToken.AccentWash,
                null,
                12,
                new Thickness(0)
            );
            number.Width = 24;
            number.Height = 24;
            number.Child.SetValue(HorizontalAlignmentProperty, HorizontalAlignment.Center);
            var edit = Ui.Button(
                Ui.Row(
                    8,
                    Ui.Icon(step.Icon, 18, ColorToken.Muted),
                    Ui.Text(step.Text, 14),
                    Ui.Icon("edit", 16, ColorToken.Muted)
                ),
                model.EditName + " " + step.Text,
                () => _viewModel.ToggleStep(step.Index),
                height: double.NaN,
                expanded: step.Editor != StepEditorKind.None
            );
            edit.MinHeight = 44;
            edit.BorderThickness = new Thickness(0);
            edit.HorizontalContentAlignment = HorizontalAlignment.Left;
            var up = IconOnly(
                "arrow_upward",
                model.UpName,
                () => _viewModel.MoveStep(step.Index, -1)
            );
            up.IsEnabled = step.CanUp;
            var down = IconOnly(
                "arrow_downward",
                model.DownName,
                () => _viewModel.MoveStep(step.Index, 1)
            );
            down.IsEnabled = step.CanDown;
            var delete = Ui.Button(
                step.Armed
                    ? Ui.Text(model.ConfirmText, 12, bold: true, ink: ColorToken.OnDanger)
                    : Ui.Icon("close", 20, ColorToken.DangerText),
                model.DeleteName,
                () => _viewModel.DeleteStep(step.Index),
                step.Armed ? ColorToken.Danger : null
            );
            delete.Padding = new Thickness(step.Armed ? 8 : 0, 0, step.Armed ? 8 : 0, 0);
            delete.MinWidth = 44;
            var line = new DockPanel { LastChildFill = true, Margin = new Thickness(10, 2, 2, 2) };
            DockPanel.SetDock(number, Dock.Left);
            number.Margin = new Thickness(0, 0, 6, 0);
            line.Children.Add(number);
            foreach (var button in new UIElement[] { delete, down, up })
            {
                DockPanel.SetDock(button, Dock.Right);
                line.Children.Add(button);
            }

            line.Children.Add(edit);
            var body = Ui.Column(0, line);
            switch (step.Editor)
            {
                case StepEditorKind.Wait:
                    var less = Ui.Button(
                        Ui.Icon("remove", 22),
                        model.LessName,
                        () => _viewModel.NudgeWait(step.Index, -1),
                        ColorToken.CardHi
                    );
                    var more = Ui.Button(
                        Ui.Icon("add", 22),
                        model.MoreName,
                        () => _viewModel.NudgeWait(step.Index, 1),
                        ColorToken.CardHi
                    );
                    less.Width = 44;
                    more.Width = 44;
                    less.Padding = new Thickness(0);
                    more.Padding = new Thickness(0);
                    var value = Ui.Text(step.Wait, 15, mono: true);
                    value.HorizontalAlignment = HorizontalAlignment.Center;
                    var wait = new DockPanel
                    {
                        LastChildFill = true,
                        Margin = new Thickness(40, 0, 10, 10),
                    };
                    DockPanel.SetDock(less, Dock.Left);
                    DockPanel.SetDock(more, Dock.Right);
                    wait.Children.Add(less);
                    wait.Children.Add(more);
                    wait.Children.Add(value);
                    body.Children.Add(wait);
                    break;
                case StepEditorKind.Text:
                    var field = StepText(step.Index, model.Label);
                    var dictate = Ui.Button(
                        Ui.Icon("mic", 22, ColorToken.Accent),
                        model.DictateName,
                        () => Dictate(field),
                        ColorToken.AccentWash
                    );
                    dictate.Width = 44;
                    dictate.Padding = new Thickness(0);
                    dictate.Margin = new Thickness(6, 0, 0, 0);
                    dictate.VerticalAlignment = VerticalAlignment.Top;
                    var text = new DockPanel
                    {
                        LastChildFill = true,
                        Margin = new Thickness(40, 0, 10, 10),
                    };
                    DockPanel.SetDock(dictate, Dock.Right);
                    text.Children.Add(dictate);
                    text.Children.Add(field);
                    body.Children.Add(text);
                    break;
                case StepEditorKind.Mouse:
                    var chips = Ui.Wrap(
                        4,
                        step.Mouse.Select(option =>
                            (UIElement)
                                Ui.Choice(
                                    Ui.Text(option.Label, 12, bold: true),
                                    option.AccessibleName,
                                    option.Selected,
                                    () => _viewModel.SetStepMouse(step.Index, option.Op),
                                    44,
                                    8,
                                    role: CcToggleRole.Option
                                )
                        )
                    );
                    chips.Margin = new Thickness(40, 0, 10 - 4, 10 - 4);
                    body.Children.Add(chips);
                    break;
            }

            column.Children.Add(
                Ui.Card(
                    body,
                    ColorToken.Card,
                    step.Editor == StepEditorKind.None ? ColorToken.Border : ColorToken.Accent,
                    10,
                    new Thickness(0),
                    step.Editor == StepEditorKind.None ? 1 : 2
                )
            );
        }

        var adds = model
            .Add.Select(add =>
            {
                var button = Dashed(
                    Ui.Column(2, Ui.Icon(add.Icon, 18), Ui.Text("+ " + add.Label, 12, bold: true)),
                    add.Label,
                    () => _viewModel.AddStep(add.Kind),
                    52
                );
                return (UIElement)button;
            })
            .ToList();
        column.Children.Add(Ui.Columns(4, 6, adds));
        return column;
    }

    private TextField StepText(int index, string name)
    {
        if (!_stepTexts.TryGetValue(index, out var field))
        {
            field = new TextField(name, string.Empty, multiline: true);
            field.Changed += (_, _) => _viewModel.SetStepText(index, field.Text);
            _stepTexts[index] = field;
        }

        Detach(field);
        field.Show(_viewModel.RevealStepText(index));
        return field;
    }

    private static CcButton Dashed(UIElement content, string name, Action click, double height)
    {
        var frame = new System.Windows.Shapes.Rectangle
        {
            RadiusX = 10,
            RadiusY = 10,
            StrokeThickness = 2,
            StrokeDashArray = [4, 3],
            IsHitTestVisible = false,
        };
        Ui.Ink(frame, System.Windows.Shapes.Shape.StrokeProperty, ColorToken.Line);
        var layers = new Grid();
        layers.Children.Add(frame);
        layers.Children.Add(content);
        if (content is FrameworkElement element)
        {
            element.HorizontalAlignment = HorizontalAlignment.Center;
            element.VerticalAlignment = VerticalAlignment.Center;
        }

        var button = Ui.Button(layers, name, click, height: height);
        button.Padding = new Thickness(0);
        button.HorizontalContentAlignment = HorizontalAlignment.Stretch;
        button.VerticalContentAlignment = VerticalAlignment.Stretch;
        button.BorderThickness = new Thickness(0);
        return button;
    }

    private Border More(MoreModel model)
    {
        var head = Ui.Button(
            new DockPanel
            {
                LastChildFill = true,
                Children =
                {
                    Docked(
                        Ui.Icon(
                            model.Expanded ? "expand_less" : "expand_more",
                            20,
                            ColorToken.Muted
                        ),
                        Dock.Right
                    ),
                    Docked(Ui.Text(model.Position, 12, ink: ColorToken.Muted), Dock.Right),
                    Docked(Ui.Icon("tune", 20, ColorToken.Muted), Dock.Left),
                    Ui.Text(model.Title, 14, bold: true),
                },
            },
            model.Title,
            _viewModel.ToggleMore,
            height: 48,
            expanded: model.Expanded
        );
        head.HorizontalContentAlignment = HorizontalAlignment.Stretch;
        head.BorderThickness = new Thickness(0);
        var body = Ui.Column(10, head);
        if (model.Expanded)
        {
            var inner = Ui.Column(10, Ui.Caption(model.PositionLabel));
            inner.Children.Add(
                Ui.Columns(
                    4,
                    6,
                    [
                        .. model.Positions.Select(option =>
                        {
                            var button = Ui.Button(
                                Ui.Column(
                                    2,
                                    Ui.Icon(option.Icon, 20),
                                    Ui.Text(option.Label, 12, bold: true)
                                ),
                                option.Label,
                                () => _viewModel.Move(option.Move),
                                ColorToken.Card,
                                stroke: ColorToken.Border,
                                height: 52
                            );
                            button.IsEnabled = option.Enabled;
                            button.Padding = new Thickness(2);
                            return (UIElement)button;
                        }),
                    ]
                )
            );
            if (model.HoldLabel is { } hold)
            {
                inner.Children.Add(Ui.Caption(hold));
                inner.Children.Add(Segments(model.Holds, _viewModel.SetHold));
                inner.Children.Add(Ui.Text(model.HoldText, 12, ink: ColorToken.Muted, wrap: true));
                inner.Children.Add(
                    Ui.Text(model.HoldSwitchText, 12, ink: ColorToken.Muted, wrap: true)
                );
            }

            if (model.MethodLabel is { } method)
            {
                inner.Children.Add(Ui.Caption(method));
                inner.Children.Add(Segments(model.Methods, _viewModel.SetMethod));
                inner.Children.Add(
                    Ui.Row(
                        6,
                        Ui.Icon("lock", 16, ColorToken.Muted),
                        Ui.Text(model.EncryptedText, 12, ink: ColorToken.Muted, wrap: true)
                    )
                );
            }

            inner.Children.Add(
                Ui.SwitchRow(
                    "verified",
                    model.ConfirmTitle,
                    model.ConfirmText,
                    model.Confirm,
                    _viewModel.ToggleConfirm
                )
            );
            inner.Children.Add(
                Ui.SwitchRow(
                    "keep",
                    model.FrequentsTitle,
                    string.Empty,
                    model.Frequents,
                    _viewModel.ToggleFrequents
                )
            );
            var badge = Ui.Card(
                Ui.Text(model.VoiceNumber, 14, bold: true, ink: ColorToken.OnWarn),
                ColorToken.Warn,
                null,
                7,
                new Thickness(6, 2, 6, 2)
            );
            badge.MinWidth = 28;
            inner.Children.Add(
                Ui.Card(
                    Ui.Row(10, badge, Ui.Text(model.VoiceLine, 13, wrap: true)),
                    ColorToken.Card,
                    null,
                    10,
                    new Thickness(12, 10, 12, 10)
                )
            );
            var how = Ui.Button(
                new DockPanel
                {
                    LastChildFill = true,
                    Children =
                    {
                        Docked(Ui.Icon("record_voice_over", 20, ColorToken.Accent), Dock.Left),
                        Docked(
                            Ui.Icon(
                                model.VoiceHowOpen ? "expand_less" : "expand_more",
                                20,
                                ColorToken.Muted
                            ),
                            Dock.Right
                        ),
                        Ui.Text(model.VoiceHowTitle, 13, bold: true),
                    },
                },
                model.VoiceHowTitle,
                _viewModel.ToggleVoiceHow,
                stroke: ColorToken.Border,
                expanded: model.VoiceHowOpen
            );
            how.HorizontalContentAlignment = HorizontalAlignment.Stretch;
            inner.Children.Add(how);
            if (model.VoiceHowOpen)
            {
                var steps = Ui.Column(6);
                for (var i = 0; i < model.VoiceSteps.Count; i++)
                {
                    var n = Ui.Card(
                        Ui.Text(
                            (i + 1).ToString(System.Globalization.CultureInfo.InvariantCulture),
                            12,
                            bold: true,
                            ink: ColorToken.OnAccent
                        ),
                        ColorToken.Accent,
                        null,
                        11,
                        new Thickness(7, 2, 7, 2)
                    );
                    n.VerticalAlignment = VerticalAlignment.Top;
                    steps.Children.Add(Ui.Row(8, n, Ui.Text(model.VoiceSteps[i], 13, wrap: true)));
                }

                inner.Children.Add(
                    Ui.Card(steps, ColorToken.Card, null, 10, new Thickness(12, 10, 12, 10))
                );
            }

            inner.Margin = new Thickness(12, 0, 12, 12);
            body.Children.Add(inner);
        }

        return Ui.Card(body, null, ColorToken.Border, 12, new Thickness(0));
    }

    private static UIElement Docked(UIElement element, Dock dock)
    {
        DockPanel.SetDock(element, dock);
        if (element is FrameworkElement framework)
        {
            framework.Margin =
                dock == Dock.Left ? new Thickness(0, 0, 10, 0) : new Thickness(10, 0, 0, 0);
        }

        return element;
    }

    private static Border Segments(ValueList<ChoiceOption> options, Action<int> choose) =>
        Ui.Card(
            Ui.Columns(
                options.Count,
                2,
                [
                    .. options.Select(option =>
                        (UIElement)Segment(option.Label, option.Selected, () => choose(option.Id))
                    ),
                ]
            ),
            ColorToken.Card,
            null,
            10,
            new Thickness(3)
        );

    private static CcToggle Segment(string label, bool selected, Action click)
    {
        var text = Ui.Text(
            label,
            12,
            bold: true,
            ink: selected ? ColorToken.OnAccent : ColorToken.Text,
            wrap: true
        );
        text.TextAlignment = TextAlignment.Center;
        var button = Ui.Choice(
            text,
            label,
            selected,
            click,
            44,
            8,
            offFill: null,
            role: CcToggleRole.Option
        );
        if (selected)
        {
            CcChrome.Paint(button, ColorToken.Accent, ColorToken.OnAccent, null);
        }
        else
        {
            CcChrome.Paint(button, null, ColorToken.Text, null);
        }

        button.BorderThickness = new Thickness(0);
        button.Padding = new Thickness(4, 0, 4, 0);
        return button;
    }

    private Border Test(TestModel model)
    {
        var close = IconOnly("close", model.CloseName, _viewModel.CloseTest);
        var head = new DockPanel { LastChildFill = true };
        DockPanel.SetDock(close, Dock.Right);
        head.Children.Add(close);
        head.Children.Add(Ui.Text(model.Title, 14, bold: true));
        var sequence = new List<UIElement>();
        foreach (var item in model.Sequence)
        {
            if (item.Separator is { } separator)
            {
                sequence.Add(Ui.Separator(separator, 13));
            }

            var chip = Ui.Card(
                Ui.IconLabel(
                    item.Icon,
                    item.Label,
                    16,
                    14,
                    bold: false,
                    ink: item.Lit ? ColorToken.OnAccent : ColorToken.Text,
                    iconInk: item.Lit ? ColorToken.OnAccent : ColorToken.Text
                ),
                item.Lit ? ColorToken.Accent : ColorToken.Card,
                item.Lit ? ColorToken.Accent : ColorToken.Border,
                8,
                new Thickness(12, 9, 12, 9)
            );
            sequence.Add(chip);
        }

        var shown = Ui.Card(Ui.Wrap(6, sequence), ColorToken.Win, null, 10, new Thickness(12));
        shown.MinHeight = 64;
        var play = Ui.Button(
            Ui.IconLabel("replay", model.PlayText),
            model.PlayText,
            _viewModel.Play,
            ColorToken.Card,
            stroke: ColorToken.Border
        );
        var what = Ui.Text(model.What, 13, wrap: true);
        what.Margin = new Thickness(10, 0, 0, 0);
        var line = new DockPanel { LastChildFill = true };
        DockPanel.SetDock(play, Dock.Left);
        play.VerticalAlignment = VerticalAlignment.Top;
        line.Children.Add(play);
        line.Children.Add(what);
        var body = Ui.Column(12, head, shown, line);
        if (model.Phase is { } phase)
        {
            var phaseLine = Ui.Card(
                Ui.Row(
                    8,
                    Ui.Icon(model.PhaseIcon, 18, ColorToken.Accent),
                    Ui.Text(phase, 13, bold: true, wrap: true)
                ),
                ColorToken.Win,
                null,
                10,
                new Thickness(10, 8, 10, 8)
            );
            AutomationProperties.SetLiveSetting(phaseLine, AutomationLiveSetting.Polite);
            body.Children.Add(phaseLine);
        }

        body.Children.Add(Ui.Line());
        body.Children.Add(Ui.Caption(model.TargetsLabel));
        if (model.NoApps is { } noApps)
        {
            body.Children.Add(Ui.Text(noApps, 13, ink: ColorToken.Muted, wrap: true));
        }
        else
        {
            body.Children.Add(
                Ui.Wrap(
                    6,
                    model.Targets.Select(target =>
                        (UIElement)
                            Ui.Choice(
                                Ui.IconLabel("apps", target.Name),
                                target.Name,
                                target.Selected,
                                () => _viewModel.ChooseTarget(target.Process),
                                44,
                                22,
                                role: CcToggleRole.Option
                            )
                    )
                )
            );
        }

        var live = Ui.Button(
            Ui.IconLabel(
                model.LiveArmed ? "verified" : "send",
                model.LiveText,
                20,
                14,
                ink: model.LiveArmed ? ColorToken.OnWarn : ColorToken.OnAccent,
                iconInk: model.LiveArmed ? ColorToken.OnWarn : ColorToken.OnAccent
            ),
            model.LiveText,
            _viewModel.TryLive,
            model.LiveArmed ? ColorToken.Warn : ColorToken.Accent,
            model.LiveArmed ? ColorToken.OnWarn : ColorToken.OnAccent,
            height: 48
        );
        if (model.LiveArmed)
        {
            AutomationProperties.SetLiveSetting(live, AutomationLiveSetting.Polite);
        }

        live.IsEnabled = model.CanLive;
        body.Children.Add(live);
        body.Children.Add(Ui.Text(model.How, 12, ink: ColorToken.Muted, wrap: true));
        if (model.Question is { } question)
        {
            var yes = Ui.Button(
                Ui.Text(model.YesText, 14, bold: true, ink: ColorToken.OnAccent),
                model.YesText,
                () => _viewModel.Answer(true),
                ColorToken.Accent,
                ColorToken.OnAccent
            );
            var no = Ui.Button(
                Ui.Text(model.NoText, 14, bold: true),
                model.NoText,
                () => _viewModel.Answer(false),
                stroke: ColorToken.Border
            );
            var ask = Ui.Card(
                Ui.Column(
                    8,
                    Ui.Text(question, 14, bold: true, wrap: true),
                    Ui.Columns(2, 6, [yes, no])
                ),
                ColorToken.Card,
                null,
                10,
                new Thickness(10)
            );
            AutomationProperties.SetLiveSetting(ask, AutomationLiveSetting.Polite);
            body.Children.Add(ask);
        }

        if (model.Answer is { } answer)
        {
            var ok = Ui.Row(
                6,
                Ui.Icon("check_circle", 18, ColorToken.Accent),
                Ui.Text(answer, 13, bold: true, wrap: true)
            );
            AutomationProperties.SetLiveSetting(ok, AutomationLiveSetting.Polite);
            body.Children.Add(ok);
        }

        if (model.TipsTitle is { } tips)
        {
            var list = Ui.Column(6, Ui.Text(tips, 13, bold: true));
            foreach (var tip in model.Tips)
            {
                var icon = Ui.Icon("lightbulb", 16, ColorToken.Warn);
                icon.VerticalAlignment = VerticalAlignment.Top;
                list.Children.Add(Ui.Row(6, icon, Ui.Text(tip, 13, wrap: true)));
            }

            body.Children.Add(Ui.Card(list, ColorToken.WarnWash, null, 10, new Thickness(10)));
        }

        return Ui.Card(body, ColorToken.AccentWash, ColorToken.Accent, 12, new Thickness(12));
    }

    private Grid Footer(FooterModel model)
    {
        var test = Ui.Choice(
            Ui.IconLabel(
                "play_arrow",
                model.TestText,
                20,
                14,
                ink: ColorToken.OnAccent,
                iconInk: ColorToken.OnAccent
            ),
            model.TestText,
            model.TestOpen,
            _viewModel.ToggleTest,
            48,
            10,
            role: CcToggleRole.Expander
        );
        CcChrome.Paint(test, ColorToken.Accent, ColorToken.OnAccent, ColorToken.Accent);
        var duplicate = Ui.Button(
            Ui.IconLabel("content_copy", model.DuplicateText, 18, 13),
            model.DuplicateText,
            _viewModel.Duplicate,
            stroke: ColorToken.Border,
            height: 48
        );
        duplicate.IsEnabled = model.Saved;
        var delete = Ui.Button(
            Ui.IconLabel(
                model.DeleteArmed ? "warning" : "delete",
                model.DeleteText,
                18,
                13,
                ink: model.DeleteArmed ? ColorToken.OnDanger : ColorToken.DangerText,
                iconInk: model.DeleteArmed ? ColorToken.OnDanger : ColorToken.DangerText
            ),
            model.DeleteText,
            _viewModel.Delete,
            model.DeleteArmed ? ColorToken.Danger : null,
            stroke: model.DeleteArmed ? ColorToken.Danger : ColorToken.Border,
            height: 48
        );
        delete.IsEnabled = model.Saved;
        foreach (
            var button in new System.Windows.Controls.Primitives.ButtonBase[]
            {
                test,
                duplicate,
                delete,
            }
        )
        {
            button.Padding = new Thickness(4, 0, 4, 0);
        }

        var grid = new Grid();
        grid.ColumnDefinitions.Add(
            new ColumnDefinition { Width = new GridLength(1.2, GridUnitType.Star) }
        );
        grid.ColumnDefinitions.Add(
            new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }
        );
        grid.ColumnDefinitions.Add(
            new ColumnDefinition { Width = new GridLength(1.2, GridUnitType.Star) }
        );
        duplicate.Margin = new Thickness(8, 0, 8, 0);
        Grid.SetColumn(duplicate, 1);
        Grid.SetColumn(delete, 2);
        grid.Children.Add(test);
        grid.Children.Add(duplicate);
        grid.Children.Add(delete);
        return grid;
    }

    private static CcButton IconOnly(string icon, string name, Action click)
    {
        var button = Ui.Button(Ui.Icon(icon, 22), name, click);
        button.Width = 44;
        button.Padding = new Thickness(0);
        button.BorderThickness = new Thickness(0);
        return button;
    }

    private void Dictate(TextField field)
    {
        _ = Keyboard.Focus(field.Box);
        _viewModel.Dictate();
    }
}
