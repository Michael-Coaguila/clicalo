namespace Clicalo.Presentation.Panel.EditMode;

/// <summary>
/// Where edit mode, the context menu, test mode and Quick settings send their notices (AVI-001, AVI-002): the
/// composition implements it with the notice bar of the panel (and the status bar of the control center for the
/// actions that live there). Texts stay messages and are localized when painted (IDI-001).
/// </summary>
public interface IPanelNoticeSink
{
    /// <summary>
    /// A notice that lasts <c>Timings.Notices.NoticeDuration</c>, or <c>UndoNoticeDuration</c> when it offers [undo]
    /// (AVI-002, AVI-005); it replaces the notice on show.
    /// </summary>
    /// <param name="notice">The notice.</param>
    void Notify(PanelNotice notice);

    /// <summary>
    /// A sticky notice ([editHint], [tmStart]): it stays until another notice replaces it or its owner clears it
    /// (AVI-002).
    /// </summary>
    /// <param name="owner">Who shows it; only the same owner clears it.</param>
    /// <param name="notice">The notice.</param>
    void ShowSticky(object owner, PanelNotice notice);

    /// <summary>
    /// The state of <paramref name="owner"/> ended (edit mode left, test mode switched off by hand): its sticky notice
    /// goes away if it is still on show; any other notice stays.
    /// </summary>
    /// <param name="owner">The owner given to <see cref="ShowSticky"/>.</param>
    void ClearSticky(object owner);
}
