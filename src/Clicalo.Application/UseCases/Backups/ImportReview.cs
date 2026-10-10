using Clicalo.Domain.Document;
using Clicalo.Domain.Errors;
using Clicalo.Domain.Library;
using Clicalo.Domain.Primitives;

namespace Clicalo.Application.UseCases.Backups;

/// <summary>
/// A backup that is imported or restored is imported content, also when Clícalo itself wrote it (LOG-006, LOG-008): the
/// file may have been edited or come from someone else. Its Web, App and Macro shortcuts are shown one by one and only
/// the ones the person confirms are installed; the rest are left out of what Combinar, Reemplazar or Restaurar apply.
/// A shortcut that does exactly what one of the current document already does is not new, so it needs no confirmation:
/// restoring a backup of one's own data asks nothing unless it brings a risky action the document does not have.
/// Pure; nothing is executed.
/// </summary>
public static class ImportReview
{
    /// <summary>Whether a shortcut opens something or runs several steps (Web, App or Macro, LOG-008).</summary>
    /// <param name="shortcut">A shortcut.</param>
    public static bool IsRisky(Shortcut shortcut)
    {
        ArgumentNullException.ThrowIfNull(shortcut);
        return shortcut.Action.Kind is ActionKind.Url or ActionKind.App or ActionKind.Macro;
    }

    /// <summary>
    /// The shortcuts of <paramref name="incoming"/> that need the person's confirmation, in the order of the document:
    /// the risky ones whose action no shortcut of <paramref name="current"/> has.
    /// </summary>
    /// <param name="current">The document in use.</param>
    /// <param name="incoming">The document read from the backup or the imported file.</param>
    public static ValueList<Shortcut> Pending(UserDocument current, UserDocument incoming)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(incoming);
        var known = current
            .Library.EnumerateShortcuts()
            .Select(located => located.Shortcut)
            .Where(IsRisky)
            .Select(shortcut => shortcut.Action)
            .ToHashSet();
        return ValueListBuilder.From(
            incoming
                .Library.EnumerateShortcuts()
                .Select(located => located.Shortcut)
                .Where(shortcut => IsRisky(shortcut) && !known.Contains(shortcut.Action))
        );
    }

    /// <summary>
    /// <paramref name="incoming"/> with only the pending shortcuts the person confirmed: every other pending one is
    /// left out. Pins and usage of what is left out dangle on purpose, as when a shortcut is deleted (FRE-005).
    /// </summary>
    /// <param name="current">The document in use.</param>
    /// <param name="incoming">The document read from the backup or the imported file.</param>
    /// <param name="confirmed">The pending shortcuts the person ticked.</param>
    public static Result<UserDocument> Confirmed(
        UserDocument current,
        UserDocument incoming,
        IReadOnlySet<ShortcutId> confirmed
    )
    {
        ArgumentNullException.ThrowIfNull(confirmed);
        var library = Results.Ok(incoming.Library);
        foreach (var shortcut in Pending(current, incoming))
        {
            if (!confirmed.Contains(shortcut.Id))
            {
                library = library.Bind(l => l.RemoveShortcut(shortcut.Id));
            }
        }

        return library.Map(l => ReferenceEquals(l, incoming.Library) ? incoming : incoming with { Library = l });
    }
}
