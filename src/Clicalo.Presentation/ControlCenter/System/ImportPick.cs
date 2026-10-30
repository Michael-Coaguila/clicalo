using Clicalo.Domain.Document;

namespace Clicalo.Presentation.ControlCenter.SystemSection;

/// <summary>A file chosen in Importar, read and validated, before Combinar or Reemplazar (COP-002, COP-005).</summary>
/// <param name="Document">The valid document; nothing in it has run (LOG-006).</param>
/// <param name="Profiles">Its profiles.</param>
/// <param name="Shortcuts">Its shortcuts.</param>
/// <param name="Version">The version of Clícalo that wrote it.</param>
/// <param name="UnavailableTexts">Encrypted texts of another user or machine, imported as unavailable.</param>
public sealed record ImportPick(
    UserDocument Document,
    int Profiles,
    int Shortcuts,
    string Version,
    int UnavailableTexts
);
