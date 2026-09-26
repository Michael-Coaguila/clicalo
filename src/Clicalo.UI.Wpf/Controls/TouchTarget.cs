using System.Windows;
using Clicalo.Domain.Catalog;

namespace Clicalo.UI.Wpf.Controls;

/// <summary>
/// The minimum touch target (REG-02, ACC-002, UIA005): every interactive control responds on at least
/// <see cref="MinimumSize"/> × <see cref="MinimumSize"/> device-independent pixels, even when it is drawn smaller.
/// </summary>
public static class TouchTarget
{
    /// <summary>Smallest side of a touch target, in device-independent pixels (<c>sizes.json</c>, 44).</summary>
    public static double MinimumSize { get; } = PanelSizes.Layout.MinTouchTargetPx;

    /// <summary>
    /// Keeps <see cref="FrameworkElement.MinWidth"/> and <see cref="FrameworkElement.MinHeight"/> of every instance
    /// of <paramref name="controlType"/> at <see cref="MinimumSize"/> or more, whatever a style or a view sets. The
    /// layout box, the mouse hit area and the UI Automation bounding rectangle of the control are then never smaller
    /// than a touch target; a smaller drawing goes inside (see <see cref="TouchTargetBox"/>). Call it once, from the
    /// static constructor of the control.
    /// </summary>
    /// <param name="controlType">A <see cref="FrameworkElement"/> type.</param>
    public static void Enforce(Type controlType)
    {
        ArgumentNullException.ThrowIfNull(controlType);
        FrameworkElement.MinWidthProperty.OverrideMetadata(
            controlType,
            new FrameworkPropertyMetadata(
                MinimumSize,
                null,
                static (_, value) => AtLeastMinimum((double)value)
            )
        );
        FrameworkElement.MinHeightProperty.OverrideMetadata(
            controlType,
            new FrameworkPropertyMetadata(
                MinimumSize,
                null,
                static (_, value) => AtLeastMinimum((double)value)
            )
        );
    }

    /// <summary>True when <paramref name="size"/> (device-independent pixels) is a valid touch target.</summary>
    public static bool IsLargeEnough(Size size) =>
        size.Width >= MinimumSize && size.Height >= MinimumSize;

    /// <summary>A size, in device-independent pixels, raised to <see cref="MinimumSize"/> when smaller.</summary>
    public static double AtLeastMinimum(double size) => Math.Max(size, MinimumSize);
}
