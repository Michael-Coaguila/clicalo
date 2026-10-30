namespace Clicalo.Application.Ports;

/// <summary>
/// Generates a template proposal with AI (PLA-002 to PLA-008, ADR-0014): sends only the four values of
/// <see cref="TemplateRequest"/> with the person's own key, waits at most <c>Timings.Ai.AiRequestTimeout</c> and checks
/// the structure of the answer against <c>data/schemas/ai-template.v1.schema.json</c>. The provider is chosen by the
/// adapter: changing it never touches the interface. Never throws for an expected failure.
/// </summary>
public interface ITemplateGenerator
{
    /// <summary>Asks for a proposal.</summary>
    /// <param name="request">The four values that leave the machine.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    ValueTask<TemplateGeneration> GenerateAsync(
        TemplateRequest request,
        CancellationToken cancellationToken
    );
}
