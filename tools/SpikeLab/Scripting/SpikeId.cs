namespace Clicalo.Tools.SpikeLab.Scripting;

/// <summary>The blocking spikes of M1 that the laboratory runs (docs/testing/spikes/).</summary>
internal enum SpikeId
{
    /// <summary>S1 · No activation of the surfaces (S1.md).</summary>
    S1,

    /// <summary>S3 · UI Automation on a non-activatable window (S3.md).</summary>
    S3,

    /// <summary>S4 · Text input and foreground per origin (S4.md).</summary>
    S4,
}
