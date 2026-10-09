using Clicalo.Application.Ports;

namespace Clicalo.Infrastructure.Ai;

/// <summary>
/// The AI of the composition root (ADR-0014, user decision D5): the template generator with the person's own key and
/// the provider chosen here. Changing provider changes only this class and its adapter.
/// </summary>
public static class AiServices
{
    /// <summary>The Credential Manager target of the key (ADR-0008): <c>Clicalo/ai/{provider}</c>.</summary>
    public static string CredentialTarget { get; } = "Clicalo/ai/" + new AnthropicProvider().Id;

    /// <summary>The template generator.</summary>
    /// <param name="keys">The person's key.</param>
    public static ITemplateGenerator CreateGenerator(IAiKeyStore keys) =>
        new ByoKeyTemplateGenerator(keys, new AnthropicProvider());
}
