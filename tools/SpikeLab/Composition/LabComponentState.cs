namespace Clicalo.Tools.SpikeLab.Composition;

/// <summary>Whether a real piece of the product works inside the laboratory.</summary>
internal enum LabComponentState
{
    /// <summary>It works.</summary>
    Ready,

    /// <summary>It waits for another piece that did not start (see the detail).</summary>
    Pending,

    /// <summary>It failed for another reason (see the detail).</summary>
    Failed,
}
