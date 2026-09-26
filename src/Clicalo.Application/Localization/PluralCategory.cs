namespace Clicalo.Application.Localization;

/// <summary>CLDR plural categories, in canonical order. They are the key suffixes of a plural family (<c>_one</c>).</summary>
public enum PluralCategory
{
    /// <summary><c>_zero</c>.</summary>
    Zero,

    /// <summary><c>_one</c>.</summary>
    One,

    /// <summary><c>_two</c>.</summary>
    Two,

    /// <summary><c>_few</c>.</summary>
    Few,

    /// <summary><c>_many</c>.</summary>
    Many,

    /// <summary><c>_other</c>: every language has it and every plural family must define it.</summary>
    Other,
}
