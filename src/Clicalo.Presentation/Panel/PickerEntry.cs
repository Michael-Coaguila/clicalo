using Clicalo.Domain.Catalog;
using Clicalo.Domain.Primitives;

namespace Clicalo.Presentation.Panel;

/// <summary>One profile as the selector and the profile grid show it (SEL-001, SEL-003).</summary>
/// <param name="Id">The profile.</param>
/// <param name="Name">Its name in the interface language (user data, shown as is).</param>
/// <param name="Icon">Its icon.</param>
public sealed record PickerEntry(ProfileId Id, string Name, IconRef Icon);
