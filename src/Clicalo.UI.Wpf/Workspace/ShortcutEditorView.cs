using System.ComponentModel;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Clicalo.Domain.Primitives;
using Clicalo.Presentation.ControlCenter.Editor;
using Clicalo.UI.Wpf.Controls;
using Clicalo.UI.Wpf.Resources;
using Clicalo.UI.Wpf.Theming;
using Clicalo.UI.Wpf.Theming.Generated;
using Clicalo.UI.Wpf.Workspace.Editor;
using Clicalo.UI.Wpf.Workspace.Internal;
using Clicalo.UI.Wpf.Workspace.Shortcuts;

namespace Clicalo.UI.Wpf.Workspace;

/// <summary>
/// State B of the editor column of «Atajos» (docs/05 §1 B) in direction C (D12): the regions of the shortcut in an area
/// that scrolls with its fade (the repeated combination, the preview and the name, «Qué hace», the combination or the
/// fields of its kind, «Fijar en Siempre visible» and «Más opciones»), the fixed foot with Probar, Duplicar and
/// Eliminar, and «Probar» as a layer over the regions, never under the foot. It draws
/// <see cref="ShortcutEditorViewModel.Model"/> region by region, rebuilding only the regions whose model changed, and
/// keeps its text fields alive so typing, dictation and the touch keyboard are never interrupted. The icon picker is a
/// layer of the section, wider than this column.
/// </summary>
public sealed class ShortcutEditorView : Grid
{
    /// <summary>The space between two regions.</summary>
    private const double RegionGap = Space.S6;

    private readonly ShortcutEditorViewModel _viewModel;
    private readonly StackPanel _regions = new();
    private readonly ContentControl _duplicate = Region();
    private readonly ContentControl _identity = Region();
    private readonly ContentControl _kinds = Region();
    private readonly ContentControl _combo = Region();
    private readonly ContentControl _fields = Region();
    private readonly ContentControl _pin = Region();
    private readonly ContentControl _more = Region();
    private readonly ContentControl _footer = new() { Focusable = false };
    private readonly Border _test;
    private readonly ContentControl _testContent = new() { Focusable = false };
    private readonly ThemedScrollViewer _scroll;
    private readonly TextInput _name;
    private readonly TextInput _text;
    private readonly TextInput _target;
    private readonly ProgramListView _programs;
    private readonly Dictionary<int, TextInput> _stepTexts = [];
    private EditorModel? _shown;
    private string _shownKey = string.Empty;

    /// <summary>Creates the view of <paramref name="viewModel"/>.</summary>
    /// <param name="viewModel">The editor.</param>
    public ShortcutEditorView(ShortcutEditorViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        _viewModel = viewModel;
        _name = new TextInput(string.Empty, string.Empty);
        _name.Changed += (_, _) => _viewModel.Rename(_name.Text);
        _name.DictateRequested += (_, _) => _viewModel.DictateName();
        _text = new TextInput(string.Empty, string.Empty, multiline: true);
        _text.Changed += (_, _) => _viewModel.SetText(_text.Text);
        _text.DictateRequested += (_, _) => _viewModel.Dictate();
        _target = new TextInput(string.Empty, string.Empty);
        _target.Changed += (_, _) => _viewModel.SetTarget(_target.Text);
        _target.DictateRequested += (_, _) => _viewModel.Dictate();
        _programs = new ProgramListView(
            _viewModel.FilterPrograms,
            _viewModel.PickInstalled,
            _ => _viewModel.Dictate()
        );
        foreach (
            var region in new[] { _duplicate, _identity, _kinds, _combo, _fields, _pin, _more }
        )
        {
            _regions.Children.Add(region);
        }

        RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        _scroll = new ThemedScrollViewer
        {
            Content = _regions,
            Padding = new Thickness(Space.S4, Space.S4, Space.S2, Space.S6),
        };
        Children.Add(_scroll);
        // «Probar» is a layer over the regions (D12): it never pushes them, and the foot stays where the finger is.
        _test = Parts.Layer(
            new ThemedScrollViewer
            {
                Content = _testContent,
                Padding = new Thickness(Space.S4, Space.S4, Space.S2, Space.S6),
            }
        );
        _test.Visibility = Visibility.Collapsed;
        Children.Add(_test);
        var foot = new Border
        {
            Child = _footer,
            Padding = new Thickness(Space.S4, Space.S3, Space.S4, Space.S3),
            BorderThickness = new Thickness(0, Theming.Generated.Stroke.Line, 0, 0),
        };
        Ui.Ink(foot, Border.BackgroundProperty, ColorToken.Win);
        Ui.Ink(foot, Border.BorderBrushProperty, ColorToken.Border);
        SetRow(foot, 1);
        Children.Add(foot);

        viewModel.PropertyChanged += OnChanged;
        Render();
    }

    /// <summary>
    /// The installed programs of «Elegir programa» (EDI-014): one list for the life of the editor, so its hundreds of
    /// choices are not built again when a letter is typed.
    /// </summary>
    public ProgramListView Programs => _programs;

    /// <summary>Stops following the view model.</summary>
    public void Detach() => _viewModel.PropertyChanged -= OnChanged;

    // A region starts collapsed: one whose model is empty from the first render is never shown, so its gap never
    // pushes the regions under it.
    private static ContentControl Region() =>
        new()
        {
            Focusable = false,
            Margin = new Thickness(0, 0, 0, RegionGap),
            Visibility = Visibility.Collapsed,
        };

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
            _scroll.ScrollToTop();
        }

        if (before?.Duplicate != model.Duplicate)
        {
            Show(_duplicate, model.Duplicate is { } duplicate ? DuplicateCard(duplicate) : null);
        }

        if (before?.Identity != model.Identity)
        {
            Show(_identity, Identity(model.Identity, keyChanged));
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
            _testContent.Content = model.Test is { } test ? Test(test) : null;
            _test.Visibility = model.Test is null ? Visibility.Collapsed : Visibility.Visible;
        }

        if (before?.Footer != model.Footer)
        {
            _footer.Content = Footer(model.Footer);
        }

        if (keyChanged && model.Identity.Name.Length == 0)
        {
            _ = Dispatcher.BeginInvoke(DispatcherPriority.Input, () => Keyboard.Focus(_name.Box));
        }
    }

    // ---- Combinación repetida (REP-001 to REP-004) ---------------------------------------------------------------

    private Card DuplicateCard(DuplicateCardModel model)
    {
        var caret = Ui.Icon(
            model.Expanded ? "expand_less" : "expand_more",
            IconBox.Medium,
            ColorToken.Muted
        );
        caret.Margin = new Thickness(Space.S2, 0, 0, 0);
        var headContent = new DockPanel { LastChildFill = true };
        DockPanel.SetDock(caret, Dock.Right);
        headContent.Children.Add(caret);
        headContent.Children.Add(
            Parts.IconLine(
                "warning",
                model.Head,
                ColorToken.WarnText,
                ColorToken.Text,
                TextStyleToken.Label
            )
        );
        var head = new TouchButton
        {
            Content = headContent,
            Variant = ButtonVariant.Tertiary,
            IsExpanded = model.Expanded,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Padding = new Thickness(Space.S2, 0, Space.S2, 0),
        };
        AutomationProperties.SetName(head, model.Head);
        head.Click += (_, _) => _viewModel.ToggleDuplicates();
        var previous = Parts.Button(
            string.Empty,
            "chevron_left",
            ButtonVariant.Tertiary,
            () => _viewModel.ShowRepeated(-1),
            model.PrevName
        );
        var next = Parts.Button(
            string.Empty,
            "chevron_right",
            ButtonVariant.Tertiary,
            () => _viewModel.ShowRepeated(1),
            model.NextName
        );
        // With one repeated combination there is nowhere for ‹ › to go: they would look enabled and do nothing.
        previous.Visibility = model.CanBrowse ? Visibility.Visible : Visibility.Collapsed;
        next.Visibility = previous.Visibility;
        var bar = new DockPanel { LastChildFill = true };
        DockPanel.SetDock(next, Dock.Right);
        DockPanel.SetDock(previous, Dock.Right);
        bar.Children.Add(next);
        bar.Children.Add(previous);
        bar.Children.Add(head);
        AutomationProperties.SetLiveSetting(bar, AutomationLiveSetting.Polite);
        var body = new StackPanel();
        body.Children.Add(bar);
        if (model.Expanded)
        {
            foreach (var row in model.Rows)
            {
                body.Children.Add(RepeatedRow(model, row));
            }

            var advice = Parts.Note(model.Advice, ColorToken.Text);
            advice.Margin = new Thickness(Space.S2, Space.S2, Space.S2, Space.S2);
            body.Children.Add(advice);
            if (model.MoveText is { } move)
            {
                var keep = Parts.Button(
                    move,
                    "push_pin",
                    model.MoveArmed ? ButtonVariant.Danger : ButtonVariant.Primary,
                    _viewModel.KeepOnlyInAlwaysVisible
                );
                keep.Margin = new Thickness(0, 0, 0, Space.S2);
                body.Children.Add(keep);
            }

            var fine = Parts.Button(
                model.FineText,
                "thumb_up",
                ButtonVariant.Secondary,
                _viewModel.AcceptRepeated
            );
            fine.Margin = new Thickness(0, 0, 0, Space.S2);
            body.Children.Add(fine);
            body.Children.Add(
                Parts.Button(
                    model.UseOtherText,
                    "swap_horiz",
                    ButtonVariant.Secondary,
                    _viewModel.UseOtherCombination
                )
            );
        }

        return new Card
        {
            Content = body,
            Tone = CardTone.Warn,
            Padding = new Thickness(
                Space.S1,
                Space.S1,
                Space.S1,
                model.Expanded ? Space.S3 : Space.S1
            ),
        };
    }

    private DockPanel RepeatedRow(DuplicateCardModel model, DuplicateRow row)
    {
        var icon = Ui.CategoryIcon(row.Icon, IconBox.Small, row.Category);
        icon.Margin = new Thickness(0, 0, Space.S2, 0);
        var where = new StyledText(row.Where, TextStyleToken.BodySmall, ColorToken.Muted)
        {
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(Space.S2, 0, 0, 0),
        };
        var name = new StyledText(row.Name, TextStyleToken.Label)
        {
            VerticalAlignment = VerticalAlignment.Center,
        };
        var text = new DockPanel { LastChildFill = true };
        DockPanel.SetDock(icon, Dock.Left);
        DockPanel.SetDock(name, Dock.Left);
        text.Children.Add(icon);
        text.Children.Add(name);
        if (row.Editing)
        {
            var editing = new Badge { Text = model.EditingText, Tone = BadgeTone.Warn };
            editing.Margin = new Thickness(Space.S2, 0, 0, 0);
            DockPanel.SetDock(editing, Dock.Right);
            text.Children.Add(editing);
        }

        text.Children.Add(where);
        var open = new TouchButton
        {
            Content = text,
            Variant = ButtonVariant.Tertiary,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Padding = new Thickness(Space.S2, 0, Space.S2, 0),
        };
        AutomationProperties.SetName(open, row.Name + " · " + row.Where);
        open.Click += (_, _) => _viewModel.OpenAppearance(row.Id);
        var delete = Parts.Button(
            row.Armed ? model.ConfirmText : string.Empty,
            "delete",
            ButtonVariant.Danger,
            () => _viewModel.DeleteAppearance(row.Id),
            row.DeleteName
        );
        var line = new DockPanel
        {
            LastChildFill = true,
            Margin = new Thickness(0, Space.S1, 0, 0),
        };
        DockPanel.SetDock(delete, Dock.Right);
        line.Children.Add(delete);
        line.Children.Add(open);
        return line;
    }

    // ---- Vista previa y nombre (EDI-001 to EDI-005) ---------------------------------------------------------------

    private DockPanel Identity(IdentityModel model, bool keyChanged)
    {
        var icon = Ui.CategoryIcon(model.Icon, IconBox.Large, model.Category);
        var tileName = new StyledText(model.TileName, TextStyleToken.Label)
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(Space.S1, Space.S1, Space.S1, 0),
        };
        var center = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        center.Children.Add(icon);
        center.Children.Add(tileName);
        var pencil = Ui.Icon("edit", IconBox.Badge, ColorToken.Muted);
        pencil.HorizontalAlignment = HorizontalAlignment.Right;
        pencil.VerticalAlignment = VerticalAlignment.Top;
        pencil.Margin = new Thickness(Space.S1 + Theming.Generated.Stroke.Selected);
        var layers = new Grid();
        layers.Children.Add(center);
        layers.Children.Add(pencil);
        var preview = new OptionButton
        {
            Kind = OptionKind.Tile,
            Content = layers,
            Role = OptionRole.Expander,
            IsSelected = model.PickerOpen,
            Width = GridTiles.TileHeight,
            Height = GridTiles.TileHeight,
            Padding = new Thickness(0),
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 0, Space.S3, 0),
        };
        AutomationProperties.SetName(preview, model.ChangeIconName);
        preview.Click += (_, _) => _viewModel.TogglePicker();

        Detach(_name);
        AutomationProperties.SetName(_name.Box, model.NameLabel);
        _name.DictateName = model.DictateName;
        _name.SetPlaceholder(model.Placeholder);
        _name.Show(model.Name, force: keyChanged);
        _name.Margin = new Thickness(0, Space.S2, 0, 0);
        var right = new StackPanel();
        right.Children.Add(new Overline(model.NameLabel));
        right.Children.Add(_name);
        var top = new DockPanel { LastChildFill = true };
        DockPanel.SetDock(preview, Dock.Left);
        top.Children.Add(preview);
        top.Children.Add(right);
        var hint = Parts.IconLine(
            model.HintIcon,
            model.Hint,
            ColorToken.AccentInk,
            style: TextStyleToken.Caption
        );
        hint.Margin = new Thickness(0, Space.S3, 0, 0);
        var column = new DockPanel { LastChildFill = true };
        DockPanel.SetDock(top, Dock.Top);
        column.Children.Add(top);
        column.Children.Add(hint);
        return column;
    }

    // ---- Qué hace (EDI-006) --------------------------------------------------------------------------------------

    private StackPanel Kinds(KindsModel model)
    {
        var options = model
            .Options.Select(option =>
            {
                var card = new OptionButton
                {
                    Kind = OptionKind.Card,
                    Content = option.Label,
                    Symbol = option.Icon,
                    IsSelected = option.Selected,
                };
                AutomationProperties.SetName(card, option.Label);
                card.Click += (_, _) => _viewModel.SetKind(option.Kind);
                return (UIElement)card;
            })
            .ToList();
        return Parts.Section(
            model.Title,
            Ui.Columns(4, Space.S2, options),
            model.Description.Length == 0 ? null : Parts.Note(model.Description)
        );
    }

    // ---- Los campos de cada tipo (EDI-011 to EDI-014) ------------------------------------------------------------

    private StackPanel? Fields(EditorModel model, bool keyChanged)
    {
        if (model.Text is { } text)
        {
            Detach(_text);
            AutomationProperties.SetName(_text.Box, text.Label);
            _text.DictateName = text.DictateName;
            _text.Show(_viewModel.RevealText(), force: keyChanged);
            return Parts.Section(
                text.Label,
                _text,
                Parts.Note(text.Hint, style: TextStyleToken.Caption),
                Parts.IconLine("lock", text.Encrypted, style: TextStyleToken.Caption)
            );
        }

        if (model.Mouse is { } mouse)
        {
            var options = mouse
                .Options.Select(option =>
                {
                    var button = new OptionButton
                    {
                        Kind = OptionKind.Tile,
                        Content = option.Label,
                        Symbol = option.Icon,
                        IsSelected = option.Selected,
                        HorizontalContentAlignment = HorizontalAlignment.Left,
                    };
                    AutomationProperties.SetName(button, option.AccessibleName);
                    button.Click += (_, _) => _viewModel.SetMouse(option.Op);
                    return (UIElement)button;
                })
                .ToList();
            var column = Parts.Section(mouse.Label, Ui.Columns(2, Space.S2, options));
            if (!mouse.Speeds.IsEmpty)
            {
                var speeds = Parts.Section(
                    mouse.SpeedLabel,
                    Parts.Track([
                        .. mouse.Speeds.Select(speed =>
                            (UIElement)
                                Parts.Segment(
                                    speed.Label,
                                    speed.Selected,
                                    () => _viewModel.SetSpeed(speed.Speed)
                                )
                        ),
                    ])
                );
                speeds.Margin = new Thickness(0, Space.S4, 0, 0);
                column.Children.Add(speeds);
            }

            return column;
        }

        if (model.Steps is { } steps)
        {
            return Steps(steps);
        }

        if (model.Target is { } target)
        {
            return Target(target, keyChanged);
        }

        return null;
    }

    private StackPanel Target(TargetModel target, bool keyChanged)
    {
        Detach(_target);
        AutomationProperties.SetName(_target.Box, target.Label);
        _target.DictateName = target.DictateName;
        _target.Show(target.Value, force: keyChanged);
        _target.Warn(target.Invalid);
        var column = Parts.Section(
            target.Label,
            _target,
            target.Invalid ? Parts.Note(target.InvalidText, ColorToken.WarnText) : null
        );
        if (target.PickLabel.Length > 0 && !target.Picks.IsEmpty)
        {
            var picks = new WrapPanel { Margin = new Thickness(0, 0, -Space.S2, -Space.S2) };
            foreach (var pick in target.Picks)
            {
                var chip = new OptionButton
                {
                    Kind = OptionKind.Chip,
                    Content = pick.Name,
                    Symbol = "apps",
                    Margin = new Thickness(0, 0, Space.S2, Space.S2),
                };
                AutomationProperties.SetName(chip, pick.Name);
                chip.Click += (_, _) => _viewModel.PickProgram(pick.Process);
                picks.Children.Add(chip);
            }

            var open = Parts.Section(
                target.PickLabel,
                Parts.Note(target.OpenLabel, style: TextStyleToken.Caption),
                picks
            );
            open.Margin = new Thickness(0, Space.S4, 0, 0);
            column.Children.Add(open);
        }

        // EDI-014: the list of programs is kept between renders; only its filter and its mark change.
        Detach(_programs);
        if (target.ProgramsLoading is not null || !target.Programs.IsEmpty)
        {
            _programs.Apply(target);
            var programs = target.Picks.IsEmpty
                ? Parts.Section(target.PickLabel, _programs)
                : Parts.Section(null, _programs);
            programs.Margin = new Thickness(0, Space.S4, 0, 0);
            column.Children.Add(programs);
        }

        return column;
    }

    // ---- Macro (EDI-012) -----------------------------------------------------------------------------------------

    private StackPanel Steps(StepsModel model)
    {
        var column = Parts.Section(model.Label);
        foreach (var step in model.Steps)
        {
            var card = Step(model, step);
            card.Margin = new Thickness(0, Space.S2, 0, 0);
            column.Children.Add(card);
        }

        var adds = model
            .Add.Select(add =>
            {
                var content = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
                content.Children.Add(Ui.Icon(add.Icon, IconBox.Small, ColorToken.AccentInk));
                content.Children.Add(
                    new StyledText(add.Label, TextStyleToken.LabelSmall)
                    {
                        HorizontalAlignment = HorizontalAlignment.Center,
                        Margin = new Thickness(Space.S1, Space.S1, Space.S1, 0),
                    }
                );
                var button = new TouchButton
                {
                    Content = GridTiles.Dashed(content, ControlHeight.Large),
                    Variant = ButtonVariant.Tertiary,
                    Size = ControlSize.Large,
                    Padding = new Thickness(0),
                    HorizontalContentAlignment = HorizontalAlignment.Stretch,
                    VerticalContentAlignment = VerticalAlignment.Stretch,
                };
                AutomationProperties.SetName(button, add.Label);
                button.Click += (_, _) => _viewModel.AddStep(add.Kind);
                return (UIElement)button;
            })
            .ToList();
        var grid = Ui.Columns(4, Space.S2, adds);
        grid.Margin = new Thickness(0, Space.S3, 0, 0);
        column.Children.Add(grid);
        return column;
    }

    /// <summary>
    /// A step: its number, its icon and what it does in one line with «…» (the whole step is its accessible name), and
    /// ↑ ↓ × in a row of their own under it, so the text keeps the width of the column.
    /// </summary>
    private Border Step(StepsModel model, StepRow step)
    {
        var number = new Badge { Text = step.Number, Tone = BadgeTone.Accent };
        number.Margin = new Thickness(0, 0, Space.S2, 0);
        var icon = Ui.Icon(step.Icon, IconBox.Small, ColorToken.Muted);
        icon.Margin = new Thickness(0, 0, Space.S2, 0);
        var caret = Ui.Icon(
            step.Editor == StepEditorKind.None ? "edit" : "expand_less",
            IconBox.Small,
            ColorToken.Muted
        );
        caret.Margin = new Thickness(Space.S2, 0, 0, 0);
        var words = new DockPanel { LastChildFill = true };
        DockPanel.SetDock(number, Dock.Left);
        DockPanel.SetDock(icon, Dock.Left);
        DockPanel.SetDock(caret, Dock.Right);
        words.Children.Add(number);
        words.Children.Add(icon);
        words.Children.Add(caret);
        words.Children.Add(
            new StyledText(step.Text, TextStyleToken.Body)
            {
                VerticalAlignment = VerticalAlignment.Center,
            }
        );
        var edit = new TouchButton
        {
            Content = words,
            Variant = ButtonVariant.Tertiary,
            IsExpanded = step.Editor != StepEditorKind.None,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Padding = new Thickness(Space.S2, 0, Space.S2, 0),
        };
        AutomationProperties.SetName(edit, model.EditName + " " + step.Text);
        edit.Click += (_, _) => _viewModel.ToggleStep(step.Index);
        var up = Parts.Button(
            string.Empty,
            "arrow_upward",
            ButtonVariant.Tertiary,
            () => _viewModel.MoveStep(step.Index, -1),
            model.UpName
        );
        up.IsEnabled = step.CanUp;
        var down = Parts.Button(
            string.Empty,
            "arrow_downward",
            ButtonVariant.Tertiary,
            () => _viewModel.MoveStep(step.Index, 1),
            model.DownName
        );
        down.IsEnabled = step.CanDown;
        var delete = Parts.Button(
            step.Armed ? model.ConfirmText : string.Empty,
            "close",
            ButtonVariant.Danger,
            () => _viewModel.DeleteStep(step.Index),
            model.DeleteName
        );
        // The header, the editor and ↑ ↓ × share one inset from the edge of the card, so their edges line up.
        var tools = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 0, Space.S2, Space.S1),
        };
        tools.Children.Add(up);
        tools.Children.Add(down);
        tools.Children.Add(delete);
        var body = new StackPanel();
        body.Children.Add(edit);
        var editor = StepEditor(model, step);
        if (editor is not null)
        {
            editor.Margin = new Thickness(Space.S2, Space.S1, Space.S2, Space.S2);
            body.Children.Add(editor);
        }

        body.Children.Add(tools);
        var open = step.Editor != StepEditorKind.None;
        if (open)
        {
            // The step that was just opened is brought into view with its editor.
            body.Loaded += (_, _) => body.BringIntoView();
        }

        return Ui.Card(
            body,
            ColorToken.Card,
            open ? ColorToken.Accent : ColorToken.Border,
            Radii.Medium,
            new Thickness(Space.S1),
            open ? Theming.Generated.Stroke.Selected : Theming.Generated.Stroke.Line
        );
    }

    private FrameworkElement? StepEditor(StepsModel model, StepRow step)
    {
        switch (step.Editor)
        {
            case StepEditorKind.Wait:
                var less = Parts.Button(
                    string.Empty,
                    "remove",
                    ButtonVariant.Secondary,
                    () => _viewModel.NudgeWait(step.Index, -1),
                    model.LessName
                );
                var more = Parts.Button(
                    string.Empty,
                    "add",
                    ButtonVariant.Secondary,
                    () => _viewModel.NudgeWait(step.Index, 1),
                    model.MoreName
                );
                var value = new StyledText(step.Wait, TextStyleToken.Label)
                {
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                };
                var wait = new DockPanel { LastChildFill = true };
                DockPanel.SetDock(less, Dock.Left);
                DockPanel.SetDock(more, Dock.Right);
                wait.Children.Add(less);
                wait.Children.Add(more);
                wait.Children.Add(value);
                return wait;
            case StepEditorKind.Text:
                return StepText(step.Index, model.Label, model.DictateName);
            case StepEditorKind.Mouse:
                var chips = new WrapPanel { Margin = new Thickness(0, 0, -Space.S2, -Space.S2) };
                foreach (var option in step.Mouse)
                {
                    var chip = new OptionButton
                    {
                        Kind = OptionKind.Chip,
                        Content = option.Label,
                        IsSelected = option.Selected,
                        Margin = new Thickness(0, 0, Space.S2, Space.S2),
                    };
                    AutomationProperties.SetName(chip, option.AccessibleName);
                    chip.Click += (_, _) => _viewModel.SetStepMouse(step.Index, option.Op);
                    chips.Children.Add(chip);
                }

                return chips;
            default:
                return null;
        }
    }

    private TextInput StepText(int index, string name, string dictateName)
    {
        if (!_stepTexts.TryGetValue(index, out var field))
        {
            field = new TextInput(name, string.Empty, multiline: true);
            field.Changed += (_, _) => _viewModel.SetStepText(index, field.Text);
            field.DictateRequested += (_, _) => _viewModel.Dictate();
            _stepTexts[index] = field;
        }

        Detach(field);
        field.DictateName = dictateName;
        field.Show(_viewModel.RevealStepText(index));
        return field;
    }

    // ---- Más opciones (EDI-015 to EDI-019) -----------------------------------------------------------------------

    private StackPanel More(MoreModel model)
    {
        var caret = Ui.Icon(
            model.Expanded ? "expand_less" : "expand_more",
            IconBox.Medium,
            ColorToken.Muted
        );
        caret.Margin = new Thickness(Space.S2, 0, 0, 0);
        var position = new StyledText(model.Position, TextStyleToken.Caption, ColorToken.Muted)
        {
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(Space.S2, 0, 0, 0),
        };
        var title = new StyledText(model.Title, TextStyleToken.Label)
        {
            VerticalAlignment = VerticalAlignment.Center,
        };
        var line = new DockPanel { LastChildFill = true };
        DockPanel.SetDock(caret, Dock.Right);
        DockPanel.SetDock(position, Dock.Right);
        line.Children.Add(caret);
        line.Children.Add(position);
        line.Children.Add(title);
        var head = new TouchButton
        {
            Content = line,
            Variant = ButtonVariant.Secondary,
            Size = ControlSize.Medium,
            IsExpanded = model.Expanded,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Padding = new Thickness(Space.S3, 0, Space.S2, 0),
        };
        AutomationProperties.SetName(head, model.Title);
        head.Click += (_, _) => _viewModel.ToggleMore();
        var column = new StackPanel();
        column.Children.Add(head);
        if (!model.Expanded)
        {
            return column;
        }

        var positions = Ui.Columns(
            4,
            Space.S2,
            [
                .. model.Positions.Select(option =>
                {
                    var content = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
                    content.Children.Add(Ui.Icon(option.Icon, IconBox.Small, ColorToken.Text));
                    content.Children.Add(
                        new StyledText(option.Label, TextStyleToken.LabelSmall)
                        {
                            HorizontalAlignment = HorizontalAlignment.Center,
                            Margin = new Thickness(Space.S1, Space.S1, Space.S1, 0),
                        }
                    );
                    var button = new TouchButton
                    {
                        Content = content,
                        Variant = ButtonVariant.Secondary,
                        Size = ControlSize.Large,
                        Padding = new Thickness(0),
                        IsEnabled = option.Enabled,
                    };
                    AutomationProperties.SetName(button, option.Label);
                    button.Click += (_, _) => _viewModel.Move(option.Move);
                    return (UIElement)button;
                }),
            ]
        );
        var inner = new StackPanel();
        inner.Children.Add(Gap(Parts.Section(model.PositionLabel, positions)));
        if (model.HoldLabel is { } hold)
        {
            inner.Children.Add(
                Gap(
                    Parts.Section(
                        hold,
                        Choices(model.Holds, _viewModel.SetHold),
                        Parts.Note(model.HoldText, style: TextStyleToken.Caption),
                        Parts.Note(model.HoldSwitchText, style: TextStyleToken.Caption)
                    )
                )
            );
        }

        if (model.MethodLabel is { } method)
        {
            inner.Children.Add(
                Gap(
                    Parts.Section(
                        method,
                        Choices(model.Methods, _viewModel.SetMethod),
                        Parts.IconLine("lock", model.EncryptedText, style: TextStyleToken.Caption)
                    )
                )
            );
        }

        inner.Children.Add(
            Gap(
                Ui.SwitchRow(
                    "verified",
                    model.ConfirmTitle,
                    model.ConfirmText,
                    model.Confirm,
                    _viewModel.ToggleConfirm
                )
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
        var number = new Badge { Text = model.VoiceNumber, Tone = BadgeTone.Warn };
        number.Margin = new Thickness(0, 0, Space.S2, 0);
        number.VerticalAlignment = VerticalAlignment.Top;
        var voice = new DockPanel
        {
            LastChildFill = true,
            Margin = new Thickness(0, Space.S3, 0, Space.S2),
        };
        DockPanel.SetDock(number, Dock.Left);
        voice.Children.Add(number);
        voice.Children.Add(Parts.Note(model.VoiceLine, ColorToken.Text));
        inner.Children.Add(voice);
        var how = new TouchButton
        {
            Content = model.VoiceHowTitle,
            Symbol = "record_voice_over",
            Variant = ButtonVariant.Tertiary,
            IsExpanded = model.VoiceHowOpen,
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        AutomationProperties.SetName(how, model.VoiceHowTitle);
        how.Click += (_, _) => _viewModel.ToggleVoiceHow();
        inner.Children.Add(how);
        if (model.VoiceHowOpen)
        {
            var steps = new StackPanel { Margin = new Thickness(0, Space.S2, 0, 0) };
            for (var i = 0; i < model.VoiceSteps.Count; i++)
            {
                var n = new Badge
                {
                    Text = (i + 1).ToString(System.Globalization.CultureInfo.InvariantCulture),
                    Tone = BadgeTone.Accent,
                    VerticalAlignment = VerticalAlignment.Top,
                    Margin = new Thickness(0, 0, Space.S2, 0),
                };
                var row = new DockPanel
                {
                    LastChildFill = true,
                    Margin = new Thickness(0, 0, 0, Space.S2),
                };
                DockPanel.SetDock(n, Dock.Left);
                row.Children.Add(n);
                row.Children.Add(Parts.Note(model.VoiceSteps[i], ColorToken.Text));
                steps.Children.Add(row);
            }

            inner.Children.Add(steps);
        }

        inner.Margin = new Thickness(0, Space.S4, 0, 0);
        column.Children.Add(inner);
        return column;
    }

    private static FrameworkElement Gap(FrameworkElement element)
    {
        element.Margin = new Thickness(0, 0, 0, Space.S4);
        return element;
    }

    private static Border Choices(ValueList<ChoiceOption> options, Action<int> choose) =>
        Parts.Track([
            .. options.Select(option =>
                (UIElement)Parts.Segment(option.Label, option.Selected, () => choose(option.Id))
            ),
        ]);

    // ---- Probar (PRB-001 to PRB-007) -----------------------------------------------------------------------------

    private StackPanel Test(TestModel model)
    {
        var close = Parts.Button(
            string.Empty,
            "close",
            ButtonVariant.Tertiary,
            _viewModel.CloseTest,
            model.CloseName
        );
        var head = new DockPanel { LastChildFill = true };
        DockPanel.SetDock(close, Dock.Right);
        head.Children.Add(close);
        head.Children.Add(
            new StyledText(model.Title, TextStyleToken.Heading)
            {
                VerticalAlignment = VerticalAlignment.Center,
            }
        );

        var sequence = new WrapPanel
        {
            Margin = new Thickness(Space.S3, Space.S3, Space.S1, Space.S1),
        };
        foreach (var item in model.Sequence)
        {
            if (item.Separator is { } separator)
            {
                var plus = new DecorativeText
                {
                    Text = separator,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 0, Space.S2, Space.S2),
                };
                StyledText.Apply(plus, TextStyleToken.Label);
                Ui.Ink(plus, TextBlock.ForegroundProperty, ColorToken.Muted);
                sequence.Children.Add(plus);
            }

            var label = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center,
            };
            if (item.Icon is { } symbol)
            {
                var glyph = Ui.Icon(
                    symbol,
                    IconBox.Small,
                    item.Lit ? ColorToken.OnAccent : ColorToken.Text
                );
                glyph.Margin = new Thickness(0, 0, Space.S1, 0);
                label.Children.Add(glyph);
            }

            // The keys of the sequence are keys: JetBrains Mono, the only use of it.
            var key = new StyledText(
                item.Label,
                TextStyleToken.Label,
                item.Lit ? ColorToken.OnAccent : ColorToken.Text
            )
            {
                VerticalAlignment = VerticalAlignment.Center,
                FontWeight = AppFonts.MonoWeight,
            };
            key.SetResourceReference(TextBlock.FontFamilyProperty, ThemeKeys.MonoFont);
            label.Children.Add(key);
            var chip = Ui.Card(
                label,
                item.Lit ? ColorToken.Accent : ColorToken.Card,
                item.Lit ? ColorToken.Accent : ColorToken.Line,
                Radii.Small,
                new Thickness(Space.S3, 0, Space.S3, 0)
            );
            chip.Height = ControlHeight.Small;
            chip.Margin = new Thickness(0, 0, Space.S2, Space.S2);
            sequence.Children.Add(chip);
        }

        var shown = Ui.Card(
            sequence,
            ColorToken.Field,
            ColorToken.Line,
            Radii.Medium,
            new Thickness(0)
        );
        shown.MinHeight = ControlHeight.Large;
        var play = Parts.Button(model.PlayText, "replay", ButtonVariant.Secondary, _viewModel.Play);
        play.VerticalAlignment = VerticalAlignment.Top;
        var what = Parts.Note(model.What, ColorToken.Text);
        what.Margin = new Thickness(Space.S3, 0, 0, 0);
        what.VerticalAlignment = VerticalAlignment.Center;
        var line = new DockPanel { LastChildFill = true };
        DockPanel.SetDock(play, Dock.Left);
        line.Children.Add(play);
        line.Children.Add(what);
        var body = Parts.Section(null, head, shown, line);
        if (model.Phase is { } phase)
        {
            var phaseLine = Parts.IconLine(
                model.PhaseIcon,
                phase,
                ColorToken.AccentInk,
                ColorToken.Text,
                TextStyleToken.Label
            );
            AutomationProperties.SetLiveSetting(phaseLine, AutomationLiveSetting.Polite);
            body.Children.Add(Spaced(phaseLine, Space.S3));
        }

        var targets = Parts.Section(model.TargetsLabel);
        if (model.NoApps is { } noApps)
        {
            targets.Children.Add(Spaced(Parts.Note(noApps), Space.S2));
        }
        else
        {
            var chips = new WrapPanel { Margin = new Thickness(0, Space.S2, -Space.S2, -Space.S2) };
            foreach (var target in model.Targets)
            {
                var chip = new OptionButton
                {
                    Kind = OptionKind.Chip,
                    Content = target.Name,
                    Symbol = "apps",
                    IsSelected = target.Selected,
                    Margin = new Thickness(0, 0, Space.S2, Space.S2),
                };
                AutomationProperties.SetName(chip, target.Name);
                chip.Click += (_, _) => _viewModel.ChooseTarget(target.Process);
                chips.Children.Add(chip);
            }

            targets.Children.Add(chips);
        }

        body.Children.Add(Spaced(targets, Space.S6));
        if (model.PausedText is { } pausedText)
        {
            // BUR-004: paused, «Probar ahora» would send nothing; the card says why and its button resumes.
            var paused = Parts.IconLine(
                "pause",
                pausedText,
                ColorToken.WarnText,
                ColorToken.Text,
                TextStyleToken.Label
            );
            AutomationProperties.SetLiveSetting(paused, AutomationLiveSetting.Polite);
            body.Children.Add(Spaced(paused, Space.S4));
        }

        var live = Parts.Button(
            model.LiveText,
            model.PausedText is not null ? "play_arrow"
                : model.LiveArmed ? "verified"
                : "send",
            ButtonVariant.Primary,
            _viewModel.TryLive
        );
        if (model.LiveArmed)
        {
            AutomationProperties.SetLiveSetting(live, AutomationLiveSetting.Polite);
        }

        live.IsEnabled = model.CanLive;
        body.Children.Add(Spaced(live, Space.S4));
        body.Children.Add(Spaced(Parts.Note(model.How, style: TextStyleToken.Caption), Space.S2));
        if (model.Question is { } question)
        {
            var yes = Parts.Button(
                model.YesText,
                null,
                ButtonVariant.Primary,
                () => _viewModel.Answer(true)
            );
            var no = Parts.Button(
                model.NoText,
                null,
                ButtonVariant.Secondary,
                () => _viewModel.Answer(false)
            );
            var ask = new Card
            {
                Content = Parts.Section(
                    null,
                    new StyledText(question, TextStyleToken.Label) { MaxLines = 0 },
                    Ui.Columns(2, Space.S2, [yes, no])
                ),
            };
            AutomationProperties.SetLiveSetting(ask, AutomationLiveSetting.Polite);
            body.Children.Add(Spaced(ask, Space.S4));
        }

        if (model.Answer is { } answer)
        {
            var ok = Parts.IconLine(
                "check_circle",
                answer,
                ColorToken.Success,
                ColorToken.Text,
                TextStyleToken.Label
            );
            AutomationProperties.SetLiveSetting(ok, AutomationLiveSetting.Polite);
            body.Children.Add(Spaced(ok, Space.S4));
        }

        if (model.TipsTitle is { } tips)
        {
            var list = Parts.Section(null, new StyledText(tips, TextStyleToken.Label));
            foreach (var tip in model.Tips)
            {
                list.Children.Add(
                    Spaced(
                        Parts.IconLine("lightbulb", tip, ColorToken.WarnText, ColorToken.Text),
                        Space.S2
                    )
                );
            }

            body.Children.Add(Spaced(new Card { Content = list, Tone = CardTone.Warn }, Space.S4));
        }

        return body;
    }

    private static FrameworkElement Spaced(FrameworkElement element, double above)
    {
        element.Margin = new Thickness(0, above, 0, 0);
        return element;
    }

    // ---- El pie fijo -------------------------------------------------------------------------------------------

    /// <summary>
    /// Probar (primary: it opens and closes the test layer), Duplicar (secondary) and Eliminar (danger, two taps,
    /// REG-04): one row that shares its width, so no text is cut while another button has room to spare.
    /// </summary>
    private ShareRow Footer(FooterModel model)
    {
        var test = new TouchButton
        {
            Content = model.TestText,
            Symbol = "play_arrow",
            Variant = ButtonVariant.Primary,
            IsExpanded = model.TestOpen,
            Padding = new Thickness(Space.S2, 0, Space.S2, 0),
        };
        AutomationProperties.SetName(test, model.TestText);
        test.Click += (_, _) => _viewModel.ToggleTest();
        var duplicate = new TouchButton
        {
            Content = model.DuplicateText,
            Symbol = "content_copy",
            Variant = ButtonVariant.Secondary,
            IsEnabled = model.Saved,
            Padding = new Thickness(Space.S2, 0, Space.S2, 0),
        };
        AutomationProperties.SetName(duplicate, model.DuplicateText);
        duplicate.Click += (_, _) => _viewModel.Duplicate();
        var delete = new TouchButton
        {
            Content = model.DeleteText,
            Symbol = model.DeleteArmed ? "warning" : "delete",
            Variant = ButtonVariant.Danger,
            IsEnabled = model.Saved,
            Padding = new Thickness(Space.S2, 0, Space.S2, 0),
        };
        AutomationProperties.SetName(delete, model.DeleteText);
        delete.Click += (_, _) => _viewModel.Delete();
        var row = new ShareRow { Gap = Space.S2, Fill = true };
        row.Children.Add(test);
        row.Children.Add(duplicate);
        row.Children.Add(delete);
        return row;
    }
}
