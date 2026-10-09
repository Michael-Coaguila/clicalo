namespace Clicalo.Presentation.ControlCenter;

/// <summary>The status bar of the Control Center (CCM-003): the last message, or [saved] at rest.</summary>
/// <param name="Icon">The icon of the message.</param>
/// <param name="Text">The message.</param>
/// <param name="IsWarning">Whether it is a warning (assertive).</param>
/// <param name="CanUndo">Whether [undo] shows.</param>
/// <param name="UndoText">[undo].</param>
/// <param name="UndoName">The accessible name of [undo]: what it undoes.</param>
public sealed record StatusModel(
    string Icon,
    string Text,
    bool IsWarning,
    bool CanUndo,
    string UndoText,
    string UndoName
);
