namespace Clicalo.UI.Wpf.Surfaces;

/// <summary>What a first-time guide shows in one step (PES-015, R-38), in the current language.</summary>
/// <param name="Name">The window for UI Automation, the same in every step.</param>
/// <param name="StepLabel">«i / 3».</param>
/// <param name="Title">The title of the step.</param>
/// <param name="Text">The text of the step; each ★ is drawn as the star icon.</param>
/// <param name="NextLabel">[next], or [understood] on the last step.</param>
/// <param name="SkipLabel">[coachSkip].</param>
/// <param name="Steps">The title and the text of every step, to keep the size of the longest.</param>
public sealed record CoachContent(
    string Name,
    string StepLabel,
    string Title,
    string Text,
    string NextLabel,
    string SkipLabel,
    IReadOnlyList<(string Title, string Text)> Steps
);
