using Clicalo.Domain.Primitives;

namespace Clicalo.Domain.Templates;

/// <summary>
/// The example chips of «Crear con IA» (PLA-002): program names, the same in every language, that only fill the field.
/// </summary>
public static class AiExamples
{
    /// <summary>Photoshop, Spotify, Teams, Canva, WhatsApp and OBS, in the order of the prototype.</summary>
    public static ValueList<string> All { get; } =
        ["Photoshop", "Spotify", "Teams", "Canva", "WhatsApp", "OBS"];
}
