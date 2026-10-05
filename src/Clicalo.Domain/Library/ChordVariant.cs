using Clicalo.Domain.Keys;
using Clicalo.Domain.Primitives;

namespace Clicalo.Domain.Library;

/// <summary>
/// The combination to send when the target apps use another language (docs/02 <c>vk</c>): Word's Bold is Ctrl+N in
/// Spanish and Ctrl+B in English. Chosen with the apps language of the keyboard settings.
/// </summary>
/// <param name="AppsLanguage">Language of the target apps.</param>
/// <param name="Chord">The combination for that language.</param>
public sealed record ChordVariant(LangCode AppsLanguage, KeyChord Chord);
