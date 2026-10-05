using Clicalo.Domain.Keys;
using Clicalo.Domain.Primitives;

namespace Clicalo.Domain.CommonActions;

/// <summary>
/// Where a common action takes another combination (decision D4): the apps of a family, by executable, when the
/// programs are in <see cref="AppsLanguage"/> (Save is Ctrl+G in Word, Excel, PowerPoint and Outlook in Spanish).
/// </summary>
/// <param name="Processes">The executables of the families it covers, compared without case (PER-002).</param>
/// <param name="AppsLanguage">The programs language of the keyboard settings it applies to.</param>
/// <param name="Chord">The combination those apps expect.</param>
public sealed record CommonActionOverride(
    ValueList<ProcessName> Processes,
    LangCode AppsLanguage,
    KeyChord Chord
);
