using Clicalo.Domain.Primitives;

namespace Clicalo.Presentation.ControlCenter.Editor;

/// <summary>A step of a macro (EDI-013).</summary>
/// <param name="Index">Its zero-based position.</param>
/// <param name="Number">Its number.</param>
/// <param name="Icon">The icon of its kind.</param>
/// <param name="Text">Its description.</param>
/// <param name="Editor">What it edits when open.</param>
/// <param name="CanUp">Whether it can go up.</param>
/// <param name="CanDown">Whether it can go down.</param>
/// <param name="Armed">Whether its delete button was tapped once.</param>
/// <param name="Wait">The duration of a wait.</param>
/// <param name="Mouse">The mouse chips of an open mouse step.</param>
/// <param name="TextVersion">Changes whenever the text of a text step changes.</param>
public sealed record StepRow(
    int Index,
    string Number,
    string Icon,
    string Text,
    StepEditorKind Editor,
    bool CanUp,
    bool CanDown,
    bool Armed,
    string Wait,
    ValueList<MouseOption> Mouse,
    int TextVersion
);
