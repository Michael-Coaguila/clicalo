using Clicalo.Domain.Primitives;

namespace Clicalo.Domain.Settings;

/// <summary>Keyboard and apps language (docs/02 <c>keyboard</c>).</summary>
/// <param name="Layout">Layout name shown to the user (<c>es-LA</c>).</param>
/// <param name="AppsLanguage">Language of the target apps; picks the <see cref="Library.ChordVariant"/>.</param>
/// <param name="Detected">Whether the values were detected rather than chosen.</param>
public sealed record KeyboardSettings(string Layout, LangCode AppsLanguage, bool Detected);
