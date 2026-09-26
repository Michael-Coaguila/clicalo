namespace Clicalo.Tools.SpikeLab.Scripting;

/// <summary>What counts one repetition of a step.</summary>
internal enum StepTrigger
{
    /// <summary>The maintainer counts: each «Funcionó» or «Falló» is one repetition.</summary>
    Manual,

    /// <summary>An accepted tap (finger, pen or mouse) on a surface under test.</summary>
    SurfaceTap,

    /// <summary>A completed drag of the panel by its handle (moves between monitors, S1 row 30).</summary>
    HandleDrag,

    /// <summary>A UI Automation command on a panel tile: Invoke, Toggle, Expand or Collapse (S3).</summary>
    UiaCommand,

    /// <summary>A foreground lease that was requested and, if granted, given back (S4).</summary>
    LeaseCycle,

    /// <summary>A forced activation of the panel without a lease, detected and reverted (S1 row 31).</summary>
    ForcedActivation,
}
