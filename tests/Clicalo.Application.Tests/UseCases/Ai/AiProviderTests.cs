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
/// Two AI providers, each with its own key and its own consent (ADR-0033): choosing another provider clears the consent
/// in the same undo step, so nothing is sent to it before the person accepts its text, and the key pasted, read or
/// deleted is always the one of the chosen provider.
/// </summary>
[Trait("Req", "PLA-004")]
public sealed class AiProviderTests
{
    [Fact]
    public async Task Choosing_another_provider_asks_for_the_consent_again_and_one_undo_brings_both_back()
    {
        var (ai, harness, generator, _) = Create(consent: true);

        ai.SetProvider(AiProvider.Gemini).ShouldBeTrue();

        harness.Store.Current.Settings.Ai.Provider.ShouldBe(AiProvider.Gemini);
        harness.Store.Current.Settings.Ai.Consent.ShouldBeFalse(
            "a consent belongs to its provider"
        );
        ai.Provider.ShouldBe(AiProvider.Gemini);
        (await Generate(ai)).ShouldBeNull();
        ai.AskingConsent.ShouldBeTrue();
        generator.Requests.ShouldBe(0, "nothing is sent to Google before its consent");

        harness.Store.Undo().IsSuccess.ShouldBeTrue();
        harness.Store.Current.Settings.Ai.Provider.ShouldBe(AiProvider.Anthropic);
        harness.Store.Current.Settings.Ai.Consent.ShouldBeTrue();
    }

    [Fact]
    public void Choosing_the_same_provider_changes_nothing()
    {
        var (ai, harness, _, _) = Create(consent: true);

        ai.SetProvider(AiProvider.Anthropic).ShouldBeFalse();

        harness.Store.Current.Settings.Ai.Consent.ShouldBeTrue();
        harness.Store.CanUndo.ShouldBeFalse();
    }

    [Fact]
    [Trait("Req", "PLA-003")]
    public void Each_provider_keeps_its_own_key()
    {
        var (ai, harness, _, stores) = Create(consent: true);
        ai.SaveKey(new Sensitive<string>("sk-ant-test", RedactionKind.Secret)).ShouldBeTrue();
        harness.Store.Current.Settings.Ai.ApiKeyRef.ShouldBe("Clicalo/ai/anthropic");

        ai.SetProvider(AiProvider.Gemini).ShouldBeTrue();
        ai.HasKey.ShouldBeFalse("the key of Anthropic is not the key of Gemini");
        harness.Store.Current.Settings.Ai.ApiKeyRef.ShouldBeNull();
        ai.SaveKey(new Sensitive<string>("AIza-test", RedactionKind.Secret)).ShouldBeTrue();
        stores[AiProvider.Gemini].Read()!.Value.Value.ShouldBe("AIza-test");
        stores[AiProvider.Anthropic].Read()!.Value.Value.ShouldBe("sk-ant-test");
        harness.Store.Current.Settings.Ai.ApiKeyRef.ShouldBe("Clicalo/ai/gemini");

        ai.DeleteKey();
        stores[AiProvider.Gemini].HasKey().ShouldBeFalse();
        stores[AiProvider.Anthropic]
            .HasKey()
            .ShouldBeTrue("deleting one key never touches the other");
        harness.Store.Current.Settings.Ai.ApiKeyRef.ShouldBeNull();

        ai.SetProvider(AiProvider.Anthropic).ShouldBeTrue();
        ai.HasKey.ShouldBeTrue();
        harness.Store.Current.Settings.Ai.ApiKeyRef.ShouldBe("Clicalo/ai/anthropic");
    }

    [Fact]
    [Trait("Req", "PLA-005")]
    public async Task With_the_consent_of_the_new_provider_and_without_its_key_it_asks_for_the_key()
    {
        var (ai, _, generator, _) = Create(consent: true);
        _ = ai.SaveKey(new Sensitive<string>("sk-ant-test", RedactionKind.Secret));
        ai.SetProvider(AiProvider.Gemini).ShouldBeTrue();
        ai.AcceptConsent();

        (await Generate(ai)).ShouldBeNull();

        ai.Error.ShouldBe(AiError.NoKey);
        generator.Requests.ShouldBe(0);
    }

    private static (
        AiAssistant Ai,
        StoreHarness Harness,
        CountingGenerator Generator,
        Dictionary<AiProvider, MemoryKeys> Stores
    ) Create(bool consent)
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
        var stores = new Dictionary<AiProvider, MemoryKeys>
        {
            [AiProvider.Anthropic] = new("Clicalo/ai/anthropic"),
            [AiProvider.Gemini] = new("Clicalo/ai/gemini"),
        };
        var keys = new ChosenProviderKeyStore(
            () => harness.Store.Current.Settings.Ai.Provider,
            provider => stores[provider]
        );
        var generator = new CountingGenerator();
        return (
            new AiAssistant(harness.Store, generator, keys, harness.Time),
            harness,
            generator,
            stores
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

    private sealed class CountingGenerator : ITemplateGenerator
    {
        public int Requests { get; private set; }

        public ValueTask<TemplateGeneration> GenerateAsync(
            TemplateRequest request,
            CancellationToken cancellationToken
        )
        {
            Requests++;
            return ValueTask.FromResult(TemplateGeneration.Fail(AiFailure.Unavailable));
        }
    }

    private sealed class MemoryKeys(string target) : IAiKeyStore
    {
        private Sensitive<string>? _key;

        public string Target => target;

        public bool HasKey() => _key is not null;

        public bool Save(Sensitive<string> key)
        {
            _key = key;
            return true;
        }

        public Sensitive<string>? Read() => _key;

        public bool Delete()
        {
            _key = null;
            return true;
        }
    }
}
