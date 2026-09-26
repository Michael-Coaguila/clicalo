using System.Globalization;
using Clicalo.Domain.Keys;
using Clicalo.Domain.Primitives;

namespace Clicalo.Domain.KeySafety;

/// <summary>
/// Who holds a pressed item (blueprint §7.4): two holders may share a physical key (a Hold with Shift and a sticky
/// Shift), and releasing one never releases the key while the other holds it.
/// </summary>
/// <param name="Value">Stable text: <c>contact:17</c>, <c>toggle:&lt;id&gt;</c>, <c>sticky:ctrl</c>, <c>macro:&lt;run&gt;</c>, <c>dock:scrollUp</c>.</param>
public readonly record struct HolderId(string Value)
{
    /// <summary>The holder of a finger, pen or mouse contact (EJE-004, EJE-006).</summary>
    /// <param name="contactId">Pointer id of the contact.</param>
    public static HolderId ForContact(int contactId) =>
        new("contact:" + contactId.ToString(CultureInfo.InvariantCulture));

    /// <summary>The holder of a latched Toggle, or of a Hold invoked without contact (EJE-005, EJE-007).</summary>
    /// <param name="shortcut">The shortcut.</param>
    public static HolderId ForToggle(ShortcutId shortcut) => new("toggle:" + shortcut.Value);

    /// <summary>The holder of a sticky modifier (FIJ-*).</summary>
    /// <param name="modifier">The modifier.</param>
    public static HolderId ForSticky(ModifierKind modifier) =>
        new("sticky:" + modifier.ToString().ToLowerInvariant());

    /// <summary>The holder of the keys a running macro keeps pressed (EJE-010).</summary>
    /// <param name="run">The macro run.</param>
    public static HolderId ForMacro(long run) =>
        new("macro:" + run.ToString(CultureInfo.InvariantCulture));

    /// <summary>The holder of a repeating button of the Tab bar (scroll up or down, SEG-001).</summary>
    /// <param name="button">The bar button.</param>
    public static HolderId ForDock(string button) => new("dock:" + button);

    /// <inheritdoc />
    public override string ToString() => Value ?? string.Empty;
}
