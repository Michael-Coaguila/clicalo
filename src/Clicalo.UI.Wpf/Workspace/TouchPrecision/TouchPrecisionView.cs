using System.ComponentModel;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using Clicalo.Domain.Geometry;
using Clicalo.Domain.Touch;
using Clicalo.Presentation.ControlCenter.TouchPrecision;
using Clicalo.UI.Wpf.Controls;
using Clicalo.UI.Wpf.Theming;
using Clicalo.UI.Wpf.Theming.Generated;
using Clicalo.UI.Wpf.Workspace.General;
using Clicalo.UI.Wpf.Workspace.Internal;

namespace Clicalo.UI.Wpf.Workspace.TouchPrecision;

/// <summary>
/// «Precisión táctil» (docs/05 §4, TAC-005, TAC-006) in direction C (D12): on the left the four presets (option tiles
/// with their icon, name and description) and the four values, each one in the same outlined frame with its title, its
/// value, its slider with − / + of 44 and its explanation; on the right, in a column of 340 on the <c>side</c>
/// background (stacked below when the window is narrower than 1240), the test zone with two «Toca aquí» targets
/// separated by a gap, the counters and the last touch. Every contact of the zone goes
/// to <see cref="TouchPrecisionViewModel.TestContact"/>, which judges it with the panel's own recognizer and filter.
/// </summary>
/// <remarks>
/// The Control Center is a normal window whose touch arrives as promoted mouse input (WPF's touch stack is off), so the
/// zone follows the mouse with capture and hands the positions over in physical screen pixels, with the targets'
/// bounds taken at every contact (the zone may have scrolled). A capture lost to the scroll area is a cancelled contact.
/// </remarks>
public sealed class TouchPrecisionView : ContentControl
{
    /// <summary>The window width from which the test zone is a column beside the values (TAC-006).</summary>
    private const double WideFrom = 1240;

    private const double ZoneWidth = 340;

    /// <summary>The least width of a preset with its icon, its name in one line and its description in two.</summary>
    private const double PresetWidth = (ControlHeight.Large * 2) + Space.S8 + Space.S2;
    private const uint MousePointer = 1;

    private readonly TouchPrecisionViewModel _viewModel;
    private readonly StyledText _title;
    private readonly StyledText _subtitle;
    private readonly ContentControl _presets = new() { Focusable = false };
    private readonly SettingSlider[] _sliders;
    private readonly StackPanel _left;
    private readonly Border _root = new();
    private readonly Border _right = new() { Padding = new Thickness(Space.S6) };
    private readonly Overline _caption = new();

    // TAC-006: at least 200 high.
    private readonly Grid _zone = new()
    {
        MinHeight = (ControlHeight.Large * 3) + Space.S2,
        Background = Brushes.Transparent,
    };
    private readonly Rectangle[] _targetFaces = new Rectangle[TouchTestZone.TargetCount];
    private readonly Grid[] _targets = new Grid[TouchTestZone.TargetCount];
    private readonly StyledText[] _targetLabels = new StyledText[TouchTestZone.TargetCount];
    private readonly StyledText _registered = SettingsUi.Styled(string.Empty, TextStyleToken.Title);
    private readonly StyledText _registeredLabel = SettingsUi.Styled(
        string.Empty,
        TextStyleToken.Caption,
        ColorToken.Muted
    );
    private readonly StyledText _ignored = SettingsUi.Styled(string.Empty, TextStyleToken.Title);
    private readonly StyledText _ignoredLabel = SettingsUi.Styled(
        string.Empty,
        TextStyleToken.Caption,
        ColorToken.Muted
    );
    private readonly StyledText _message = SettingsUi.Styled(
        string.Empty,
        TextStyleToken.BodySmall,
        ColorToken.Muted,
        lines: 2
    );
    private readonly ContentControl _reset = new() { Focusable = false };
    private TouchScreen? _shown;
    private bool? _narrow;
    private bool _pressed;

    /// <summary>Creates the view of <paramref name="viewModel"/>.</summary>
    /// <param name="viewModel">The section.</param>
    public TouchPrecisionView(TouchPrecisionViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        _viewModel = viewModel;
        Focusable = false;
        (_title, _subtitle) = SettingsUi.NewHeader();
        Ui.Ink(_root, Border.BackgroundProperty, ColorToken.Win);
        Content = _root;
        _sliders =
        [
            .. Enum.GetValues<TouchValue>()
                .Select(value => new SettingSlider(
                    amount => _viewModel.SetValue(value, amount),
                    withDescription: true
                )),
        ];
        _left = Ui.Column(
            Space.S6,
            SettingsUi.PageHeader(_title, _subtitle),
            _presets,
            Ui.Column(
                Space.S4,
                [.. _sliders.Select(slider => SettingsUi.Frame(slider.Element, string.Empty))]
            )
        );
        _left.Margin = new Thickness(Space.S6);

        for (var i = 0; i < TouchTestZone.TargetCount; i++)
        {
            _zone.ColumnDefinitions.Add(
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }
            );
            _targets[i] = Target(i);
            Grid.SetColumn(_targets[i], i);
            _zone.Children.Add(_targets[i]);
        }

        _zone.MouseLeftButtonDown += OnDown;
        _zone.MouseMove += OnMove;
        _zone.MouseLeftButtonUp += OnUp;
        _zone.LostMouseCapture += OnLostCapture;
        AutomationProperties.SetLiveSetting(_message, AutomationLiveSetting.Polite);
        // Two lines are kept for the message: the counters and the button never move under the finger.
        _message.MinHeight = TextStyles.Get(TextStyleToken.BodySmall).LineHeight * 2;
        // The counters share the columns of the targets above them (the same gap of 24), so their edges line up.
        var counters = new Grid();
        counters.ColumnDefinitions.Add(
            new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }
        );
        counters.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(Space.S6) });
        counters.ColumnDefinitions.Add(
            new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }
        );
        var registered = Counter(_registered, _registeredLabel);
        var ignored = Counter(_ignored, _ignoredLabel);
        Grid.SetColumn(ignored, 2);
        counters.Children.Add(registered);
        counters.Children.Add(ignored);
        var column = new DockPanel { LastChildFill = true };
        foreach (var part in new UIElement[] { _caption, counters, _message, _reset })
        {
            if (part is FrameworkElement element)
            {
                element.Margin = new Thickness(
                    0,
                    ReferenceEquals(part, _caption) ? 0 : Space.S4,
                    0,
                    0
                );
            }

            DockPanel.SetDock(part, ReferenceEquals(part, _caption) ? Dock.Top : Dock.Bottom);
        }

        column.Children.Add(_caption);
        column.Children.Add(_reset);
        column.Children.Add(_message);
        column.Children.Add(counters);
        _zone.Margin = new Thickness(0, Space.S4, 0, 0);
        column.Children.Add(_zone);
        _right.Child = column;
        Ui.Ink(_right, Border.BackgroundProperty, ColorToken.Side);
        Ui.Ink(_right, Border.BorderBrushProperty, ColorToken.Border);

        viewModel.PropertyChanged += OnChanged;
        SizeChanged += (_, _) => ApplyWidth();
        Arrange(narrow: false);
        Render();
    }

    /// <summary>Stops following the view model (the window is closing for good).</summary>
    public void Detach() => _viewModel.PropertyChanged -= OnChanged;

    /// <summary>A counter of the zone: the number in <c>title</c> over its name, on a card with its line.</summary>
    private static Border Counter(StyledText value, StyledText label)
    {
        var counter = Ui.Card(
            Ui.Column(0, value, label),
            ColorToken.Card,
            ColorToken.Border,
            Radii.Medium,
            new Thickness(Space.S3)
        );
        counter.SetResourceReference(Border.BorderThicknessProperty, ThemeScope.BorderThicknessKey);
        return counter;
    }

    private Grid Target(int index)
    {
        // A solid control to touch, with the line of every control: the dashed line is only for the places to add
        // something (D19, sistema §3.3), and a target is not one.
        var face = new Rectangle { RadiusX = Radii.Large, RadiusY = Radii.Large };
        face.SetResourceReference(Shape.StrokeThicknessProperty, ThemeKeys.LineThickness);
        Ui.Ink(face, Shape.StrokeProperty, ColorToken.Line);
        Ui.Ink(face, Shape.FillProperty, ColorToken.Card);
        var label = SettingsUi.Styled(string.Empty, TextStyleToken.Heading, lines: 2);
        label.TextAlignment = TextAlignment.Center;
        var icon = Ui.Icon("touch_app", IconBox.Large, ColorToken.AccentInk);
        var content = Ui.Column(Space.S2, icon, label);
        content.VerticalAlignment = VerticalAlignment.Center;
        content.HorizontalAlignment = HorizontalAlignment.Center;
        content.Margin = new Thickness(Space.S2);
        content.IsHitTestVisible = false;
        var target = new Grid
        {
            // The gap between the targets checks the extra area too (TAC-006).
            Margin = new Thickness(
                index == 0 ? 0 : Space.S6 / 2,
                0,
                index == TouchTestZone.TargetCount - 1 ? 0 : Space.S6 / 2,
                0
            ),
        };
        target.Children.Add(face);
        target.Children.Add(content);
        _targetFaces[index] = face;
        _targetLabels[index] = label;
        return target;
    }

    private void OnChanged(object? sender, PropertyChangedEventArgs e) => Render();

    private void ApplyWidth()
    {
        if (ActualWidth <= 0)
        {
            return;
        }

        var narrow = SettingsUi.WindowWidth(this) < WideFrom;
        if (narrow != _narrow)
        {
            Arrange(narrow);
        }
    }

    /// <summary>The zone is a column of 340 on the right, or below the sliders in a narrow window (TAC-006).</summary>
    private void Arrange(bool narrow)
    {
        _narrow = narrow;
        Release(_left);
        Release(_right);
        if (narrow)
        {
            _right.BorderThickness = new Thickness(0, Stroke.Line, 0, 0);
            _right.Margin = new Thickness(0);
            _root.Child = new ThemedScrollViewer { Content = Ui.Column(0, _left, _right) };
            return;
        }

        _right.BorderThickness = new Thickness(Stroke.Line, 0, 0, 0);
        var layout = new Grid();
        layout.ColumnDefinitions.Add(
            new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }
        );
        layout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(ZoneWidth) });
        var scroll = new ThemedScrollViewer { Content = _left };
        layout.Children.Add(scroll);
        Grid.SetColumn(_right, 1);
        layout.Children.Add(_right);
        _root.Child = layout;
    }

    private static void Release(FrameworkElement element)
    {
        switch (element.Parent)
        {
            case Panel panel:
                panel.Children.Remove(element);
                break;
            case ContentControl content:
                content.Content = null;
                break;
            case Decorator decorator:
                decorator.Child = null;
                break;
        }
    }

    private void Render()
    {
        var screen = _viewModel.Screen;
        var shown = _shown;
        _shown = screen;
        _title.Text = screen.Title;
        _subtitle.Text = screen.Subtitle;
        if (shown?.Presets != screen.Presets)
        {
            _presets.Content = Presets(screen);
        }

        for (var i = 0; i < _sliders.Length && i < screen.Sliders.Count; i++)
        {
            var slider = screen.Sliders[i];
            _sliders[i]
                .Show(
                    slider.Label,
                    slider.Display,
                    slider.Current,
                    slider.Minimum,
                    slider.Maximum,
                    slider.Step,
                    slider.Step,
                    slider.LessName,
                    slider.MoreName,
                    slider.Description
                );
        }

        var test = screen.Test;
        _caption.Text = test.Caption;
        for (var i = 0; i < _targets.Length; i++)
        {
            var face = _targetFaces[i];
            _targetLabels[i].Text = test.TapHere;
            var mark = i < test.Marks.Count ? test.Marks[i] : TestMark.None;
            Ui.Ink(
                face,
                Shape.FillProperty,
                mark switch
                {
                    TestMark.Registered => ColorToken.AccentWash,
                    TestMark.Ignored => ColorToken.WarnWash,
                    _ => ColorToken.Card,
                }
            );
            AutomationProperties.SetName(_targets[i], test.TapHere);
        }

        _registered.Text = test.Registered.ToString(
            System.Globalization.CultureInfo.CurrentCulture
        );
        _registeredLabel.Text = test.RegisteredLabel;
        _ignored.Text = test.Ignored.ToString(System.Globalization.CultureInfo.CurrentCulture);
        _ignoredLabel.Text = test.IgnoredLabel;
        _message.Text = test.Message;
        _message.Ink = test.IsWarning ? ColorToken.WarnText : ColorToken.Muted;
        if (!string.Equals(shown?.Test.ResetLabel, test.ResetLabel, StringComparison.Ordinal))
        {
            var reset = SettingsUi.Button(
                test.ResetLabel,
                ButtonVariant.Secondary,
                _viewModel.ResetCounters,
                "restart_alt"
            );
            reset.HorizontalAlignment = HorizontalAlignment.Left;
            _reset.Content = reset;
        }
    }

    /// <summary>
    /// The four presets (TAC-005): option tiles with their icon, name and description, as many in a row as fit with
    /// their whole names; the one in use has the single look of «elegido», with its icon filled (not only color).
    /// </summary>
    private UniformGrid Presets(TouchScreen screen)
    {
        // Four in a row where each keeps its whole name, two by two otherwise: never three and one alone.
        var grid = new UniformGrid { Columns = 4, Margin = new Thickness(-Space.S1) };
        var descriptions = new List<TextBlock>();
        grid.SizeChanged += (_, e) =>
        {
            grid.Columns = e.NewSize.Width >= PresetWidth * 4 ? 4 : 2;
            _ = Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () => FitLines(descriptions));
        };
        foreach (var preset in screen.Presets)
        {
            // Every description keeps the lines of the longest one, so the names of the four presets share one
            // baseline; two only when one of them wraps, so cards of one line are not left with an empty band.
            var description = SettingsUi.Styled(
                preset.Description,
                TextStyleToken.Caption,
                ColorToken.Muted,
                lines: 2
            );
            description.MinHeight = TextStyles.Caption.LineHeight;
            description.VerticalAlignment = VerticalAlignment.Top;
            descriptions.Add(description);
            var texts = Ui.Column(
                0,
                SettingsUi.Styled(preset.Label, TextStyleToken.Label),
                description
            );

            // The icon goes with the name and the first line of the description, as in a list row, not with the
            // middle of the two lines kept for it: chosen, it is filled in AccentInk (the single look of «elegido»).
            var icon = Ui.Icon(
                PresetIcon(preset.Id),
                IconBox.Small,
                preset.Selected ? ColorToken.AccentInk : null
            );
            icon.IsFilled = preset.Selected;
            icon.VerticalAlignment = VerticalAlignment.Center;
            var iconSlot = new Border
            {
                Child = icon,
                Height = TextStyles.Label.LineHeight + TextStyles.Caption.LineHeight,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 0, Space.S2, 0),
            };
            var content = new DockPanel { LastChildFill = true };
            DockPanel.SetDock(iconSlot, Dock.Left);
            content.Children.Add(iconSlot);
            content.Children.Add(texts);
            var card = SettingsUi.Tall(
                SettingsUi.Option(
                    OptionKind.Tile,
                    content,
                    preset.Label,
                    preset.Selected,
                    () => _viewModel.ChoosePreset(preset.Id)
                )
            );
            card.HorizontalContentAlignment = HorizontalAlignment.Left;
            card.Margin = new Thickness(Space.S1);
            AutomationProperties.SetHelpText(card, preset.Description);
            grid.Children.Add(card);
        }

        return grid;
    }

    /// <summary>
    /// Gives <paramref name="texts"/> the lines of the longest of them: two when one does not fit its width in one
    /// line, one otherwise.
    /// </summary>
    private static void FitLines(List<TextBlock> texts)
    {
        var wraps = texts.Exists(static text =>
        {
            if (text.ActualWidth <= 0)
            {
                return false;
            }

            var line = new FormattedText(
                text.Text,
                System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface(text.FontFamily, text.FontStyle, text.FontWeight, text.FontStretch),
                text.FontSize,
                text.Foreground,
                VisualTreeHelper.GetDpi(text).PixelsPerDip
            );
            return line.WidthIncludingTrailingWhitespace > text.ActualWidth;
        });
        foreach (var text in texts)
        {
            text.MinHeight = wraps
                ? TextStyles.Caption.LineHeight * 2
                : TextStyles.Caption.LineHeight;
        }
    }

    private static string PresetIcon(string id) =>
        id switch
        {
            "standard" => "touch_app",
            "mild-tremor" => "pan_tool_alt",
            "strong-tremor" => "front_hand",
            _ => "tune",
        };

    // ---- The test zone (TAC-006) ---------------------------------------------------------------------------------

    /// <summary>
    /// The device of a mouse event of the zone: a touch or a pen promoted to the mouse keeps its kind, so the zone judges
    /// it as the panel does (the minimum contact is for fingers and pens, TAC-002).
    /// </summary>
    private static PointerKind KindOf(MouseEventArgs e) =>
        e.StylusDevice is { } stylus
            ? stylus.TabletDevice?.Type == TabletDeviceType.Touch
                ? PointerKind.Finger
                : PointerKind.Pen
            : PointerKind.Mouse;

    private void OnDown(object sender, MouseButtonEventArgs e)
    {
        PlaceTargets();
        _pressed = true;
        _ = _zone.CaptureMouse();
        _viewModel.TestContact(MousePointer, PointerPhase.Down, ScreenPoint(e), KindOf(e));
        e.Handled = true;
    }

    private void OnMove(object sender, MouseEventArgs e)
    {
        if (_pressed && e.LeftButton == MouseButtonState.Pressed)
        {
            _viewModel.TestContact(MousePointer, PointerPhase.Move, ScreenPoint(e), KindOf(e));
        }
    }

    private void OnUp(object sender, MouseButtonEventArgs e)
    {
        if (!_pressed)
        {
            return;
        }

        _pressed = false;
        _viewModel.TestContact(MousePointer, PointerPhase.Up, ScreenPoint(e), KindOf(e));
        _zone.ReleaseMouseCapture();
        e.Handled = true;
    }

    private void OnLostCapture(object sender, MouseEventArgs e)
    {
        if (!_pressed)
        {
            return;
        }

        // The scroll area took the gesture, or the window lost the mouse: the contact is cancelled (TAC-004).
        _pressed = false;
        _viewModel.TestContact(MousePointer, PointerPhase.Cancel, ScreenPoint(e));
    }

    private PhysicalPoint ScreenPoint(MouseEventArgs e)
    {
        var point = _zone.PointToScreen(e.GetPosition(_zone));
        return new PhysicalPoint((int)Math.Round(point.X), (int)Math.Round(point.Y));
    }

    private void PlaceTargets()
    {
        var bounds = new List<PhysicalRect>(_targets.Length);
        foreach (var target in _targets)
        {
            var topLeft = target.PointToScreen(new Point(0, 0));
            var bottomRight = target.PointToScreen(
                new Point(target.ActualWidth, target.ActualHeight)
            );
            bounds.Add(
                new PhysicalRect(
                    (int)Math.Round(topLeft.X),
                    (int)Math.Round(topLeft.Y),
                    (int)Math.Round(bottomRight.X - topLeft.X),
                    (int)Math.Round(bottomRight.Y - topLeft.Y)
                )
            );
        }

        _viewModel.PlaceTargets(bounds, VisualTreeHelper.GetDpi(_zone).DpiScaleX);
    }
}
