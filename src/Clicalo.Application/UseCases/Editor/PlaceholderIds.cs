using Clicalo.Domain.Primitives;

namespace Clicalo.Application.UseCases.Editor;

/// <summary>
/// The ids of content copied for <c>CreateShortcut</c>, which replaces them with new ones from the store's generator
/// (DAT-004): a fixed placeholder, never persisted.
/// </summary>
internal sealed class PlaceholderIds : IIdGenerator
{
    private PlaceholderIds() { }

    /// <summary>The single instance.</summary>
    public static PlaceholderIds Instance { get; } = new();

    /// <inheritdoc />
    public ProfileId NewProfileId() => new("placeholder");

    /// <inheritdoc />
    public ShortcutId NewShortcutId() => new("placeholder");
}
