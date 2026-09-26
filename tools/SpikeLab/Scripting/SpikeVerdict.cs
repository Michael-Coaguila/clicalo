namespace Clicalo.Tools.SpikeLab.Scripting;

/// <summary>The verdict of a whole script, following the decision rule of its document.</summary>
internal enum SpikeVerdict
{
    /// <summary>Some step is not finished yet, or was left incomplete.</summary>
    Incomplete,

    /// <summary>Every decisive step passed or does not apply.</summary>
    Passed,

    /// <summary>A decisive step failed.</summary>
    Failed,
}
