namespace Clicalo.Application.Localization;

/// <summary>A value (<c>Low == High</c>) or an inclusive integer range (<c>2..4</c>) of a CLDR plural relation.</summary>
internal readonly record struct PluralRange(decimal Low, decimal High)
{
    /// <summary>
    /// A single value matches by equality; a range matches integers only, as the CLDR <c>=</c> relation
    /// (<c>n = 2..4</c> is false for 2.5).
    /// </summary>
    public bool Contains(decimal value) =>
        Low == High
            ? value == Low
            : decimal.Truncate(value) == value && value >= Low && value <= High;
}
