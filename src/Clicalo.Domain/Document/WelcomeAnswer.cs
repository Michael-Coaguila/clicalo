namespace Clicalo.Domain.Document;

/// <summary>
/// An answer of the welcome step «¿Cómo usas tu equipo?» (BIE-005), as persisted in <see cref="WelcomeAnswers.Uses"/>
/// (ADR-0028). Same members and order as <c>Clicalo.Application.UseCases.Welcome.WelcomeUse</c>, which the
/// Application maps one to one; the persisted names are <c>touch</c>, <c>voice</c>, <c>noKeyboard</c>, <c>tremor</c>
/// and <c>mouse</c>.
/// </summary>
public enum WelcomeAnswer
{
    /// <summary>Pantalla táctil.</summary>
    Touch,

    /// <summary>Control por voz.</summary>
    Voice,

    /// <summary>No puedo usar el teclado.</summary>
    NoKeyboard,

    /// <summary>Tengo temblor.</summary>
    Tremor,

    /// <summary>Mouse o trackball.</summary>
    Mouse,
}
