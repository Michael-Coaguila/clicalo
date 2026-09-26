namespace Clicalo.Domain.Library;

/// <summary>
/// The closed set of action types (docs/02 <c>type</c>). Every <see cref="ShortcutAction"/> subtype maps to one; the
/// facet test of blueprint §4.4 (mechanism 5) requires each kind to have a planner, a completeness rule, a DTO mapper,
/// an editor, a view and texts in ES and EN.
/// </summary>
public enum ActionKind
{
    /// <summary>Press a combination and release it (EJE-003).</summary>
    Tap,

    /// <summary>Keep a combination pressed while the contact lasts (EJE-004, EJE-005).</summary>
    Hold,

    /// <summary>Latch a combination on and off with successive taps (EJE-007).</summary>
    Toggle,

    /// <summary>Type or paste a text (EJE-008).</summary>
    Text,

    /// <summary>A mouse action at the last external pointer position (EJE-009).</summary>
    Mouse,

    /// <summary>A sequence of steps (EJE-010).</summary>
    Macro,

    /// <summary>Open a web address in the default browser (EJE-011).</summary>
    Url,

    /// <summary>Start an app, a Store app or a document without a command interpreter (EJE-011).</summary>
    App,

    /// <summary>A system action that cannot be sent as keys (EJE-016).</summary>
    System,
}
