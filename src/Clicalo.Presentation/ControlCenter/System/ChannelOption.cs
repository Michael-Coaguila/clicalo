using Clicalo.Domain.Settings;

namespace Clicalo.Presentation.ControlCenter.SystemSection;

/// <summary>A button of the channel (ACT-002).</summary>
/// <param name="Channel">The channel.</param>
/// <param name="Label">[stable] or [beta].</param>
/// <param name="Selected">Whether it is the chosen one.</param>
public sealed record ChannelOption(UpdateChannel Channel, string Label, bool Selected);
