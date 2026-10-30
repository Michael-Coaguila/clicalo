using Clicalo.Application.Ports;
using Clicalo.Application.Tests.Store;
using Clicalo.Application.UseCases.Ai;
using Clicalo.Domain.Document;
using Clicalo.Domain.Library;
using Clicalo.Domain.Primitives;
using Clicalo.Domain.Privacy;
using Clicalo.Domain.Settings;
using Clicalo.Domain.Templates;
using Clicalo.Domain.Tests.Generators;

namespace Clicalo.Application.Tests.UseCases.Ai;

/// <summary>
/// What the AI card of General does (GEN-015, PLA-004): the AI on or off and the consent given or revoked, without
/// opening the consent question of Plantillas.
/// </summary>
public sealed class AiSettingsTests
{
    [Fact]
    [Trait("Req", "GEN-015")]
    [Trait("Req", "PLA-004")]
    public async Task Turning_the_ai_off_in_general_leads_to_the_off_error_and_on_again_clears_it()
    {
        var (ai, harness) = Create(consent: true);

        ai.SetEnabled(false).ShouldBeTrue();
        harness.Store.Current.Settings.Ai.Disabled.ShouldBeTrue();
        (await Generate(ai)).ShouldBeNull();
        ai.Error.ShouldBe(AiError.Off);

        ai.SetEnabled(true).ShouldBeTrue();
        harness.Store.Current.Settings.Ai.Disabled.ShouldBeFalse();
        ai.Error.ShouldBe(AiError.None);
        ai.AskingConsent.ShouldBeFalse("General never opens the question of Plantillas");
    }

    [Fact]
    [Trait("Req", "GEN-015")]
    [Trait("Req", "PLA-004")]
    public async Task Revoking_the_consent_asks_again_before_the_next_attempt_and_it_can_be_undone()
    {
        var (ai, harness) = Create(consent: true);

        ai.SetConsent(false).ShouldBeTrue();
        harness.Store.Current.Settings.Ai.Consent.ShouldBeFalse();
        (await Generate(ai)).ShouldBeNull();
        ai.AskingConsent.ShouldBeTrue("nothing is sent without consent");

        harness.Store.Undo().IsSuccess.ShouldBeTrue();
        harness.Store.Current.Settings.Ai.Consent.ShouldBeTrue("revoking can be undone");
        ai.SetConsent(false).ShouldBeTrue();
        ai.SetConsent(true).ShouldBeTrue();
        harness.Store.Current.Settings.Ai.Consent.ShouldBeTrue();
        ai.AskingConsent.ShouldBeFalse();
    }

    private static (AiAssistant Ai, StoreHarness Harness) Create(bool consent)
    {
        var defaults = SettingsSchema.Defaults;
        var harness = new StoreHarness(
            UserDocument.Create(
                ShortcutLibrary.CreateValidated([], [DomainGen.General()]).Value,
                defaults with
                {
                    Ai = defaults.Ai with { Consent = consent },
                }
            )
        );
        return (
            new AiAssistant(harness.Store, new NoGenerator(), new NoKeys(), harness.Time),
            harness
        );
    }

    private static Task<AiTemplate?> Generate(AiAssistant ai) =>
        ai.GenerateAsync(
            "WhatsApp",
            "es-LA",
            LangCode.Es,
            static _ => false,
            static _ => true,
            TestContext.Current.CancellationToken
        );

    private sealed class NoGenerator : ITemplateGenerator
    {
        public ValueTask<TemplateGeneration> GenerateAsync(
            TemplateRequest request,
            CancellationToken cancellationToken
        ) => ValueTask.FromResult(TemplateGeneration.Fail(AiFailure.Unavailable));
    }

    private sealed class NoKeys : IAiKeyStore
    {
        public string Target => "Clicalo/ai/test";

        public bool HasKey() => false;

        public bool Save(Sensitive<string> key) => false;

        public Sensitive<string>? Read() => null;

        public bool Delete() => true;
    }
}
