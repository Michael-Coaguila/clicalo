using Clicalo.Domain.Primitives;

namespace Clicalo.Presentation.ControlCenter.Templates;

/// <summary>A button of «Tus perfiles» (PLA-014).</summary>
/// <param name="Id">The profile.</param>
/// <param name="Icon">Its icon.</param>
/// <param name="Name">Its name.</param>
/// <param name="Count">Its number of shortcuts.</param>
/// <param name="CountName">«N atajos», for the accessible name (IDI-004).</param>
public sealed record InstalledProfile(
    ProfileId Id,
    string Icon,
    string Name,
    string Count,
    string CountName
);
