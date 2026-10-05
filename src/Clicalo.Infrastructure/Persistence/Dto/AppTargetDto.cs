namespace Clicalo.Infrastructure.Persistence.Dto;

/// <summary>What an App action opens: <c>exe</c> (path and arguments), <c>store</c> (AUMID), <c>document</c> or <c>raw</c>.</summary>
internal sealed record AppTargetDto
{
    public string? Kind { get; init; }

    public string? Path { get; init; }

    public string? Args { get; init; }

    public string? Aumid { get; init; }

    public string? Text { get; init; }
}
