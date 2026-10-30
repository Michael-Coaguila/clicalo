namespace Clicalo.Windowing.IntegrationTests.Automation.Audit;

/// <summary>How the window of a view takes a touch, which decides where a target answers (REG-02).</summary>
internal enum TouchInput
{
    /// <summary>WPF input (Control Center, welcome): a target is hit where it is drawn.</summary>
    Wpf,

    /// <summary>
    /// The pointer layer of the surfaces of the panel (blueprint §8.3): a target answers on its bounds grown to 44 × 44
    /// inside its window, and overlaps go to the nearest center.
    /// </summary>
    PointerLayer,
}
