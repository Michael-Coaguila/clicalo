namespace Clicalo.Domain.Library;

/// <summary>Per-shortcut options.</summary>
/// <param name="Confirm">Ask for a second tap before running (EJE-002), for example Alt+F4.</param>
/// <param name="MaxHold">Automatic release limit of a Hold or Toggle (SEG-004).</param>
/// <param name="IsPrivate">Notices never show the text of a Text action (EJE-008).</param>
public sealed record ShortcutOptions(bool Confirm, HoldLimit MaxHold, bool IsPrivate);
