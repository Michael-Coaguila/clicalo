namespace Clicalo.DevCli.AnonymizeV1;

/// <summary>What one anonymized v1 file contains and how much was replaced; counts only, never content.</summary>
/// <param name="Profiles">Profiles.</param>
/// <param name="Buttons">Buttons, separators included.</param>
/// <param name="KeptNames">Profile and button names kept because they are public.</param>
/// <param name="ReplacedTexts">Different texts replaced by placeholders.</param>
internal sealed record AnonymizeStats(int Profiles, int Buttons, int KeptNames, int ReplacedTexts);
