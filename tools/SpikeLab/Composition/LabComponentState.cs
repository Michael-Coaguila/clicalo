namespace Clicalo.Tools.SpikeLab.Composition;

/// <summary>Whether a real piece of the product works inside the laboratory.</summary>
internal enum LabComponentState
{
    /// <summary>It works.</summary>
    Ready,

    /// <summary>Its M1 package has not been integrated yet: it still throws <see cref="NotImplementedException"/>.</summary>
    Pending,

    /// <summary>It failed for another reason (see the detail).</summary>
    Failed,
}
