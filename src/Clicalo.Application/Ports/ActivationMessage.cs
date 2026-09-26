namespace Clicalo.Application.Ports;

/// <summary>The activation message a surface received (blueprint §3.5, hook table).</summary>
public enum ActivationMessage
{
    /// <summary><c>WM_ACTIVATE</c> with a state other than <c>WA_INACTIVE</c>.</summary>
    Activate,

    /// <summary><c>WM_NCACTIVATE</c> with <c>TRUE</c>.</summary>
    NcActivate,

    /// <summary><c>WM_ACTIVATEAPP</c> with <c>TRUE</c>.</summary>
    ActivateApp,
}
