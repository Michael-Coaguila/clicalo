namespace Clicalo.Domain.Keys;

/// <summary>How <see cref="KeyChordFormatter"/> writes a combination.</summary>
public enum KeyLabelStyle
{
    /// <summary>Full labels joined by « + » («Ctrl + Shift + T»): the key line of the sizes M and L.</summary>
    Full,

    /// <summary>
    /// Abbreviations joined by «+» without spaces («Ctl+⇧+T»): the key line of the size S (CUA-008).
    /// </summary>
    Abbreviated,

    /// <summary>
    /// The names a screen reader says, joined by « + » («Win + Flecha izquierda»): the accessible name always uses the
    /// full names (CUA-008).
    /// </summary>
    Spoken,
}
