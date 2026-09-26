using System.Globalization;
using System.Text;
using Clicalo.Domain.Messages;

namespace Clicalo.Application.Localization;

/// <summary>
/// Pure message formatter for one language, with the default language as fallback. Named placeholders, CLDR plural
/// selection on <c>{count}</c>, decimal separator of <c>locales.json</c> and nested messages (IDI-004). Numbers are
/// written without digit grouping: every number in the interface is a count, a position or a short decimal.
/// </summary>
public sealed class Localizer : ILocalizer
{
    /// <summary>Nested messages deeper than this are shown as their key; real messages nest one or two levels.</summary>
    private const int MaxNesting = 8;

    /// <summary>The argument that selects the plural form, as in i18next.</summary>
    private const string PluralSelector = "count";

    private readonly LanguagePack _pack;
    private readonly LanguagePack? _fallback;

    /// <summary>Creates a formatter.</summary>
    /// <param name="pack">Texts of the language.</param>
    /// <param name="fallback">Texts of the default language, used for keys the pack lacks; <c>null</c> for the default language itself.</param>
    public Localizer(LanguagePack pack, LanguagePack? fallback = null)
    {
        ArgumentNullException.ThrowIfNull(pack);
        _pack = pack;
        _fallback = ReferenceEquals(pack, fallback) ? null : fallback;
    }

    /// <inheritdoc/>
    public LocaleInfo Locale => _pack.Locale;

    /// <inheritdoc/>
    public bool Contains(MessageKey key) =>
        _pack.Contains(key) || (_fallback?.Contains(key) ?? false);

    /// <inheritdoc/>
    public string Format(Message message)
    {
        ArgumentNullException.ThrowIfNull(message);
        var sb = new StringBuilder();
        Append(sb, message, 0);
        return sb.ToString();
    }

    private static PluralOperands? CountOf(Message message) =>
        !message.TryGetArgument(PluralSelector, out var value) ? null
        : value.TryGetWholeNumber(out var whole) ? PluralOperands.FromWholeNumber(whole)
        : value.TryGetDecimalNumber(out var number) ? PluralOperands.FromDecimalNumber(number)
        : null;

    private static void AppendDecimal(StringBuilder sb, decimal value, LocaleInfo locale)
    {
        var invariant = value.ToString(CultureInfo.InvariantCulture);
        var point = invariant.IndexOf('.', StringComparison.Ordinal);
        if (point < 0)
        {
            sb.Append(invariant);
            return;
        }

        sb.Append(invariant, 0, point)
            .Append(locale.DecimalSeparator)
            .Append(invariant, point + 1, invariant.Length - point - 1);
    }

    private void Append(StringBuilder sb, Message message, int depth)
    {
        var pack = _pack;
        if (!pack.TryGetEntry(message.Key, out var entry))
        {
            if (_fallback is null || !_fallback.TryGetEntry(message.Key, out entry))
            {
                sb.Append(message.Key.Value);
                return;
            }

            pack = _fallback;
        }

        if (depth > MaxNesting)
        {
            sb.Append(message.Key.Value);
            return;
        }

        // Plural selection and number format follow the language of the text actually used.
        var locale = pack.Locale;
        var category =
            entry.IsPlural && CountOf(message) is { } operands
                ? locale.PluralRules.Select(operands)
                : PluralCategory.Other;
        foreach (var segment in entry.Select(category).Segments)
        {
            if (!segment.IsPlaceholder)
            {
                sb.Append(segment.Text);
            }
            else if (message.TryGetArgument(segment.Text, out var value))
            {
                AppendValue(sb, value, locale, depth);
            }
            else
            {
                sb.Append('{').Append(segment.Text).Append('}');
            }
        }
    }

    private void AppendValue(StringBuilder sb, MessageValue value, LocaleInfo locale, int depth)
    {
        if (value.TryGetText(out var text))
        {
            sb.Append(text);
        }
        else if (value.TryGetMessage(out var nested))
        {
            Append(sb, nested, depth + 1);
        }
        else if (value.TryGetWholeNumber(out var whole))
        {
            sb.Append(whole.ToString(CultureInfo.InvariantCulture));
        }
        else if (value.TryGetDecimalNumber(out var number))
        {
            AppendDecimal(sb, number, locale);
        }
    }
}
