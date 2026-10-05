using System.Security.Cryptography;
using Clicalo.Domain.Primitives;

namespace Clicalo.App.Composition;

/// <summary>
/// The adapter of <see cref="IIdGenerator"/> (DAT-004): opaque ids from the cryptographic generator, never from the
/// name or the time, so renaming, duplicating or importing can never collide with an existing id.
/// </summary>
internal sealed class RandomIdGenerator : IIdGenerator
{
    /// <inheritdoc />
    public ProfileId NewProfileId() =>
        new("p-" + RandomNumberGenerator.GetHexString(16, lowercase: true));

    /// <inheritdoc />
    public ShortcutId NewShortcutId() =>
        new("s-" + RandomNumberGenerator.GetHexString(16, lowercase: true));
}
