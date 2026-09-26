using Clicalo.Application.Ports;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Controls;

namespace Clicalo.UI.Wpf.Pointer;

/// <summary>
/// Process and window setup of the own pointer layer (blueprint §8.3, ADR-0006): WPF's stylus and touch stacks are
/// switched off (they hang at logon #3147, lose touch on topmost windows after a device change #2054 and die with
/// WMI #9752), so touch arrives only as <c>WM_POINTER*</c> to <see cref="PointerInputSource"/>, the mouse is routed
/// through the same messages, and the touch feedback circle of Windows is disabled on every surface (ACC-007).
/// </summary>
/// <remarks>
/// At start-up, before the first WPF window: <see cref="DisableStylusAndTouchSupport"/> and
/// <see cref="EnableMouseInPointer"/>. Per window, before its first show: <see cref="DisableTouchFeedback"/>.
/// </remarks>
public static class PointerSetup
{
    /// <summary>The <see cref="AppContext"/> switch that turns off WPF's stylus and touch support.</summary>
    public const string DisableStylusAndTouchSupportSwitch =
        "Switch.System.Windows.Input.Stylus.DisableStylusAndTouchSupport";

    /// <summary>Every visual feedback of <c>SetWindowFeedbackSetting</c> (<c>FEEDBACK_TYPE</c> 1 to 11).</summary>
    private static readonly FEEDBACK_TYPE[] FeedbackTypes =
    [
        FEEDBACK_TYPE.FEEDBACK_TOUCH_CONTACTVISUALIZATION,
        FEEDBACK_TYPE.FEEDBACK_PEN_BARRELVISUALIZATION,
        FEEDBACK_TYPE.FEEDBACK_PEN_TAP,
        FEEDBACK_TYPE.FEEDBACK_PEN_DOUBLETAP,
        FEEDBACK_TYPE.FEEDBACK_PEN_PRESSANDHOLD,
        FEEDBACK_TYPE.FEEDBACK_PEN_RIGHTTAP,
        FEEDBACK_TYPE.FEEDBACK_TOUCH_TAP,
        FEEDBACK_TYPE.FEEDBACK_TOUCH_DOUBLETAP,
        FEEDBACK_TYPE.FEEDBACK_TOUCH_PRESSANDHOLD,
        FEEDBACK_TYPE.FEEDBACK_TOUCH_RIGHTTAP,
        FEEDBACK_TYPE.FEEDBACK_GESTURE_PRESSANDTAP,
    ];

    /// <summary>True when <see cref="DisableStylusAndTouchSupportSwitch"/> is on in this process.</summary>
    public static bool IsStylusAndTouchSupportDisabled =>
        AppContext.TryGetSwitch(DisableStylusAndTouchSupportSwitch, out var disabled) && disabled;

    /// <summary>True when the mouse of this process arrives as <c>WM_POINTER*</c> (<see cref="EnableMouseInPointer"/>).</summary>
    public static bool IsMouseInPointerEnabled => PInvoke.IsMouseInPointerEnabled();

    /// <summary>
    /// Sets <see cref="DisableStylusAndTouchSupportSwitch"/> for the process. Must run before the first WPF
    /// window or <c>Application</c> is created; the executables also set it in their runtime configuration
    /// (<c>RuntimeHostConfigurationOption</c>), which is what makes it effective at logon.
    /// </summary>
    public static void DisableStylusAndTouchSupport() =>
        AppContext.SetSwitch(DisableStylusAndTouchSupportSwitch, true);

    /// <summary>
    /// Routes the mouse (and the precision touchpad) of the whole process through <c>WM_POINTER*</c>
    /// (<c>EnableMouseInPointer</c>), so <see cref="PointerInputSource"/> turns a mouse click on a surface into the
    /// same frames as a finger (finger, pen and mouse in spike S1). Pointer messages that no source consumes still reach
    /// WPF as ordinary mouse messages through <c>DefWindowProc</c>, so activatable windows (Control Center) keep their
    /// mouse input. Process-wide and permanent: call it once at start-up, before the first window.
    /// </summary>
    /// <returns>True when the mouse arrives as pointer messages (now or already before the call).</returns>
    public static bool EnableMouseInPointer() =>
        IsMouseInPointerEnabled || PInvoke.EnableMouseInPointer(true);

    /// <summary>
    /// Disables every <c>FEEDBACK_TYPE</c> of <c>SetWindowFeedbackSetting</c> on <paramref name="window"/>: no touch
    /// circle, no press-and-hold ring, no simulated right click. Called by <c>NonActivatingWindow</c> before the first
    /// show, and by the Control Center for its own window. Must run on the thread that owns the window.
    /// </summary>
    /// <returns>True when every feedback type was disabled.</returns>
    public static unsafe bool DisableTouchFeedback(WindowToken window)
    {
        if (window.IsNone)
        {
            throw new ArgumentException("The window cannot be empty.", nameof(window));
        }

        BOOL enabled = false;
        var allDisabled = true;
        foreach (var feedback in FeedbackTypes)
        {
            allDisabled &= (bool)
                PInvoke.SetWindowFeedbackSetting(
                    (HWND)window.Handle,
                    feedback,
                    0,
                    (uint)sizeof(BOOL),
                    &enabled
                );
        }

        return allDisabled;
    }

    /// <summary>
    /// True when every <c>FEEDBACK_TYPE</c> is explicitly disabled on <paramref name="window"/> itself
    /// (<c>GetWindowFeedbackSetting</c>): what <see cref="DisableTouchFeedback"/> leaves behind, checked by the S1
    /// tests and available to the surface integrity check.
    /// </summary>
    public static unsafe bool IsTouchFeedbackDisabled(WindowToken window)
    {
        if (window.IsNone)
        {
            throw new ArgumentException("The window cannot be empty.", nameof(window));
        }

        foreach (var feedback in FeedbackTypes)
        {
            BOOL enabled = true;
            var size = (uint)sizeof(BOOL);
            var configured = PInvoke.GetWindowFeedbackSetting(
                (HWND)window.Handle,
                feedback,
                0,
                &size,
                &enabled
            );
            if (!configured || enabled)
            {
                return false;
            }
        }

        return true;
    }
}
