using Clicalo.Domain.Templates;

namespace Clicalo.Application.Ports;

/// <summary>The outcome of <see cref="ITemplateGenerator.GenerateAsync"/>: a proposal, or why there is none.</summary>
/// <param name="Proposal">The structurally valid answer; null on failure.</param>
/// <param name="Failure">Why there is no proposal; <see cref="AiFailure.None"/> with one.</param>
public sealed record TemplateGeneration(AiTemplateProposal? Proposal, AiFailure Failure)
{
    /// <summary>A proposal.</summary>
    /// <param name="proposal">The answer.</param>
    public static TemplateGeneration Ok(AiTemplateProposal proposal) =>
        new(proposal, AiFailure.None);

    /// <summary>No proposal.</summary>
    /// <param name="failure">Why.</param>
    public static TemplateGeneration Fail(AiFailure failure) => new(null, failure);
}
