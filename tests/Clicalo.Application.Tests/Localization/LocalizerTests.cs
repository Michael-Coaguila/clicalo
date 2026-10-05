using Clicalo.Application.Localization;
using Clicalo.Domain.Messages;

namespace Clicalo.Application.Tests.Localization;

public sealed class LocalizerTests
{
    private static readonly Localizer Es = I18nRepository.Localizer("es");
    private static readonly Localizer En = I18nRepository.Localizer("en");

    [Theory]
    [InlineData(0, "0 teclas · se guarda solo", "0 keys · auto-saved")]
    [InlineData(1, "1 tecla · se guarda solo", "1 key · auto-saved")]
    [InlineData(2, "2 teclas · se guarda solo", "2 keys · auto-saved")]
    [InlineData(-1, "-1 tecla · se guarda solo", "-1 key · auto-saved")]
    [Trait("Req", "IDI-004")]
    public void Plural_forms_follow_the_CLDR_rules_of_each_language(
        long count,
        string spanish,
        string english
    )
    {
        Es.Format(L.ComboN(count)).ShouldBe(spanish);
        En.Format(L.ComboN(count)).ShouldBe(english);
    }

    [Theory]
    [InlineData(1, "Para 1 tecla · se guarda solo", "For 1 key · auto-saved")]
    [InlineData(3, "Para 3 teclas · se guarda solo", "For 3 keys · auto-saved")]
    [Trait("Req", "IDI-004")]
    public void A_nested_plural_message_chooses_its_own_form(
        long count,
        string spanish,
        string english
    )
    {
        var message = L.LcFor(L.ComboN(count));

        Es.Format(message).ShouldBe(spanish);
        En.Format(message).ShouldBe(english);
    }

    [Theory]
    [InlineData("1.5", "Zoom 1,5", "Zoom 1.5")]
    [InlineData("0.50", "Zoom 0,50", "Zoom 0.50")]
    [InlineData("-0.25", "Zoom -0,25", "Zoom -0.25")]
    [InlineData("1234.5", "Zoom 1234,5", "Zoom 1234.5")]
    [InlineData("3", "Zoom 3", "Zoom 3")]
    [Trait("Req", "IDI-004")]
    public void Decimal_numbers_use_the_separator_of_locales_json(
        string value,
        string spanish,
        string english
    )
    {
        var number = decimal.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
        var message = new Message(
            new MessageKey("zoom"),
            MessageArgument.DecimalNumber("ratio", number)
        );

        Synthetic("es", ("zoom", "Zoom {ratio}")).Format(message).ShouldBe(spanish);
        Synthetic("en", ("zoom", "Zoom {ratio}")).Format(message).ShouldBe(english);
    }

    [Theory]
    [InlineData("1", "1 tecla", "1 key")]
    [InlineData("1.0", "1,0 tecla", "1.0 keys")]
    [InlineData("1.5", "1,5 teclas", "1.5 keys")]
    [InlineData("0.0", "0,0 teclas", "0.0 keys")]
    public void Decimal_counts_select_plurals_with_their_visible_digits(
        string value,
        string spanish,
        string english
    )
    {
        var count = decimal.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
        var message = new Message(
            new MessageKey("keys"),
            MessageArgument.DecimalNumber("count", count)
        );

        Synthetic("es", ("keys_one", "{count} tecla"), ("keys_other", "{count} teclas"))
            .Format(message)
            .ShouldBe(spanish);
        Synthetic("en", ("keys_one", "{count} key"), ("keys_other", "{count} keys"))
            .Format(message)
            .ShouldBe(english);
    }

    [Fact]
    public void Nested_messages_are_localized_in_the_language_of_the_outer_message()
    {
        var message = L.LcFor(L.Always);

        Es.Format(message).ShouldBe("Para Siempre visible");
        En.Format(message).ShouldBe("For Always visible");
        Es.Format(L.CreateFor(L.FgApp))
            .ShouldBe("Crear para aplicación en primer plano — el panel detecta cuál es");
    }

    [Fact]
    public void Text_arguments_are_shown_verbatim_even_with_braces()
    {
        Es.Format(L.CreateFor("{app} & <Word>")).ShouldBe("Crear para {app} & <Word>");
        En.Format(L.PanicMsg("Ctrl + Alt + Supr")).ShouldBe("Held: Ctrl + Alt + Supr");
    }

    [Fact]
    public void Leading_and_trailing_spaces_of_a_text_are_kept() =>
        Es.Format(L.CopySuffix).ShouldBe(" (copia)");

    [Fact]
    public void A_missing_argument_is_shown_as_its_placeholder_instead_of_failing() =>
        Es.Format(new Message(new MessageKey("createFor"))).ShouldBe("Crear para {app}");

    [Fact]
    public void A_plural_family_without_count_uses_its_other_form() =>
        Es.Format(new Message(new MessageKey("comboN")))
            .ShouldBe("{count} teclas · se guarda solo");

    [Fact]
    public void An_unknown_key_is_shown_as_the_key_itself()
    {
        Es.Format(new Message(new MessageKey("noSuchKey"))).ShouldBe("noSuchKey");
        Es.Contains(new MessageKey("noSuchKey")).ShouldBeFalse();
    }

    [Fact]
    public void Keys_missing_in_a_language_fall_back_to_the_default_language()
    {
        var spanish = LanguagePack.Create(
            Es.Locale,
            [KeyValuePair.Create("onlySpanish", "Solo en español {count}")]
        );
        var english = LanguagePack.Create(En.Locale, []);
        var localizer = new Localizer(english, spanish);

        localizer.Contains(new MessageKey("onlySpanish")).ShouldBeTrue();
        localizer
            .Format(
                new Message(new MessageKey("onlySpanish"), MessageArgument.WholeNumber("count", 2))
            )
            .ShouldBe("Solo en español 2");
    }

    [Fact]
    public void Every_generated_key_has_a_text_in_every_language()
    {
        foreach (var descriptor in MessageCatalog.All)
        {
            Es.Contains(descriptor.Key).ShouldBeTrue(descriptor.Key.Value);
            En.Contains(descriptor.Key).ShouldBeTrue(descriptor.Key.Value);
        }
    }

    private static Localizer Synthetic(string code, params (string Key, string Text)[] entries)
    {
        var locale = string.Equals(code, "es", StringComparison.Ordinal) ? Es.Locale : En.Locale;
        return new Localizer(
            LanguagePack.Create(
                locale,
                entries.Select(static e => KeyValuePair.Create(e.Key, e.Text))
            )
        );
    }
}
