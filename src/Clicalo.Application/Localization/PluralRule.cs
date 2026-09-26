using System.Collections.Immutable;

namespace Clicalo.Application.Localization;

/// <summary>A CLDR plural condition such as <c>i = 1 and v = 0</c>, parsed once and evaluated on <see cref="PluralOperands"/>.</summary>
public sealed class PluralRule
{
    private readonly ImmutableArray<ImmutableArray<PluralRelation>> _alternatives;

    private PluralRule(string source, ImmutableArray<ImmutableArray<PluralRelation>> alternatives)
    {
        Source = source;
        _alternatives = alternatives;
    }

    /// <summary>The condition as written in <c>locales.json</c>.</summary>
    public string Source { get; }

    /// <summary>Parses a CLDR condition (see <see cref="PluralRuleParser"/> for the grammar).</summary>
    /// <exception cref="FormatException">The condition is not valid.</exception>
    public static PluralRule Parse(string condition)
    {
        ArgumentNullException.ThrowIfNull(condition);
        return new PluralRule(condition, PluralRuleParser.Parse(condition));
    }

    /// <summary>True when the operands satisfy the condition.</summary>
    public bool Matches(PluralOperands operands)
    {
        foreach (var alternative in _alternatives)
        {
            var all = true;
            foreach (var relation in alternative)
            {
                if (!relation.Matches(operands))
                {
                    all = false;
                    break;
                }
            }

            if (all)
            {
                return true;
            }
        }

        return false;
    }

    /// <inheritdoc/>
    public override string ToString() => Source;
}
