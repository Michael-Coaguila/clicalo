namespace Clicalo.Domain.Execution;

/// <summary>Where an activation came from; every surface and origin goes through the same policy (EJE-001, TAC-002).</summary>
public enum ActivationOrigin
{
    /// <summary>A finger.</summary>
    Touch,

    /// <summary>A pen.</summary>
    Pen,

    /// <summary>A mouse.</summary>
    Mouse,

    /// <summary>UI Automation Invoke: Voice Access, Narrator, Windows Speech Recognition (EJE-005, ACC-004).</summary>
    UiaInvoke,

    /// <summary>The keyboard mode (a key on a focused tile).</summary>
    Keyboard,

    /// <summary>The Repeat button (AVI-004).</summary>
    Repeat,
}
