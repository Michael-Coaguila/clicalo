namespace Clicalo.Application.Localization;

/// <summary>
/// An entry of a strings file that a <see cref="LanguagePack"/> skipped. Texts of skipped entries fall back to the
/// default language, so a broken translation never breaks the interface; the problem is for the log.
/// </summary>
/// <param name="Key">Physical key as written in the file.</param>
/// <param name="Description">English, developer-facing reason.</param>
public readonly record struct LanguagePackProblem(string Key, string Description);
