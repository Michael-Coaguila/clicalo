namespace Clicalo.Infrastructure.Persistence.Dto;

/// <summary>The AI settings; the key itself lives in the Credential Manager (ADR-0008), only its reference here.</summary>
internal sealed record AiDto
{
    public bool? Consent { get; init; }

    public bool? Disabled { get; init; }

    public int? FreeLeftToday { get; init; }

    public DateTimeOffset? FreeResetAt { get; init; }

    public string? ApiKeyRef { get; init; }
}
