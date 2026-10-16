using Clicalo.UI.Wpf.Theming.Generated;

namespace Clicalo.UI.Wpf.Controls;

/// <summary>The height of each <see cref="ControlSize"/>.</summary>
public static class ControlSizes
{
    /// <summary>The height of <paramref name="size"/>, in device-independent pixels.</summary>
    public static double Height(ControlSize size) =>
        size switch
        {
            ControlSize.Small => ControlHeight.Small,
            ControlSize.Medium => ControlHeight.Medium,
            ControlSize.Large => ControlHeight.Large,
            _ => throw new ArgumentOutOfRangeException(nameof(size), size, message: null),
        };
}
