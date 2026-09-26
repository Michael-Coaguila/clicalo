namespace Clicalo.Infrastructure.Persistence.Dto;

/// <summary>The combination to use when the programs of the user are in another language (CAT-005).</summary>
internal sealed record VariantDto
{
    public string? Lang { get; init; }

    public List<string>? Keys { get; init; }
}
