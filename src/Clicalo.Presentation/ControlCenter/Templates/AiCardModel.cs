using Clicalo.Domain.Primitives;

namespace Clicalo.Presentation.ControlCenter.Templates;

/// <summary>«Crear con IA» (PLA-002 to PLA-006, user decision D5).</summary>
/// <param name="Title">[aiHero].</param>
/// <param name="Description">[aiHeroD].</param>
/// <param name="Placeholder">[aiPh].</param>
/// <param name="Query">The program name in the field.</param>
/// <param name="DictateName">[dictName].</param>
/// <param name="GenerateText">[generate] or [generating].</param>
/// <param name="CanGenerate">Whether [Generar con IA] works (a name, not generating).</param>
/// <param name="Examples">The example chips.</param>
/// <param name="Privacy">[aiPrivacy4].</param>
/// <param name="KeyStatus">[quotaKey] or [keyNone].</param>
/// <param name="KeyButton">[keyUse] or [keyChange].</param>
/// <param name="KeyFieldOpen">Whether the key field shows.</param>
/// <param name="KeyPlaceholder">[keyPh].</param>
/// <param name="PasteText">[keyPaste].</param>
/// <param name="DoneText">[done].</param>
/// <param name="DeleteKeyText">[keyDelete] when a key is saved.</param>
/// <param name="Consent">The consent card, when it waits.</param>
/// <param name="Error">The error card, when there is one.</param>
/// <param name="Keyboard">The keyboard line.</param>
public sealed record AiCardModel(
    string Title,
    string Description,
    string Placeholder,
    string Query,
    string DictateName,
    string GenerateText,
    bool CanGenerate,
    ValueList<string> Examples,
    string Privacy,
    string KeyStatus,
    string KeyButton,
    bool KeyFieldOpen,
    string KeyPlaceholder,
    string PasteText,
    string DoneText,
    string? DeleteKeyText,
    ConsentModel? Consent,
    AiErrorModel? Error,
    KeyboardModel Keyboard
);
