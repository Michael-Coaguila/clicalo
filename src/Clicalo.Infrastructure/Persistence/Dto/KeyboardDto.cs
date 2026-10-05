namespace Clicalo.Infrastructure.Persistence.Dto;

/// <summary>The keyboard layout and the language of the user's programs.</summary>
internal sealed record KeyboardDto
{
    public string? Layout { get; init; }

    public string? AppsLang { get; init; }

    public bool? Detected { get; init; }
}
