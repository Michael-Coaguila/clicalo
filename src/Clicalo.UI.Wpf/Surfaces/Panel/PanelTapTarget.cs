using System.Windows;

namespace Clicalo.UI.Wpf.Surfaces.Panel;

/// <summary>
/// A tappable element of the body of the panel and what an accepted tap on it does. The panel's pointer layer marks
/// the pointer messages as handled (blueprint §8.3), so WPF never raises <c>Click</c> from a finger or the mouse: the
/// surface registers every visible target with its gesture recognizer and calls <see cref="Tap"/> on an accepted tap.
/// UI Automation Invoke, Toggle or ExpandCollapse reach the same action through the control itself.
/// </summary>
/// <param name="Element">The element; its bounds are the touch target (44 × 44 or more, REG-02).</param>
/// <param name="Tap">What an accepted tap does.</param>
public sealed record PanelTapTarget(FrameworkElement Element, Action Tap);
