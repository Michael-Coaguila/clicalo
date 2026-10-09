namespace Clicalo.Presentation.ControlCenter.About;

/// <summary>A kind of feedback of the 2 × 2 grid (ACE-002).</summary>
/// <param name="Kind">The kind.</param>
/// <param name="Icon">Its Material Symbols icon.</param>
/// <param name="Label">Its text and accessible name.</param>
/// <param name="Selected">Whether it is the chosen one.</param>
public sealed record AboutOption(FeedbackKind Kind, string Icon, string Label, bool Selected);
