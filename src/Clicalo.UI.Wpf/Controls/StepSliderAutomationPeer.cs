using System.Windows.Automation.Peers;
using Clicalo.UI.Wpf.Controls.Internal;

namespace Clicalo.UI.Wpf.Controls;

/// <summary>
/// UI Automation peer of <see cref="StepSlider"/> (ACC-001, ACC-005, REG-06): a Slider with the RangeValue pattern
/// whose only children are its − and + buttons (Invoke), so «clic Subir» works by voice; the track's repeat buttons
/// and the knob are not elements. UI Automation can focus it only while its window is active (REG-01).
/// </summary>
/// <param name="owner">The slider.</param>
public sealed class StepSliderAutomationPeer(StepSlider owner) : SliderAutomationPeer(owner)
{
    /// <inheritdoc />
    protected override string GetClassNameCore() => nameof(StepSlider);

    /// <summary>The − and + buttons, in this order.</summary>
    protected override List<AutomationPeer>? GetChildrenCore()
    {
        var slider = (StepSlider)Owner;
        var children = new List<AutomationPeer>(2);
        foreach (var button in new[] { slider.DecreaseButton, slider.IncreaseButton })
        {
            if (button is not null && CreatePeerForElement(button) is { } peer)
            {
                children.Add(peer);
            }
        }

        return children.Count == 0 ? null : children;
    }

    /// <inheritdoc />
    protected override bool IsKeyboardFocusableCore() => SurfaceFocus.CanFocus(Owner);

    /// <inheritdoc />
    protected override void SetFocusCore() => SurfaceFocus.Focus(Owner);
}
