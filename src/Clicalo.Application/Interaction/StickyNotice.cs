namespace Clicalo.Application.Interaction;

/// <summary>
/// A fixed notice of the <see cref="NoticeQueue"/> (AVI-002): it lasts while the state of <paramref name="Owner"/>
/// lasts (a Mantener under a finger, a running macro, edit mode, test mode, the capture of an app).
/// </summary>
/// <param name="Owner">Whose state it tells; compared by reference.</param>
/// <param name="Notice">The notice.</param>
public sealed record StickyNotice(object Owner, Notice Notice);
