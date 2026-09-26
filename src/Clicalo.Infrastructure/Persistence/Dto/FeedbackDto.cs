namespace Clicalo.Infrastructure.Persistence.Dto;

/// <summary>Sound and flash after a tap (GEN-011).</summary>
internal sealed record FeedbackDto
{
    public bool? Sound { get; init; }

    public bool? Flash { get; init; }
}
