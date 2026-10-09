using Clicalo.Domain.Primitives;

namespace Clicalo.Presentation.ControlCenter.Shortcuts;

/// <summary>The profiles column (ATJ-002).</summary>
/// <param name="Title">[profiles].</param>
/// <param name="Rows">Always visible and every profile in order.</param>
/// <param name="NewProfile">«+ [newProfile]».</param>
public sealed record ProfilesModel(string Title, ValueList<ProfileRow> Rows, string NewProfile);
