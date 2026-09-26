using System.Windows.Threading;
using Clicalo.Application.Ports;

namespace Clicalo.UI.Wpf.Windowing.Internal;

/// <summary>
/// What the surfaces of one UI thread were doing when an activation arrived, so that <see cref="ActivationGuard"/>
/// can record a probable cause with every <c>reg01.violations</c> increment (blueprint §3.5).
/// </summary>
/// <remarks>
/// <para>
/// Two sources. Own operations that can activate (<see cref="NonActivatingWindow.ShowPassive"/>, the topmost
/// repair of <see cref="SurfaceIntegrityCheck"/>) wrap their Win32 calls in <see cref="Enter"/> and
/// <see cref="Exit"/>: an activation they cause is delivered synchronously inside the call. Messages (a
/// <c>WM_DPICHANGED</c>, or a <c>WM_ACTIVATEAPP(TRUE)</c> that says another application handed the activation over)
/// are recorded with <see cref="Note"/> and forgotten once the dispatcher runs its next operation: Windows delivers
/// the whole activation sequence of one change (<c>WM_ACTIVATEAPP</c>, <c>WM_NCACTIVATE</c>, <c>WM_ACTIVATE</c>)
/// inside one message retrieval, so no dispatcher operation runs in between.
/// </para>
/// <para>An own operation beats a message, and a message about this process beats <see cref="ActivationCause.External"/>.</para>
/// </remarks>
internal sealed class ActivationHints(Dispatcher dispatcher)
{
    private ActivationCause _operation = ActivationCause.Unknown;
    private ActivationCause _noted = ActivationCause.Unknown;
    private bool _forgetQueued;

    /// <summary>The probable cause of an activation arriving now.</summary>
    public ActivationCause Current => _operation != ActivationCause.Unknown ? _operation : _noted;

    /// <summary>Starts an own operation that may activate; returns the value to pass to <see cref="Exit"/>.</summary>
    public ActivationCause Enter(ActivationCause cause)
    {
        var previous = _operation;
        _operation = cause;
        return previous;
    }

    /// <summary>Ends the operation started by <see cref="Enter"/>.</summary>
    public void Exit(ActivationCause previous) => _operation = previous;

    /// <summary>Records a message that explains an activation arriving before the next dispatcher operation.</summary>
    public void Note(ActivationCause cause)
    {
        if (_noted == ActivationCause.Unknown || _noted == ActivationCause.External)
        {
            _noted = cause;
        }

        if (!_forgetQueued)
        {
            _forgetQueued = true;
            _ = dispatcher.InvokeAsync(Forget, DispatcherPriority.Send);
        }
    }

    private void Forget()
    {
        _forgetQueued = false;
        _noted = ActivationCause.Unknown;
    }
}
