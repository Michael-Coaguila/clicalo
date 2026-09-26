namespace Clicalo.Build;

/// <summary>How strictly a build step compiles.</summary>
internal enum BuildMode
{
    /// <summary>Inner loop: Debug, restore allowed to update lock files.</summary>
    Debug,

    /// <summary>The CI gate: Release, after a locked restore, every MSBuild warning as an error.</summary>
    ReleaseGate,
}
