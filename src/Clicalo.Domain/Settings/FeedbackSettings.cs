namespace Clicalo.Domain.Settings;

/// <summary>Confirmation after a tap (GEN-011, EJE-012).</summary>
/// <param name="Sound">Soft sound.</param>
/// <param name="Flash">Colour flash of the tile.</param>
public sealed record FeedbackSettings(bool Sound, bool Flash);
