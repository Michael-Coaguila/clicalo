using Clicalo.Domain.Messages;

namespace Clicalo.Application.Localization;

/// <summary>
/// Formats messages in one language. An instance is an immutable snapshot: a language change produces another
/// instance (see <see cref="ILocalizationContext"/>), so a formatter can be used from any thread without locks.
/// </summary>
public interface ILocalizer
{
    /// <summary>The language of this snapshot.</summary>
    LocaleInfo Locale { get; }

    /// <summary>
    /// The visible text of <paramref name="message"/>: its template in this language (or in the default language
    /// when this one lacks it), with the plural form chosen by <c>{count}</c>, numbers written with the decimal
    /// separator of the language and nested messages localized in this language. Never throws for data problems:
    /// a missing argument is shown as <c>{name}</c> and an unknown key as the key itself.
    /// </summary>
    string Format(Message message);

    /// <summary>True when the key has a text in this language or in the default one.</summary>
    bool Contains(MessageKey key);
}
