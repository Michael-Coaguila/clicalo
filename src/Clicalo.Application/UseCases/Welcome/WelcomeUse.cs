namespace Clicalo.Application.UseCases.Welcome;

/// <summary>The options of the welcome step «¿Cómo usas tu equipo?» (BIE-005), in the order they are shown.</summary>
public enum WelcomeUse
{
    /// <summary>Pantalla táctil (<c>touch_app</c>): the mild tremor preset.</summary>
    Touch,

    /// <summary>Control por voz (<c>mic</c>): numbers for voice on.</summary>
    Voice,

    /// <summary>No puedo usar el teclado (<c>keyboard_off</c>): the mild preset and <c>noKeyboardUser</c>.</summary>
    NoKeyboard,

    /// <summary>Tengo temblor (<c>vibration</c>): the strong tremor preset and size L.</summary>
    Tremor,

    /// <summary>Mouse o trackball (<c>mouse</c>): no effect of its own.</summary>
    Mouse,
}
