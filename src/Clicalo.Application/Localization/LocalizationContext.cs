using System.Collections.Frozen;
using System.Collections.Immutable;

namespace Clicalo.Application.Localization;

/// <summary>
/// Holds one immutable <see cref="Localizer"/> per language (each falling back to the default language) and swaps
/// the current one atomically. Thread-safe; <see cref="LanguageChanged"/> is raised outside the lock.
/// </summary>
public sealed class LocalizationContext : ILocalizationContext
{
    private readonly Lock _gate = new();
    private readonly FrozenDictionary<string, ILocalizer> _localizers;
    private ILocalizer _current;

    /// <summary>Creates the context.</summary>
    /// <param name="packs">One pack per language, in the order to offer them.</param>
    /// <param name="defaultLanguage">Code of the default language, used as fallback for missing texts.</param>
    /// <param name="initialLanguage">Language to start with; the default one when <c>null</c> or unavailable.</param>
    /// <exception cref="ArgumentException">No pack for <paramref name="defaultLanguage"/>, or two packs share a code.</exception>
    public LocalizationContext(
        IEnumerable<LanguagePack> packs,
        string defaultLanguage,
        string? initialLanguage = null
    )
    {
        ArgumentNullException.ThrowIfNull(packs);
        ArgumentException.ThrowIfNullOrWhiteSpace(defaultLanguage);
        var list = packs.ToList();
        var fallback =
            list.Find(p => string.Equals(p.Locale.Code, defaultLanguage, StringComparison.Ordinal))
            ?? throw new ArgumentException(
                "There is no pack for the default language.",
                nameof(defaultLanguage)
            );
        var localizers = new Dictionary<string, ILocalizer>(StringComparer.Ordinal);
        foreach (var pack in list)
        {
            if (!localizers.TryAdd(pack.Locale.Code, new Localizer(pack, fallback)))
            {
                throw new ArgumentException(
                    "Two packs have the language '" + pack.Locale.Code + "'.",
                    nameof(packs)
                );
            }
        }

        _localizers = localizers.ToFrozenDictionary(StringComparer.Ordinal);
        Languages = [.. list.Select(static p => p.Locale)];
        _current =
            initialLanguage is not null && _localizers.TryGetValue(initialLanguage, out var initial)
                ? initial
                : _localizers[defaultLanguage];
    }

    /// <inheritdoc/>
    public event EventHandler<LanguageChangedEventArgs>? LanguageChanged;

    /// <inheritdoc/>
    public ILocalizer Current => Volatile.Read(ref _current);

    /// <inheritdoc/>
    public ImmutableArray<LocaleInfo> Languages { get; }

    /// <inheritdoc/>
    public bool TrySetLanguage(string code)
    {
        ArgumentNullException.ThrowIfNull(code);
        if (!_localizers.TryGetValue(code, out var next))
        {
            return false;
        }

        ILocalizer previous;
        lock (_gate)
        {
            previous = _current;
            if (ReferenceEquals(previous, next))
            {
                return true;
            }

            Volatile.Write(ref _current, next);
        }

        LanguageChanged?.Invoke(this, new LanguageChangedEventArgs(previous, next));
        return true;
    }
}
