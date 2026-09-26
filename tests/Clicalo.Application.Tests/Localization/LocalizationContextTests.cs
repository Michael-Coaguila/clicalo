using Clicalo.Application.Localization;
using Clicalo.Domain.Messages;

namespace Clicalo.Application.Tests.Localization;

public sealed class LocalizationContextTests
{
    [Fact]
    public void Starts_in_the_default_language_and_offers_every_language_in_order()
    {
        var context = I18nRepository.Context();

        context.Current.Locale.Code.ShouldBe("es");
        context.Languages.Select(static l => l.NativeName).ShouldBe(["Español", "English"]);
    }

    [Fact]
    [Trait("Req", "IDI-001")]
    public void Switching_language_raises_one_event_and_repaints_the_same_message_in_the_new_language()
    {
        var context = I18nRepository.Context();
        var notice = L.ComboN(1);
        var events = new List<LanguageChangedEventArgs>();
        context.LanguageChanged += (_, e) => events.Add(e);

        context.Current.Format(notice).ShouldBe("1 tecla · se guarda solo");
        context.TrySetLanguage("en").ShouldBeTrue();

        events.Count.ShouldBe(1);
        events[0].Previous.Locale.Code.ShouldBe("es");
        events[0].Current.ShouldBeSameAs(context.Current);
        context.Current.Format(notice).ShouldBe("1 key · auto-saved");
    }

    [Fact]
    public void Setting_the_current_language_again_raises_nothing()
    {
        var context = I18nRepository.Context("en");
        var raised = 0;
        context.LanguageChanged += (_, _) => raised++;

        context.TrySetLanguage("en").ShouldBeTrue();

        raised.ShouldBe(0);
    }

    [Fact]
    public void An_unknown_language_is_refused_and_nothing_changes()
    {
        var context = I18nRepository.Context();
        var before = context.Current;

        context.TrySetLanguage("fr").ShouldBeFalse();

        context.Current.ShouldBeSameAs(before);
    }

    [Fact]
    public void An_unavailable_initial_language_falls_back_to_the_default() =>
        I18nRepository.Context("fr").Current.Locale.Code.ShouldBe("es");

    [Fact]
    public void The_default_language_must_have_a_pack() =>
        Should.Throw<ArgumentException>(() => new LocalizationContext([], "es"));

    [Fact]
    public async Task Concurrent_switches_always_leave_a_consistent_snapshot()
    {
        var context = I18nRepository.Context();
        var codes = new[] { "es", "en" };

        await Parallel.ForAsync(
            0,
            1000,
            TestContext.Current.CancellationToken,
            (i, _) =>
            {
                context.TrySetLanguage(codes[i % 2]);
                context
                    .Current.Format(L.ComboN(2))
                    .ShouldBeOneOf("2 teclas · se guarda solo", "2 keys · auto-saved");
                return ValueTask.CompletedTask;
            }
        );
    }
}
