using System.Collections.Immutable;

namespace Clicalo.Application.Localization;

/// <summary>One CLDR relation: <c>operand [% modulus] (= | !=) range_list</c>.</summary>
internal sealed class PluralRelation(
    char operand,
    decimal? modulus,
    bool negated,
    ImmutableArray<PluralRange> ranges
)
{
    public bool Matches(PluralOperands operands)
    {
        var value = operand switch
        {
            'n' => operands.N,
            'i' => operands.I,
            'v' => operands.V,
            'w' => operands.W,
            'f' => operands.F,
            't' => operands.T,
            _ => 0m, // 'e' and 'c': compact exponent, never used by Clícalo.
        };
        if (modulus is { } m)
        {
            value %= m;
        }

        var inList = false;
        foreach (var range in ranges)
        {
            if (range.Contains(value))
            {
                inList = true;
                break;
            }
        }

        return inList != negated;
    }
}
