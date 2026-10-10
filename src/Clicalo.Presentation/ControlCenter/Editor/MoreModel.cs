using Clicalo.Domain.Primitives;

namespace Clicalo.Presentation.ControlCenter.Editor;

/// <summary>«Más opciones» (EDI-016 to EDI-018).</summary>
/// <param name="Title">[moreOpts].</param>
/// <param name="Position">«Posición i/N».</param>
/// <param name="Expanded">Whether it is open.</param>
/// <param name="PositionLabel">[position].</param>
/// <param name="Positions">The four position buttons.</param>
/// <param name="HoldLabel">[autoRelease], only for Hold and Toggle.</param>
/// <param name="Holds">Como en General, 30 s, 1 min, 2 min, Nunca.</param>
/// <param name="HoldText">[autoReleaseTimed] or [autoReleaseNever]: what the chosen time really does.</param>
/// <param name="HoldSwitchText">[autoReleaseSwitchOn] or [autoReleaseSwitchOff], as General has it.</param>
/// <param name="MethodLabel">[textMethod], only for Text.</param>
/// <param name="Methods">[tmType] and [tmPaste].</param>
/// <param name="EncryptedText">[textEnc].</param>
/// <param name="ConfirmTitle">The title of the confirmation switch.</param>
/// <param name="ConfirmText">Its description.</param>
/// <param name="Confirm">The confirmation switch.</param>
/// <param name="FrequentsTitle">The title of the Frequents switch.</param>
/// <param name="Frequents">Whether it is pinned in Frequents.</param>
/// <param name="VoiceNumber">The voice number of the shortcut.</param>
/// <param name="VoiceLine">What to say, or [voiceOff].</param>
/// <param name="VoiceHowTitle">[voiceHowT].</param>
/// <param name="VoiceHowOpen">Whether the three steps show.</param>
/// <param name="VoiceSteps">[vh1], [vh2] and [vh3].</param>
public sealed record MoreModel(
    string Title,
    string Position,
    bool Expanded,
    string PositionLabel,
    ValueList<PositionOption> Positions,
    string? HoldLabel,
    ValueList<ChoiceOption> Holds,
    string HoldText,
    string HoldSwitchText,
    string? MethodLabel,
    ValueList<ChoiceOption> Methods,
    string EncryptedText,
    string ConfirmTitle,
    string ConfirmText,
    bool Confirm,
    string FrequentsTitle,
    bool Frequents,
    string VoiceNumber,
    string VoiceLine,
    string VoiceHowTitle,
    bool VoiceHowOpen,
    ValueList<string> VoiceSteps
);
