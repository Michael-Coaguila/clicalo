using System.Collections.Frozen;
using Clicalo.Application.Ports;
using Clicalo.Domain.Settings;

namespace Clicalo.Infrastructure.Ai;

/// <summary>
/// <see cref="ITemplateGenerator"/> of the provider chosen in the document (ADR-0033): the choice is read once per
/// generation, so its key and its adapter always belong to the same provider.
/// </summary>
/// <param name="chosen">The provider in the document.</param>
/// <param name="generators">One generator per provider.</param>
internal sealed class ChosenProviderGenerator(
    Func<AiProvider> chosen,
    FrozenDictionary<AiProvider, ITemplateGenerator> generators
) : ITemplateGenerator
{
    /// <inheritdoc />
    public ValueTask<TemplateGeneration> GenerateAsync(
        TemplateRequest request,
        CancellationToken cancellationToken
    ) =>
        generators.TryGetValue(chosen(), out var generator)
            ? generator.GenerateAsync(request, cancellationToken)
            : ValueTask.FromResult(TemplateGeneration.Fail(AiFailure.Unavailable));
}
