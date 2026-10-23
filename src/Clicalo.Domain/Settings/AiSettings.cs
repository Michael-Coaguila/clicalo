namespace Clicalo.Domain.Settings;

/// <summary>AI settings (docs/02 <c>ai</c>, PLA-003, PLA-004). The key itself never enters the document (ADR-0008).</summary>
public sealed record AiSettings
{
    /// <summary>Whether the user accepted what is sent.</summary>
    public required bool Consent { get; init; }

    /// <summary>Whether the AI is switched off.</summary>
    public required bool Disabled { get; init; }

    /// <summary>Free requests left today.</summary>
    public required int FreeLeftToday { get; init; }

    /// <summary>When the free quota resets.</summary>
    public required DateTimeOffset? FreeResetAt { get; init; }

    /// <summary>Credential Manager target of the user's key, or <see langword="null"/> without one.</summary>
    public required string? ApiKeyRef { get; init; }

    /// <summary>
    /// The provider (ADR-0033), persisted as <c>settings.aiProvider</c> since schema 1.2. The consent belongs to the
    /// provider: changing it clears <see cref="Consent"/> in the same step.
    /// </summary>
    public required AiProvider Provider { get; init; }
}
