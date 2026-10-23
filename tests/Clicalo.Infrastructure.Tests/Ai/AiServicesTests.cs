using Clicalo.Application.Ports;
using Clicalo.Domain.Primitives;
using Clicalo.Domain.Privacy;
using Clicalo.Domain.Settings;
using Clicalo.Infrastructure.Ai;

namespace Clicalo.Infrastructure.Tests.Ai;

/// <summary>
/// The AI of the composition root with two providers (ADR-0033): one Credential Manager target each, the one of
/// Anthropic unchanged so a key saved before keeps working, and a generation that only reads the key of the chosen
/// provider. No test here reaches the network: the chosen provider has no key, so nothing is sent.
/// </summary>
[Trait("Req", "PLA-003")]
public sealed class AiServicesTests
{
    [Fact]
    public void Every_provider_has_its_own_target_and_anthropic_keeps_the_one_it_had()
    {
        AiServices.CredentialTarget(AiProvider.Anthropic).ShouldBe("Clicalo/ai/anthropic");
        AiServices.CredentialTarget(AiProvider.Gemini).ShouldBe("Clicalo/ai/gemini");
        AiServices.CredentialTargets.ShouldBe(
            ["Clicalo/ai/anthropic", "Clicalo/ai/gemini"],
            ignoreOrder: true
        );
    }

    [Theory]
    [Trait("Req", "PLA-008")]
    [InlineData(AiProvider.Anthropic, AiProvider.Gemini)]
    [InlineData(AiProvider.Gemini, AiProvider.Anthropic)]
    public async Task A_generation_reads_only_the_key_of_the_chosen_provider(
        AiProvider chosen,
        AiProvider other
    )
    {
        var stores = new Dictionary<AiProvider, CountingKeys>
        {
            [chosen] = new(key: null),
            [other] = new(key: "a-key-of-the-other-provider"),
        };
        var generator = AiServices.CreateGenerator(() => chosen, provider => stores[provider]);

        var generation = await generator.GenerateAsync(
            new TemplateRequest("WhatsApp", "es-LA", LangCode.En, LangCode.Es),
            CancellationToken.None
        );

        generation.Failure.ShouldBe(AiFailure.NoKey);
        stores[chosen].Reads.ShouldBe(1);
        stores[other].Reads.ShouldBe(0, "a key saved for one provider is never sent to another");
    }

    private sealed class CountingKeys(string? key) : IAiKeyStore
    {
        public int Reads { get; private set; }

        public string Target => "Clicalo/ai/test";

        public bool HasKey() => key is not null;

        public bool Save(Sensitive<string> value) => false;

        public Sensitive<string>? Read()
        {
            Reads++;
            return key is null ? null : new Sensitive<string>(key, RedactionKind.Secret);
        }

        public bool Delete() => true;
    }
}
