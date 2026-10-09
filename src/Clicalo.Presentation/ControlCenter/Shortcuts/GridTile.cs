using Clicalo.Domain.Primitives;

namespace Clicalo.Presentation.ControlCenter.Shortcuts;

/// <summary>A tile of 88 of the shortcuts grid (ATJ-009).</summary>
/// <param name="Id">The shortcut.</param>
/// <param name="Icon">Its icon.</param>
/// <param name="Category">Its color category.</param>
/// <param name="Name">Its name.</param>
/// <param name="Foot">Its keys or «n pasos», in monospace.</param>
/// <param name="Repeated">The warning sign when it is repeated (REP-003).</param>
/// <param name="TypeIcon">The icon of its kind when it is not Pulsar.</param>
/// <param name="Number">Its voice number, when the numbers show.</param>
/// <param name="Incomplete">«[incomplete]».</param>
/// <param name="Selected">Whether it is the one in the editor.</param>
/// <param name="AccessibleName">Its name for UI Automation, with its voice number.</param>
/// <param name="AccessibleState">Its state in words: repeated, incomplete.</param>
public sealed record GridTile(
    ShortcutId Id,
    string Icon,
    string Category,
    string Name,
    string Foot,
    bool Repeated,
    string? TypeIcon,
    string? Number,
    bool Incomplete,
    bool Selected,
    string AccessibleName,
    string AccessibleState
);
