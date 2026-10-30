namespace Clicalo.Presentation.ControlCenter.SystemSection;

/// <summary>«Reabrir como administrador» on demand (user decision D7): a row with its button, not a switch.</summary>
/// <param name="Title">[reopenAdmin].</param>
/// <param name="Description">[reopenAdminD], or [adminActive] when Clícalo already runs elevated.</param>
/// <param name="Button">[reopenBtn].</param>
/// <param name="CanReopen">Whether the button shows: not when already elevated nor while asking Windows.</param>
public sealed record AdminRowModel(string Title, string Description, string Button, bool CanReopen);
