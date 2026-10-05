using System.Collections.Frozen;
using Clicalo.Domain.Keys;
using Clicalo.Domain.Library;
using Clicalo.Domain.Primitives;

namespace Clicalo.Domain.CommonActions;

/// <summary>
/// The adaptive common actions (decision D4 of the user, 2026-10-05; <c>data/catalogs/common-actions.json</c>). A tap
/// shortcut installed from a common action (its catalog reference comes from one of <see cref="Origins"/> with the id
/// of an action) that still has a combination of the table is sent with the combination of the app in front and the
/// programs language; any app outside the table gets the standard one. A shortcut with a combination of its own is sent
/// as saved. Pure and immutable: the engine and the tiles use the same <see cref="ChordToSend"/>, so a tile always
/// shows what a tap sends to the app in front.
/// </summary>
public sealed class CommonActionTable
{
    private readonly FrozenSet<string> _origins;
    private readonly FrozenDictionary<string, CommonAction> _actions;

    /// <summary>Creates the table.</summary>
    /// <param name="origins">The catalog sources whose shortcuts are common actions (<c>seed</c>, <c>library</c>).</param>
    /// <param name="actions">The actions; an id that repeats keeps the first one.</param>
    public CommonActionTable(IEnumerable<string> origins, IEnumerable<CommonAction> actions)
    {
        ArgumentNullException.ThrowIfNull(origins);
        ArgumentNullException.ThrowIfNull(actions);
        _origins = origins.ToFrozenSet(StringComparer.Ordinal);
        var byId = new Dictionary<string, CommonAction>(StringComparer.Ordinal);
        foreach (var action in actions)
        {
            byId.TryAdd(action.Id, action);
        }

        _actions = byId.ToFrozenDictionary(StringComparer.Ordinal);
    }

    /// <summary>The table without actions: every shortcut is sent as saved.</summary>
    public static CommonActionTable Empty { get; } = new([], []);

    /// <summary>The catalog sources whose shortcuts are common actions.</summary>
    public IReadOnlyCollection<string> Origins => _origins;

    /// <summary>The actions.</summary>
    public IReadOnlyCollection<CommonAction> Actions => _actions.Values;

    /// <summary>
    /// The common action <paramref name="shortcut"/> follows: a tap whose catalog reference names one of the actions
    /// from one of <see cref="Origins"/> and whose saved combination is still one of the table.
    /// </summary>
    /// <param name="shortcut">The shortcut.</param>
    /// <param name="action">The action it follows.</param>
    public bool TryFind(Shortcut shortcut, out CommonAction action)
    {
        ArgumentNullException.ThrowIfNull(shortcut);
        if (
            shortcut is { Origin: { } origin, Action: TapAction tap }
            && origin.Source is not null
            && origin.ItemId is not null
            && _origins.Contains(origin.Source)
            && _actions.TryGetValue(origin.ItemId, out var found)
            && found.IsTableChord(tap.Chord)
        )
        {
            action = found;
            return true;
        }

        action = null!;
        return false;
    }

    /// <summary>
    /// The combination a tap of <paramref name="shortcut"/> sends to <paramref name="app"/> (EJE-003, decision D4): the
    /// common action's combination for that app and programs language; otherwise the variant of the programs language
    /// (CAT-005) or the saved combination.
    /// </summary>
    /// <param name="shortcut">A shortcut whose action is <paramref name="tap"/>.</param>
    /// <param name="tap">Its tap action.</param>
    /// <param name="app">The app in front, or <see langword="null"/> when none is known.</param>
    /// <param name="appsLanguage">The programs language, or <see langword="null"/> to use the saved combination.</param>
    public KeyChord ChordToSend(
        Shortcut shortcut,
        TapAction tap,
        ProcessName? app,
        LangCode? appsLanguage
    )
    {
        ArgumentNullException.ThrowIfNull(shortcut);
        ArgumentNullException.ThrowIfNull(tap);
        if (TryFind(shortcut, out var action))
        {
            return action.ChordFor(app, appsLanguage);
        }

        return
            appsLanguage is { } language
            && tap.Variants.Items.FirstOrDefault(v => v.AppsLanguage == language) is { } variant
            ? variant.Chord
            : tap.Chord;
    }
}
