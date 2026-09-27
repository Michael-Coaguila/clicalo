namespace Clicalo.Presentation.Panel;

/// <summary>
/// How a tile reacts to the finger (blueprint §7.1, §8.6): the view picks the touch target kind and the UI Automation
/// pattern from it. Every other action kind behaves as <see cref="Tap"/> on the surface; the engine decides what it
/// runs (the planners of Text, Mouse, Macro, Web, App and System arrive in M3).
/// </summary>
public enum TileBehavior
{
    /// <summary>Runs when an accepted tap lifts (EJE-003); UI Automation Invoke.</summary>
    Tap,

    /// <summary>Holds while the contact lasts (EJE-004); UI Automation Toggle, latched when invoked (EJE-005).</summary>
    Hold,

    /// <summary>Each accepted tap latches or releases (EJE-007); UI Automation Toggle.</summary>
    Toggle,
}
