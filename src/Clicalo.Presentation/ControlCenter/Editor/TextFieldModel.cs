namespace Clicalo.Presentation.ControlCenter.Editor;

/// <summary>The text of a Text shortcut (EDI-011); the text itself is read with the editor, never kept here.</summary>
/// <param name="Label">[textLabel].</param>
/// <param name="Hint">[textHint].</param>
/// <param name="DictateName">The accessible name of the dictation button.</param>
/// <param name="Encrypted">[textEnc].</param>
/// <param name="Version">Changes whenever the saved text changes.</param>
public sealed record TextFieldModel(
    string Label,
    string Hint,
    string DictateName,
    string Encrypted,
    int Version
);
