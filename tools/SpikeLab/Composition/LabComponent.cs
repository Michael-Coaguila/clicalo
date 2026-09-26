namespace Clicalo.Tools.SpikeLab.Composition;

/// <summary>The state of one real piece composed by the laboratory, as the control window and the report list it.</summary>
/// <param name="Name">The piece (type name of the product).</param>
/// <param name="State">Its state.</param>
/// <param name="Detail">Why it is not ready, or what it does; never user content.</param>
internal sealed record LabComponent(string Name, LabComponentState State, string Detail);
