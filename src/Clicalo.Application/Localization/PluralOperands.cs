namespace Clicalo.Application.Localization;

/// <summary>
/// The CLDR plural operands of a number (UTS #35, part 3, «Plural Operand Meanings»): <c>n</c> absolute value,
/// <c>i</c> integer digits, <c>v</c>/<c>w</c> number of visible fraction digits with/without trailing zeros,
/// <c>f</c>/<c>t</c> visible fraction digits with/without trailing zeros, and <c>e</c>/<c>c</c> compact exponent
/// (always 0: Clícalo never uses compact notation).
/// </summary>
public readonly record struct PluralOperands
{
    private PluralOperands(decimal n, decimal i, int v, int w, decimal f, decimal t)
    {
        N = n;
        I = i;
        V = v;
        W = w;
        F = f;
        T = t;
    }

    /// <summary>Absolute value.</summary>
    public decimal N { get; }

    /// <summary>Integer digits of <see cref="N"/>.</summary>
    public decimal I { get; }

    /// <summary>Number of visible fraction digits, with trailing zeros.</summary>
    public int V { get; }

    /// <summary>Number of visible fraction digits, without trailing zeros.</summary>
    public int W { get; }

    /// <summary>Visible fraction digits, with trailing zeros, as an integer.</summary>
    public decimal F { get; }

    /// <summary>Visible fraction digits, without trailing zeros, as an integer.</summary>
    public decimal T { get; }

    /// <summary>Operands of a whole number.</summary>
    public static PluralOperands FromWholeNumber(long value)
    {
        var n = Math.Abs((decimal)value);
        return new PluralOperands(n, n, 0, 0, 0m, 0m);
    }

    /// <summary>Operands of a decimal number; its scale counts as visible digits (<c>1.0m</c> has <c>v = 1</c>).</summary>
    public static PluralOperands FromDecimalNumber(decimal value)
    {
        var n = Math.Abs(value);
        var i = decimal.Truncate(n);
        var v = (int)n.Scale;
        var f = decimal.Truncate((n - i) * Pow10(v));
        var t = f;
        var w = v;
        while (w > 0 && t % 10m == 0m)
        {
            t /= 10m;
            w--;
        }

        return new PluralOperands(n, i, v, w, f, t);
    }

    private static decimal Pow10(int exponent)
    {
        var result = 1m;
        for (var k = 0; k < exponent; k++)
        {
            result *= 10m;
        }

        return result;
    }
}
