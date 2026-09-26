using System.Globalization;
using Clicalo.Application.Localization;

namespace Clicalo.Application.Tests.Localization;

public sealed class PluralRulesTests
{
    [Theory]
    [InlineData("1", 1, 1, 0, 0, 0, 0)]
    [InlineData("1.0", 1, 1, 1, 0, 0, 0)]
    [InlineData("1.50", 1.50, 1, 2, 1, 50, 5)]
    [InlineData("-2.305", 2.305, 2, 3, 3, 305, 305)]
    [InlineData("0.100", 0.100, 0, 3, 1, 100, 1)]
    public void Operands_follow_the_CLDR_definitions(
        string value,
        double n,
        long i,
        int v,
        int w,
        long f,
        long t
    )
    {
        var operands = PluralOperands.FromDecimalNumber(
            decimal.Parse(value, CultureInfo.InvariantCulture)
        );

        operands.N.ShouldBe((decimal)n);
        operands.I.ShouldBe(i);
        operands.V.ShouldBe(v);
        operands.W.ShouldBe(w);
        operands.F.ShouldBe(f);
        operands.T.ShouldBe(t);
    }

    [Fact]
    public void Whole_numbers_have_no_fraction_digits()
    {
        var operands = PluralOperands.FromWholeNumber(long.MinValue);

        operands.N.ShouldBe(9223372036854775808m);
        operands.V.ShouldBe(0);
    }

    [Theory]
    [InlineData("one", "i = 1 and v = 0", "1", true)]
    [InlineData("one", "i = 1 and v = 0", "1.0", false)]
    [InlineData("one", "n = 1", "1.0", true)]
    [InlineData("few", "v = 0 and i % 10 = 2..4 and i % 100 != 12..14", "22", true)]
    [InlineData("few", "v = 0 and i % 10 = 2..4 and i % 100 != 12..14", "12", false)]
    [InlineData("one", "n = 0,1 or i = 0 and f = 1", "0.1", true)]
    [InlineData("one", "n = 2..4", "2.5", false)]
    [InlineData("one", "n % 10 = 1 and n % 100 != 11", "21", true)]
    [InlineData("one", "n%10=1", "31", true)]
    [InlineData("one", "t = 5 @decimal 0.5, 1.5", "1.50", true)]
    public void Conditions_match_as_in_CLDR(
        string category,
        string condition,
        string value,
        bool matches
    )
    {
        var rules = PluralRules.Create([
            KeyValuePair.Create(Enum.Parse<PluralCategory>(category, true), condition),
        ]);
        var operands = PluralOperands.FromDecimalNumber(
            decimal.Parse(value, CultureInfo.InvariantCulture)
        );

        (rules.Select(operands) != PluralCategory.Other).ShouldBe(matches);
    }

    [Theory]
    [InlineData("")]
    [InlineData("n")]
    [InlineData("n =")]
    [InlineData("x = 1")]
    [InlineData("n = 3..1")]
    [InlineData("n is 1")]
    [InlineData("n in 1..3")]
    [InlineData("n % 0 = 1")]
    [InlineData("n = 1 and")]
    [InlineData("n = 1 nor i = 2")]
    [InlineData("nn = 1")]
    [InlineData("n = 123456789012345678901234567890")]
    public void Invalid_conditions_are_rejected(string condition) =>
        Should.Throw<FormatException>(() => PluralRule.Parse(condition));

    [Fact]
    public void A_category_takes_one_rule_and_other_takes_none()
    {
        Should.Throw<ArgumentException>(() =>
            PluralRules.Create([
                KeyValuePair.Create(PluralCategory.One, "n = 1"),
                KeyValuePair.Create(PluralCategory.One, "n = 2"),
            ])
        );
        Should.Throw<ArgumentException>(() =>
            PluralRules.Create([KeyValuePair.Create(PluralCategory.Other, "n = 1")])
        );
    }

    [Fact]
    public void Categories_are_listed_in_canonical_order_ending_with_other()
    {
        var rules = PluralRules.Create([
            KeyValuePair.Create(PluralCategory.Many, "n = 11"),
            KeyValuePair.Create(PluralCategory.One, "n = 1"),
        ]);

        rules.Categories.ShouldBe([PluralCategory.One, PluralCategory.Many, PluralCategory.Other]);
    }

    [Fact]
    [Trait("Req", "IDI-006")]
    public void The_rules_of_locales_json_classify_their_own_CLDR_samples()
    {
        foreach (var locale in I18nRepository.Locales())
        {
            using var document = System.Text.Json.JsonDocument.Parse(
                File.ReadAllText(Path.Combine(I18nRepository.DataDirectory, "locales.json"))
            );
            var entry = document
                .RootElement.GetProperty("locales")
                .EnumerateArray()
                .Single(l =>
                    string.Equals(
                        l.GetProperty("code").GetString(),
                        locale.Code,
                        StringComparison.Ordinal
                    )
                );
            foreach (var rule in entry.GetProperty("plural").EnumerateObject())
            {
                var category = Enum.Parse<PluralCategory>(rule.Name, ignoreCase: true);
                var samples = Samples(rule.Value.GetString()!).ToList();
                samples.ShouldNotBeEmpty(
                    locale.Code + " " + rule.Name + " should list CLDR samples"
                );
                foreach (var sample in samples)
                {
                    locale
                        .PluralRules.Select(PluralOperands.FromDecimalNumber(sample))
                        .ShouldBe(category, locale.Code + " " + sample);
                }
            }
        }
    }

    [Theory]
    [InlineData("es", "0", PluralCategory.Other)]
    [InlineData("es", "1", PluralCategory.One)]
    [InlineData("es", "2", PluralCategory.Other)]
    [InlineData("es", "1.0", PluralCategory.One)]
    [InlineData("en", "1", PluralCategory.One)]
    [InlineData("en", "1.0", PluralCategory.Other)]
    [InlineData("en", "21", PluralCategory.Other)]
    [Trait("Req", "IDI-004")]
    public void Spanish_and_English_use_the_one_and_other_rules(
        string code,
        string value,
        PluralCategory expected
    )
    {
        var locale = I18nRepository
            .Locales()
            .Single(l => string.Equals(l.Code, code, StringComparison.Ordinal));

        locale
            .PluralRules.Select(
                PluralOperands.FromDecimalNumber(decimal.Parse(value, CultureInfo.InvariantCulture))
            )
            .ShouldBe(expected);
    }

    /// <summary>Numbers after <c>@integer</c>/<c>@decimal</c>; ranges (<c>2~4</c>) give both ends, <c>…</c> is skipped.</summary>
    private static IEnumerable<decimal> Samples(string condition)
    {
        var at = condition.IndexOf('@', StringComparison.Ordinal);
        if (at < 0)
        {
            yield break;
        }

        foreach (
            var token in condition[at..]
                .Split([' ', ',', '~'], StringSplitOptions.RemoveEmptyEntries)
        )
        {
            if (
                decimal.TryParse(
                    token,
                    NumberStyles.AllowDecimalPoint,
                    CultureInfo.InvariantCulture,
                    out var value
                )
            )
            {
                yield return value;
            }
        }
    }
}
