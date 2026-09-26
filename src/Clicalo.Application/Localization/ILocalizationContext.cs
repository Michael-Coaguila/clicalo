using System.Collections.Immutable;

namespace Clicalo.Application.Localization;

/// <summary>
/// The language of the interface and its hot switch (IDI-001). Readers take <see cref="Current"/> when they paint;
/// adapters (one <c>LocalizationSource</c> per dispatcher in UI.Wpf) listen to <see cref="LanguageChanged"/> and
/// repaint, so every window changes language at once without restarting.
/// </summary>
public interface ILocalizationContext
{
    /// <summary>Formatter of the current language (an immutable snapshot).</summary>
    ILocalizer Current { get; }

    /// <summary>Available languages, in <c>locales.json</c> order.</summary>
    ImmutableArray<LocaleInfo> Languages { get; }

    /// <summary>Raised after <see cref="Current"/> changes, on the thread that changed it.</summary>
    event EventHandler<LanguageChangedEventArgs>? LanguageChanged;

    /// <summary>Switches the language; false when <paramref name="code"/> is not available.</summary>
    bool TrySetLanguage(string code);
}
