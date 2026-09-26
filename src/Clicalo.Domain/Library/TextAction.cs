using Clicalo.Domain.Privacy;

namespace Clicalo.Domain.Library;

/// <summary>Type or paste a text (EJE-008). The text is a <see cref="SecretText"/>, encrypted on disk (LOG-003).</summary>
/// <param name="Text">The text; empty or unavailable means incomplete.</param>
/// <param name="Method">How it is sent.</param>
public sealed record TextAction(SecretText Text, TextMethod Method) : ShortcutAction
{
    /// <inheritdoc />
    public override ActionKind Kind => ActionKind.Text;
}
