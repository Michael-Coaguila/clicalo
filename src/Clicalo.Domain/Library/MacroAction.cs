using Clicalo.Domain.Primitives;

namespace Clicalo.Domain.Library;

/// <summary>Run steps in order (EJE-010); a second tap cancels and releases everything the macro holds.</summary>
/// <param name="Steps">The steps; none means incomplete.</param>
public sealed record MacroAction(ValueList<MacroStep> Steps) : ShortcutAction
{
    /// <inheritdoc />
    public override ActionKind Kind => ActionKind.Macro;
}
