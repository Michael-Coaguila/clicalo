namespace Clicalo.Application.Localization;

/// <summary>The texts of one key in one language: a plain template or one template per plural category.</summary>
internal sealed class LanguagePackEntry
{
    private readonly MessageTemplate?[] _forms = new MessageTemplate?[
        (int)PluralCategory.Other + 1
    ];
    private MessageTemplate? _plain;

    /// <summary>True when the key is a plural family in this language.</summary>
    public bool IsPlural { get; private set; }

    /// <summary>True when the entry can produce a text (plain, or plural with its <c>other</c> form).</summary>
    public bool IsComplete =>
        IsPlural ? _forms[(int)PluralCategory.Other] is not null : _plain is not null;

    /// <summary>Sets the plain text; false when the key already has plural forms or a plain text.</summary>
    public bool TrySetPlain(MessageTemplate template)
    {
        if (IsPlural || _plain is not null)
        {
            return false;
        }

        _plain = template;
        return true;
    }

    /// <summary>Sets a plural form; false when the key already has a plain text or that form.</summary>
    public bool TrySetForm(PluralCategory category, MessageTemplate template)
    {
        if (_plain is not null || _forms[(int)category] is not null)
        {
            return false;
        }

        IsPlural = true;
        _forms[(int)category] = template;
        return true;
    }

    /// <summary>The template for a category (plain entries ignore it); a missing plural form falls back to <c>other</c>.</summary>
    public MessageTemplate Select(PluralCategory category) =>
        _plain ?? _forms[(int)category] ?? _forms[(int)PluralCategory.Other]!;
}
