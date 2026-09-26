using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using Clicalo.Tools.SpikeLab.Session;
using Clicalo.Tools.SpikeLab.Tiles;
using Clicalo.UI.Wpf.Automation;

namespace Clicalo.Tools.SpikeLab.Views;

/// <summary>
/// The content of the guide strip: the current step of the script in large type, the big buttons «Funcionó», «Falló»,
/// «Repetir» and «Siguiente» (real tiles: at least 44 px and named for voice), the extra action of the step, «Soltar
/// todo ya», the automatic measurements, and green or red with the state also in words. Its notice line is the live
/// region of <see cref="LiveAnnouncer"/>.
/// </summary>
internal sealed class GuideView : Border
{
    private static readonly Brush Green = Frozen(Color.FromRgb(0x1B, 0x5E, 0x20));
    private static readonly Brush Red = Frozen(Color.FromRgb(0xB7, 0x1C, 0x1C));

    private readonly TextBlock _heading = Text(16, FontWeights.SemiBold);
    private readonly TextBlock _status = Text(18, FontWeights.Bold);
    private readonly Border _statusChip = new()
    {
        Padding = new Thickness(10, 4, 10, 4),
        CornerRadius = new CornerRadius(6),
    };
    private readonly TextBlock _title = Text(22, FontWeights.Bold);
    private readonly TextBlock _instruction = Text(24, FontWeights.Normal);
    private readonly TextBlock _progress = Text(18, FontWeights.SemiBold);
    private readonly StackPanel _measurements = new() { Margin = new Thickness(0, 6, 0, 6) };
    private readonly ShortcutTile _stepAction;

    /// <summary>Builds the view; <paramref name="addTile"/> creates each tile and wires its input.</summary>
    public GuideView(Func<LabTile, double, double, ShortcutTile> addTile)
    {
        ArgumentNullException.ThrowIfNull(addTile);
        Width = 980;
        Padding = new Thickness(14, 10, 14, 10);
        BorderThickness = new Thickness(6);
        SetResourceReference(BackgroundProperty, SystemColors.WindowBrushKey);
        TextElement.SetFontSize(this, 16);

        _statusChip.Child = _status;
        _status.Foreground = Brushes.White;
        AutomationProperties.SetName(_statusChip, "Estado del ciclo");
        var top = new DockPanel { LastChildFill = true };
        DockPanel.SetDock(_statusChip, Dock.Right);
        top.Children.Add(_statusChip);
        top.Children.Add(_heading);

        Notice = Text(17, FontWeights.Normal);
        Notice.FontStyle = FontStyles.Italic;
        AutomationProperties.SetName(Notice, "Aviso");

        var buttons = new WrapPanel { Margin = new Thickness(0, 4, 0, 0) };
        foreach (var tile in LabTiles.Guide.Take(4))
        {
            buttons.Children.Add(Large(addTile(tile, 160, 72)));
        }

        _stepAction = Large(addTile(LabTiles.GuideStepAction, 250, 72));
        buttons.Children.Add(_stepAction);
        buttons.Children.Add(Large(addTile(LabTiles.Guide[4], 160, 72)));

        var layout = new StackPanel();
        layout.Children.Add(top);
        layout.Children.Add(_title);
        layout.Children.Add(_instruction);
        layout.Children.Add(_progress);
        layout.Children.Add(_measurements);
        layout.Children.Add(Notice);
        layout.Children.Add(buttons);
        Child = layout;
    }

    /// <summary>The notice line: the live region of the laboratory's <see cref="LiveAnnouncer"/>.</summary>
    public TextBlock Notice { get; }

    /// <summary>Shows <paramref name="model"/>.</summary>
    public void Update(GuideModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        _heading.Text = model.Heading;
        _status.Text = model.StatusWord;
        _statusChip.Background = model.IsGreen ? Green : Red;
        BorderBrush = model.IsGreen ? Green : Red;
        _title.Text = model.Title;
        _instruction.Text = model.Instruction;
        _progress.Text = model.Progress;

        while (_measurements.Children.Count > model.Measurements.Length)
        {
            _measurements.Children.RemoveAt(_measurements.Children.Count - 1);
        }

        for (var i = 0; i < model.Measurements.Length; i++)
        {
            if (i >= _measurements.Children.Count)
            {
                _measurements.Children.Add(Text(16, FontWeights.Normal));
            }

            ((TextBlock)_measurements.Children[i]).Text = model.Measurements[i];
        }

        if (
            model.Notice is { } notice
            && !string.Equals(Notice.Text, notice, StringComparison.Ordinal)
        )
        {
            Notice.Text = notice;
        }

        if (model.StepActionName is { } action)
        {
            _stepAction.AccessibleName = action;
            _stepAction.Visibility = Visibility.Visible;
        }
        else
        {
            _stepAction.Visibility = Visibility.Collapsed;
        }
    }

    private static ShortcutTile Large(ShortcutTile tile)
    {
        tile.FontSize = 19;
        return tile;
    }

    private static TextBlock Text(double size, FontWeight weight) =>
        new()
        {
            FontSize = size,
            FontWeight = weight,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 2, 0, 2),
        };

    private static SolidColorBrush Frozen(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }
}
