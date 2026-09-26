using Clicalo.Domain.Errors;
using Clicalo.Domain.Library;
using Clicalo.Domain.Primitives;

namespace Clicalo.Domain.Commands;

/// <summary>
/// New ids for created, duplicated and installed elements (DAT-004): taken from <see cref="IIdGenerator"/> and checked
/// against the whole document, so a faulty generator fails the command instead of breaking invariant I1.
/// </summary>
internal static class NewIds
{
    private const int Attempts = 64;

    public static Result<ShortcutId> Shortcut(
        ShortcutLibrary library,
        IIdGenerator ids,
        ISet<string>? taken = null
    )
    {
        for (var attempt = 0; attempt < Attempts; attempt++)
        {
            var id = ids.NewShortcutId();
            if (IsFree(library, id.Value, taken))
            {
                taken?.Add(id.Value);
                return Results.Ok(id);
            }
        }

        return Results.Fail<ShortcutId>(CommandFailures.IdsExhausted());
    }

    public static Result<ProfileId> Profile(
        ShortcutLibrary library,
        IIdGenerator ids,
        ISet<string>? taken = null
    )
    {
        for (var attempt = 0; attempt < Attempts; attempt++)
        {
            var id = ids.NewProfileId();
            if (IsFree(library, id.Value, taken))
            {
                taken?.Add(id.Value);
                return Results.Ok(id);
            }
        }

        return Results.Fail<ProfileId>(CommandFailures.IdsExhausted());
    }

    private static bool IsFree(ShortcutLibrary library, string? id, ISet<string>? taken) =>
        !string.IsNullOrEmpty(id)
        && !library.ContainsId(id)
        && (taken is null || !taken.Contains(id));
}
