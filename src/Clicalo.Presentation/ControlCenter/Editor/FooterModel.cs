namespace Clicalo.Presentation.ControlCenter.Editor;

/// <summary>The footer of the editor (EDI-019).</summary>
/// <param name="TestText">[test].</param>
/// <param name="TestOpen">Whether the card is open.</param>
/// <param name="DuplicateText">[duplicate].</param>
/// <param name="DeleteText">[delete], or [delConfirm] when armed.</param>
/// <param name="DeleteArmed">Whether the first tap armed it.</param>
/// <param name="Saved">Whether the shortcut is in the document (a draft can be neither duplicated nor deleted).</param>
public sealed record FooterModel(
    string TestText,
    bool TestOpen,
    string DuplicateText,
    string DeleteText,
    bool DeleteArmed,
    bool Saved
);
