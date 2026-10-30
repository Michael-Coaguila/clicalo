using System.Collections.Frozen;
using System.Collections.Immutable;
using Clicalo.Application.Ports;
using Clicalo.Domain.Settings;

namespace Clicalo.Infrastructure.Ai;

/// <summary>
/// The AI of the composition root (ADR-0014, ADR-0033, user decision D5): the template generator with the person's own
/// key and the provider chosen in the document. Adding a provider changes only this class and its adapter.
/// </summary>
public static class AiServices
{
    /// <summary>
    /// The Credential Manager targets of every provider (ADR-0008): uninstalling with the data deleted deletes them all.
    /// </summary>
    public static ImmutableArray<string> CredentialTargets { get; } =
    [.. Enum.GetValues<AiProvider>().Select(CredentialTarget)];

    /// <summary>
    /// The Credential Manager target of the key of <paramref name="provider"/> (ADR-0008): <c>Clicalo/ai/{id}</c>, one
    /// per provider, so a key saved for one is never sent to another.
    /// </summary>
    /// <param name="provider">The provider.</param>
    public static string CredentialTarget(AiProvider provider) =>
        "Clicalo/ai/" + Adapter(provider).Id;

    /// <summary>The template generator of the chosen provider, each with its own key.</summary>
    /// <param name="chosen">The provider in the document, read for each generation.</param>
    /// <param name="keys">The key of a provider.</param>
    public static ITemplateGenerator CreateGenerator(
        Func<AiProvider> chosen,
        Func<AiProvider, IAiKeyStore> keys
    )
    {
        ArgumentNullException.ThrowIfNull(chosen);
        ArgumentNullException.ThrowIfNull(keys);
        return new ChosenProviderGenerator(
            chosen,
            Enum.GetValues<AiProvider>()
                .ToFrozenDictionary(
                    static provider => provider,
                    ITemplateGenerator (provider) =>
                        new ByoKeyTemplateGenerator(keys(provider), Adapter(provider))
                )
        );
    }

    private static IAiProvider Adapter(AiProvider provider) =>
        provider switch
        {
            AiProvider.Gemini => new GeminiProvider(),
            _ => new AnthropicProvider(),
        };
}
