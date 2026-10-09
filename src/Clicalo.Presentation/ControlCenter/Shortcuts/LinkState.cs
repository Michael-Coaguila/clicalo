namespace Clicalo.Presentation.ControlCenter.Shortcuts;

/// <summary>The four states of the binding row (ATJ-005).</summary>
public enum LinkState
{
    /// <summary>General: used in any app without its own profile; no action.</summary>
    General,

    /// <summary>Waiting for the next app (capture mode), with Cancelar.</summary>
    Waiting,

    /// <summary>Bound to one or more apps, with Cambiar.</summary>
    Linked,

    /// <summary>Not bound, in warn, with Vincular.</summary>
    Unlinked,
}
