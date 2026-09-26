using Clicalo.Domain.Messages;

namespace Clicalo.Domain.Settings;

/// <summary>
/// Everything the product knows about one setting (blueprint §6.3): the single source of its default, clamping on
/// load, v1 conversion, the simple rows of the UI and the tests (every leaf of <see cref="UserSettings"/> has a
/// descriptor and texts in ES and EN).
/// </summary>
/// <param name="Path">Dotted path in the persisted settings (<c>keySafety.maxHoldSec</c>).</param>
/// <param name="Scope">What it affects.</param>
/// <param name="Undoable">Whether changing it records an undo entry (REG-07, <c>undo-exemptions.json</c>).</param>
/// <param name="Default">Default value.</param>
/// <param name="Range">Allowed range of a numeric setting.</param>
/// <param name="Label">Label key in <c>data/i18n</c>.</param>
/// <param name="Description">Description key in <c>data/i18n</c>.</param>
public sealed record SettingDescriptor(
    string Path,
    SettingScope Scope,
    bool Undoable,
    object Default,
    SettingRange? Range,
    MessageKey Label,
    MessageKey? Description
);
