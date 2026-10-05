namespace Clicalo.Infrastructure.Persistence.Dto;

/// <summary>Key safety (GEN-012): <c>maxHoldMs</c> 0 is «Never» (docs/02 <c>maxHoldSec: 0</c>).</summary>
internal sealed record KeySafetyDto
{
    public double? MaxHoldMs { get; init; }

    public bool? ReleaseOnAppSwitch { get; init; }
}
