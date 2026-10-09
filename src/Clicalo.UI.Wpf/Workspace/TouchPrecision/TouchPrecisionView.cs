using System.ComponentModel;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Clicalo.Domain.Geometry;
using Clicalo.Domain.Touch;
using Clicalo.Presentation.ControlCenter.TouchPrecision;
using Clicalo.UI.Wpf.Theming.Generated;
using Clicalo.UI.Wpf.Workspace.General;
using Clicalo.UI.Wpf.Workspace.Internal;

namespace Clicalo.UI.Wpf.Workspace.TouchPrecision;

/// <summary>
/// «Precisión táctil» (docs/05 §4, TAC-005, TAC-006): on the left the four presets and the four sliders; on the right,
/// in a column of 340 on the <c>side</c> background (stacked below when the window is narrower than 1240), the test
/// zone with two «Toca aquí» targets separated by a gap, the counters and the last touch. Every contact of the zone goes
/// to <see cref="TouchPrecisionViewModel.TestContact"/>, which judges it with the panel's own recognizer and filter.
/// </summary>
/// <remarks>
/// The Control Center is a normal window whose touch arrives as promoted mouse input (WPF's touch stack is off), so the
/// zone follows the mouse with capture and hands the positions over in physical screen pixels, with the targets'
/// bounds taken at every contact (the zone may have scrolled). A capture lost to the scroll area is a cancelled contact.
/// </remarks>
public sealed class TouchPrecisionView : ContentControl
{
    private const double NarrowBelow = 1240;
    private const double ZoneWidth = 340;
    private const double TargetGap = 24;
    private const uint MousePointer = 1;

    private readonly TouchPrecisionViewModel _viewModel;
    private readonly TextBlock _title = Ui.Text(string.Empty, 24, bold: true);
    private readonly TextBlock _subtitle = Ui.Text(
        string.Empty,
        14,
        ink: ColorToken.Muted,
        wrap: true
    );
    private readonly ContentControl _presets = new() { Focusable = false };
    private readonly SettingSlider[] _sliders;
    private readonly StackPanel _left;
    private readonly Border _root = new();
    private readonly Border _right = new() { Padding = new Thickness(20) };
    private readonly TextBlock _caption = Ui.Text(
        string.Empty,
        12,
        bold: true,
        ink: ColorToken.Muted
    );
    private readonly Grid _zone = new() { MinHeight = 200, Background = Brushes.Transparent };
    private readonly Rectangle[] _targetFaces = new Rectangle[TouchTestZone.TargetCount];
    private readonly Grid[] _targets = new Grid[TouchTestZone.TargetCount];
    private readonly TextBlock[] _targetLabels = new TextBlock[TouchTestZone.TargetCount];
    private readonly TextBlock _registered = Ui.Text(string.Empty, 28, bold: true);
    private readonly TextBlock _registeredLabel = Ui.Text(string.Empty, 12, ink: ColorToken.Muted);
    private readonly TextBlock _ignored = Ui.Text(string.Empty, 28, bold: true);
    private readonly TextBlock _ignoredLabel = Ui.Text(string.Empty, 12, ink: ColorToken.Muted);
    private readonly TextBlock _message = Ui.Text(
        string.Empty,
        13,
        ink: ColorToken.Muted,
        wrap: true
    );
    private readonly ContentControl _reset = new() { Focusable = false };
    private TouchScreen? _shown;
    private bool _narrow;
    private bool _pressed;

    /// <summary>Creates the view of <paramref name="viewModel"/>.</summary>
    /// <param name="viewModel">The section.</param>
    public TouchPrecisionView(TouchPrecisionViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        _viewModel = viewModel;
        Focusable = false;
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
            20,
            Ui.Column(4, _title, _subtitle),
            _presets,
            Ui.Column(20, [.. _sliders.Select(slider => slider.Element)])
        );
        _left.Margin = new Thickness(24);

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
        _message.MinHeight = 36;
        var counters = Ui.Columns(
            2,
            8,
            [Counter(_registered, _registeredLabel), Counter(_ignored, _ignoredLabel)]
        );
        var column = new DockPanel { LastChildFill = true };
        foreach (var part in new UIElement[] { _caption, counters, _message, _reset })
        {
            if (part is FrameworkElement element)
            {
                element.Margin = new Thickness(0, ReferenceEquals(part, _caption) ? 0 : 14, 0, 0);
            }

            DockPanel.SetDock(part, ReferenceEquals(part, _caption) ? Dock.Top : Dock.Bottom);
        }

        column.Children.Add(_caption);
        column.Children.Add(_reset);
        column.Children.Add(_message);
        column.Children.Add(counters);
        _zone.Margin = new Thickness(0, 14, 0, 0);
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

    private static Border Counter(TextBlock value, TextBlock label) =>
        Ui.Card(Ui.Column(0, value, label), ColorToken.Card, null, 10, new Thickness(12));

    private Grid Target(int index)
    {
        var face = new Rectangle
        {
            RadiusX = 16,
            RadiusY = 16,
            StrokeThickness = 2,
            StrokeDashArray = [4, 3],
        };
        Ui.Ink(face, Shape.StrokeProperty, ColorToken.Accent);
        Ui.Ink(face, Shape.FillProperty, ColorToken.Card);
        var label = Ui.Text(string.Empty, 20, bold: true, wrap: true);
        label.TextAlignment = TextAlignment.Center;
        var content = Ui.Column(8, Ui.Icon("touch_app", 40, ColorToken.Accent), label);
        content.VerticalAlignment = VerticalAlignment.Center;
        content.HorizontalAlignment = HorizontalAlignment.Center;
        content.Margin = new Thickness(8);
        content.IsHitTestVisible = false;
        var target = new Grid
        {
            Margin = new Thickness(
                index == 0 ? 0 : TargetGap / 2,
                0,
                index == TouchTestZone.TargetCount - 1 ? 0 : TargetGap / 2,
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
        var width = Window.GetWindow(this)?.ActualWidth ?? ActualWidth;
        var narrow = width > 0 && width < NarrowBelow;
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
            _right.BorderThickness = new Thickness(0, 1, 0, 0);
            _right.Margin = new Thickness(0);
            _root.Child = new TouchPanScrollViewer { Content = Ui.Column(0, _left, _right) };
            return;
        }

        _right.BorderThickness = new Thickness(1, 0, 0, 0);
        var layout = new Grid();
        layout.ColumnDefinitions.Add(
            new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }
        );
        layout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(ZoneWidth) });
        var scroll = new TouchPanScrollViewer { Content = _left };
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
        _caption.Text = test.Caption.ToUpperInvariant();
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
        Ui.Ink(
            _message,
            TextBlock.ForegroundProperty,
            test.IsWarning ? ColorToken.Text : ColorToken.Muted
        );
        if (!string.Equals(shown?.Test.ResetLabel, test.ResetLabel, StringComparison.Ordinal))
        {
            var reset = Ui.Button(
                Ui.IconLabel("restart_alt", test.ResetLabel, 18, 13),
                test.ResetLabel,
                _viewModel.ResetCounters,
                ColorToken.Card,
                stroke: ColorToken.Border
            );
            reset.HorizontalAlignment = HorizontalAlignment.Left;
            _reset.Content = reset;
        }
    }

    private UniformGrid Presets(TouchScreen screen)
    {
        var grid = new UniformGrid { Columns = 4, Margin = new Thickness(0, 0, -8, 0) };
        foreach (var preset in screen.Presets)
        {
            var card = Ui.Choice(
                Ui.Column(
                    4,
                    Ui.Text(preset.Label, 15, bold: true, wrap: true),
                    Ui.Text(preset.Description, 12, ink: ColorToken.Muted, wrap: true)
                ),
                preset.Label,
                preset.Selected,
                () => _viewModel.ChoosePreset(preset.Id),
                76,
                12
            );
            card.Height = double.NaN;
            card.MinHeight = 76;
            card.Padding = new Thickness(10);
            card.Margin = new Thickness(0, 0, 8, 0);
            card.HorizontalContentAlignment = HorizontalAlignment.Left;
            card.VerticalContentAlignment = VerticalAlignment.Top;
            AutomationProperties.SetHelpText(card, preset.Description);
            grid.Children.Add(card);
        }

        return grid;
    }

    // ---- The test zone (TAC-006) ---------------------------------------------------------------------------------

    private void OnDown(object sender, MouseButtonEventArgs e)
    {
        PlaceTargets();
        _pressed = true;
        _ = _zone.CaptureMouse();
        _viewModel.TestContact(MousePointer, PointerPhase.Down, ScreenPoint(e));
        e.Handled = true;
    }

    private void OnMove(object sender, MouseEventArgs e)
    {
        if (_pressed && e.LeftButton == MouseButtonState.Pressed)
        {
            _viewModel.TestContact(MousePointer, PointerPhase.Move, ScreenPoint(e));
        }
    }

    private void OnUp(object sender, MouseButtonEventArgs e)
    {
        if (!_pressed)
        {
            return;
        }

        _pressed = false;
        _viewModel.TestContact(MousePointer, PointerPhase.Up, ScreenPoint(e));
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
