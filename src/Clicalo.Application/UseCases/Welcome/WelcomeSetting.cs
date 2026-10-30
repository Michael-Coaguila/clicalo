namespace Clicalo.Application.UseCases.Welcome;

/// <summary>The four settings the answers of «¿Cómo usas tu equipo?» change (BIE-005), in the order they are reported.</summary>
public enum WelcomeSetting
{
    /// <summary>The touch preset (<c>touch.preset</c> and its four values).</summary>
    TouchPreset,

    /// <summary>The panel size (<c>size</c>).</summary>
    Size,

    /// <summary>Numbers for voice control (<c>voiceNumbers</c>).</summary>
    VoiceNumbers,

    /// <summary>«No puedo usar el teclado» (<c>noKeyboardUser</c>).</summary>
    NoKeyboard,
}
