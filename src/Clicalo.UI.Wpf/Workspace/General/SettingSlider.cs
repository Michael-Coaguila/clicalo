using System.Windows.Automation;
using System.Windows.Controls;
using Clicalo.UI.Wpf.Controls;
using Clicalo.UI.Wpf.Theming.Generated;
using Clicalo.UI.Wpf.Workspace.Internal;

namespace Clicalo.UI.Wpf.Workspace.General;

/// <summary>
/// A slider row of «General y panel» and «Precisión táctil» (GEN-009, TAC-005): the name, the value in accent
/// monospace, the <see cref="StepSlider"/> with its − / + and, optionally, the explanation below. It lives across the
/// rebuilds of its section, so a finger dragging the knob never loses it; the section only updates its texts and value.
/// </summary>
internal sealed class SettingSlider
{
    private readonly TextBlock _title = Ui.Text(string.Empty, 15, bold: true);
    private readonly TextBlock _value = Ui.Text(
        string.Empty,
        14,
        ink: ColorToken.Accent,
        mono: true
    );
    private readonly TextBlock _description = Ui.Text(
        string.Empty,
        13,
        ink: ColorToken.Muted,
        wrap: true
    );
    private readonly Action<double> _moved;
    private bool _applying;

    /// <summary>Creates the row.</summary>
    /// <param name="moved">Receives the value the person chose (drag, track, − / +, keyboard or UI Automation).</param>
    /// <param name="withDescription">Whether the explanation shows under the slider.</param>
    public SettingSlider(Action<double> moved, bool withDescription)
    {
        _moved = moved;
        Slider = new StepSlider { IsSnapToTickEnabled = true };
        Slider.ValueChanged += (_, change) =>
        {
            if (!_applying)
            {
                _moved(change.NewValue);
            }
        };
        var heading = new DockPanel { LastChildFill = true };
        DockPanel.SetDock(_value, Dock.Right);
        heading.Children.Add(_value);
        heading.Children.Add(_title);
        Element = Ui.Column(6, heading, Slider, withDescription ? _description : null);
    }

    /// <summary>The slider.</summary>
    public StepSlider Slider { get; }

    /// <summary>The whole row, to place in a card.</summary>
    public StackPanel Element { get; }

    /// <summary>Shows a value without reporting it back.</summary>
    /// <param name="title">The name of the slider.</param>
    /// <param name="value">The value as text.</param>
    /// <param name="current">The value.</param>
    /// <param name="minimum">The lowest value.</param>
    /// <param name="maximum">The highest value.</param>
    /// <param name="tick">The grid of the track.</param>
    /// <param name="buttonStep">How far − and + move.</param>
    /// <param name="lessName">The accessible name of −.</param>
    /// <param name="moreName">The accessible name of +.</param>
    /// <param name="description">The explanation; empty for none.</param>
    public void Show(
        string title,
        string value,
        double current,
        double minimum,
        double maximum,
        double tick,
        double buttonStep,
        string lessName,
        string moreName,
        string description = ""
    )
    {
        _applying = true;
        try
        {
            _title.Text = title;
            _value.Text = value;
            _description.Text = description;
            Slider.Minimum = minimum;
            Slider.Maximum = maximum;
            Slider.TickFrequency = tick;
            Slider.LargeChange = tick;
            Slider.SmallChange = buttonStep;
            Slider.DecreaseName = lessName;
            Slider.IncreaseName = moreName;
            AutomationProperties.SetName(Slider, title);
            AutomationProperties.SetHelpText(Slider, description);
            if (!Slider.Value.Equals(current))
            {
                Slider.Value = current;
            }
        }
        finally
        {
            _applying = false;
        }
    }

    /// <summary>Takes the row out of the card it was in, before the section places it in a new one.</summary>
    public StackPanel Detached()
    {
        switch (Element.Parent)
        {
            case Panel panel:
                panel.Children.Remove(Element);
                break;
            case Decorator decorator:
                decorator.Child = null;
                break;
            case ContentControl content:
                content.Content = null;
                break;
        }

        return Element;
    }
}
