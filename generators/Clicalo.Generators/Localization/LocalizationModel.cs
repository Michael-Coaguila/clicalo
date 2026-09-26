using System.Collections.Immutable;

namespace Clicalo.Generators.Localization;

/// <summary>Result of analysing <c>data/i18n</c>: what to generate and every data error found.</summary>
internal sealed class LocalizationModel(
    string? defaultLanguage,
    ImmutableArray<LocaleDefinition> locales,
    ImmutableArray<PlaceholderDefinition> placeholders,
    ImmutableArray<MessageDefinition> messages,
    ImmutableArray<LocalizationIssue> issues
)
{
    /// <summary>Code of the default language, or <c>null</c> when <c>locales.json</c> is unusable.</summary>
    public string? DefaultLanguage { get; } = defaultLanguage;

    public ImmutableArray<LocaleDefinition> Locales { get; } = locales;

    public ImmutableArray<PlaceholderDefinition> Placeholders { get; } = placeholders;

    /// <summary>Valid logical keys in the order of the default language file.</summary>
    public ImmutableArray<MessageDefinition> Messages { get; } = messages;

    /// <summary>Data errors sorted by file, line, column and id.</summary>
    public ImmutableArray<LocalizationIssue> Issues { get; } = issues;

    public bool HasErrors => !Issues.IsEmpty;
}
