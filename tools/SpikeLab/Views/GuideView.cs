using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using Clicalo.Tools.SpikeLab.Session;
using Clicalo.Tools.SpikeLab.Surfaces;
using Clicalo.Tools.SpikeLab.Tiles;
using Clicalo.UI.Wpf.Automation;

namespace Clicalo.Tools.SpikeLab.Views;

/// <summary>
/// The content of the guide strip, compact by default so it never hides what the script asks to watch: its handle
/// (<see cref="Grip"/>, to drag it with a finger), the step and the state in words, the instruction in three lines
/// («Ver instrucción completa» shows the rest), the measurements, the notice line (the live region of
/// <see cref="LiveAnnouncer"/>) and the buttons «Funcionó», «Falló», «Repetir», «Siguiente», the action of the step
/// and «Soltar todo ya». «Plegar la tira» leaves only the step, the state, the notice and the buttons; «Mover la tira»
/// sends it to the other half of the screen. Every button is a real tile of at least 44 px, named for voice.
/// </summary>
internal sealed class GuideView : Border
{
    /// <summary>Name of the fold button while the strip is folded.</summary>
    public const string UnfoldName = "Desplegar la tira";

    /// <summary>Name of the instruction button while the whole instruction is shown.</summary>
    public const string ShortenName = "Acortar la instrucción";

    /// <summary>Lines of the instruction in the compact strip.</summary>
    public const int CompactInstructionLines = 3;

    private const double InstructionLineHeight = 23;
    private const double TileFontSize = 14;
    private static readonly Brush Green = Frozen(Color.FromRgb(0x1B, 0x5E, 0x20));
    private static readonly Brush Red = Frozen(Color.FromRgb(0xB7, 0x1C, 0x1C));

    private readonly TextBlock _heading = Text(13, FontWeights.SemiBold);
    private readonly TextBlock _status = Text(14, FontWeights.Bold);
    private readonly Border _statusChip = new()
    {
        Padding = new Thickness(8, 2, 8, 2),
        CornerRadius = new CornerRadius(6),
        Margin = new Thickness(0, 2, 0, 2),
        HorizontalAlignment = HorizontalAlignment.Left,
    };
    private readonly TextBlock _title = Text(17, FontWeights.Bold);
    private readonly TextBlock _instruction = Text(18, FontWeights.Normal);
    private readonly TextBlock _progress = Text(14, FontWeights.SemiBold);
    private readonly StackPanel _measurements = new() { Margin = new Thickness(0, 2, 0, 2) };
    private readonly StackPanel _details = new();
    private readonly ShortcutTile _stepAction;
    private readonly ShortcutTile _fold;
    private readonly ShortcutTile _instructionButton;
    private bool _folded;
    private bool _fullInstruction;

    /// <summary>Builds the view; <paramref name="addTile"/> creates each tile and wires its input.</summary>
    public GuideView(Func<LabTile, double, double, ShortcutTile> addTile)
    {
        ArgumentNullException.ThrowIfNull(addTile);
        Width = LabLayout.StripPreferredWidth;
        Padding = new Thickness(10, 6, 10, 6);
        BorderThickness = new Thickness(6);
        SetResourceReference(BackgroundProperty, SystemColors.WindowBrushKey);
        TextElement.SetFontSize(this, 14);

        Grip = new DragGrip("Asa de la tira-guía", 48)
        {
            VerticalAlignment = VerticalAlignment.Top,
            Height = 56,
            Margin = new Thickness(0, 4, 8, 4),
        };

        _instructionButton = Small(addTile(LabTiles.GuideInstruction, 112, 48));
        _fold = Small(addTile(LabTiles.GuideFold, 112, 48));
        var viewButtons = new StackPanel { Orientation = Orientation.Horizontal };
        viewButtons.Children.Add(_instructionButton);
        viewButtons.Children.Add(_fold);
        viewButtons.Children.Add(Small(addTile(LabTiles.GuideMove, 112, 48)));

        _statusChip.Child = _status;
        _status.Foreground = Brushes.White;
        // The step and the state share a line while they fit; the title goes below.
        _heading.Margin = new Thickness(0, 3, 8, 1);
        _heading.VerticalAlignment = VerticalAlignment.Center;
        var stepAndState = new WrapPanel();
        stepAndState.Children.Add(_heading);
        stepAndState.Children.Add(_statusChip);
        var header = new StackPanel();
        header.Children.Add(stepAndState);
        header.Children.Add(_title);

        var top = new DockPanel { LastChildFill = true };
        DockPanel.SetDock(Grip, Dock.Left);
        DockPanel.SetDock(viewButtons, Dock.Right);
        top.Children.Add(Grip);
        top.Children.Add(viewButtons);
        top.Children.Add(header);

        _instruction.LineHeight = InstructionLineHeight;
        _instruction.LineStackingStrategy = LineStackingStrategy.BlockLineHeight;
        _instruction.TextTrimming = TextTrimming.WordEllipsis;
        _details.Children.Add(_instruction);
        _details.Children.Add(_progress);
        _details.Children.Add(_measurements);

        Notice = Text(14, FontWeights.Normal);
        Notice.FontStyle = FontStyles.Italic;
        AutomationProperties.SetName(Notice, "Aviso");

        var buttons = new WrapPanel { Margin = new Thickness(0, 2, 0, 0) };
        foreach (var tile in LabTiles.Guide.Take(4))
        {
            buttons.Children.Add(Small(addTile(tile, 104, 56)));
        }

        _stepAction = Small(addTile(LabTiles.GuideStepAction, 160, 56));
        buttons.Children.Add(_stepAction);
        buttons.Children.Add(Small(addTile(LabTiles.Guide[4], 104, 56)));

        var layout = new StackPanel();
        layout.Children.Add(top);
        layout.Children.Add(_details);
        layout.Children.Add(Notice);
        layout.Children.Add(buttons);
        Child = layout;
        ShowState();
    }

    /// <summary>The handle that drags the strip with a finger.</summary>
    public DragGrip Grip { get; }

    /// <summary>The notice line: the live region of the laboratory's <see cref="LiveAnnouncer"/>.</summary>
    public TextBlock Notice { get; }

    /// <summary>True while the strip is folded (only the step, the state, the notice and the buttons).</summary>
    public bool IsFolded
    {
        get => _folded;
        set
        {
            _folded = value;
            ShowState();
        }
    }

    /// <summary>True while the whole instruction is shown instead of its first lines.</summary>
    public bool ShowsFullInstruction
    {
        get => _fullInstruction;
        set
        {
            _fullInstruction = value;
            ShowState();
        }
    }

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
                _measurements.Children.Add(Text(13, FontWeights.Normal));
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

    private void ShowState()
    {
        _details.Visibility = _folded ? Visibility.Collapsed : Visibility.Visible;
        _instructionButton.Visibility = _folded ? Visibility.Collapsed : Visibility.Visible;
        _fold.AccessibleName = _folded ? UnfoldName : LabTiles.GuideFold.Name;
        _instructionButton.AccessibleName = _fullInstruction
            ? ShortenName
            : LabTiles.GuideInstruction.Name;
        _instruction.MaxHeight = _fullInstruction
            ? double.PositiveInfinity
            : CompactInstructionLines * InstructionLineHeight;
    }

    private static ShortcutTile Small(ShortcutTile tile)
    {
        tile.FontSize = TileFontSize;
        return tile;
    }

    private static TextBlock Text(double size, FontWeight weight) =>
        new()
        {
            FontSize = size,
            FontWeight = weight,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 1, 0, 1),
        };

    private static SolidColorBrush Frozen(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }
}
