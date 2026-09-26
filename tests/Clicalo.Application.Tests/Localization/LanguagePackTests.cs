using Clicalo.Application.Localization;
using Clicalo.Domain.Messages;

namespace Clicalo.Application.Tests.Localization;

public sealed class LanguagePackTests
{
    private static readonly LocaleInfo English = I18nRepository
        .Locales()
        .Single(static l => l.Code is "en");

    [Theory]
    [InlineData("Paso {index} de {total}", new[] { "index", "total" })]
    [InlineData("Llaves {{literales}} y {app}", new[] { "app" })]
    [InlineData("{count} {count}", new[] { "count" })]
    [InlineData("Sin marcadores", new string[0])]
    public void Templates_use_the_same_grammar_as_the_generator(string text, string[] placeholders)
    {
        MessageTemplate.TryParse(text, out var template, out _).ShouldBeTrue();

        template.Placeholders.ShouldBe(placeholders);
    }

    [Theory]
    [InlineData("abierto {")]
    [InlineData("cerrado }")]
    [InlineData("{Mayúscula}")]
    [InlineData("{con espacio}")]
    [InlineData("{}")]
    public void Templates_reject_stray_braces(string text) =>
        MessageTemplate.TryParse(text, out _, out _).ShouldBeFalse();

    [Fact]
    public void Doubled_braces_render_as_literal_braces()
    {
        var pack = LanguagePack.Create(English, [KeyValuePair.Create("k", "{{app}} is {app}")]);

        new Localizer(pack)
            .Format(new Message(new MessageKey("k"), MessageArgument.Text("app", "Word")))
            .ShouldBe("{app} is Word");
    }

    [Fact]
    public void Invalid_entries_are_skipped_and_reported_instead_of_failing()
    {
        var pack = LanguagePack.Create(
            English,
            [
                KeyValuePair.Create("good", "Good"),
                KeyValuePair.Create("bad-key", "Bad"),
                KeyValuePair.Create("empty", " "),
                KeyValuePair.Create("broken", "Broken {"),
                KeyValuePair.Create("few_few", "{count} few"),
                KeyValuePair.Create("orphan_one", "{count} orphan"),
                KeyValuePair.Create("mixed", "Mixed"),
                KeyValuePair.Create("mixed_other", "{count} mixed"),
            ]
        );

        pack.Count.ShouldBe(2);
        pack.Contains(new MessageKey("good")).ShouldBeTrue();
        pack.Contains(new MessageKey("mixed")).ShouldBeTrue();
        pack.Problems.Select(static p => p.Key)
            .ShouldBe(["bad-key", "empty", "broken", "few_few", "mixed_other", "orphan_other"]);
    }
}
