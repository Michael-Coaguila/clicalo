using Clicalo.Domain.Catalog;

namespace Clicalo.Domain.Library;

/// <summary>A system action such as «Lock the computer», which replaces Win+L (EJE-014, EJE-016).</summary>
/// <param name="Command">The command.</param>
public sealed record SystemAction(SystemCommandId Command) : ShortcutAction
{
    /// <inheritdoc />
    public override ActionKind Kind => ActionKind.System;
}
