using System.Collections.Immutable;
using Clicalo.Domain.Keys;
using Clicalo.Domain.Primitives;

namespace Clicalo.Domain.KeySafety;

/// <summary>
/// The logical ledger of everything Clícalo holds (SEG-001, blueprint §7.4), with reference counts per physical key:
/// a key goes down when its first holder acquires it and up when its last holder releases it. Pure and immutable; the
/// engine keeps it in <c>EngineState</c> and the physical ledger (<c>Clicalo.Platform.Core.KeyLedger</c>) mirrors
/// what is actually down.
/// </summary>
/// <remarks>
/// Every transition returns the events that make the system match the new ledger, so «what is down» (<c>D</c> of
/// §7.5) is always the union of the keys of every item (INV-1). Mouse buttons are counted the same way, by the items
/// that hold them. Safety releases (a holder's release, «Release all») send the menu mask before Alt or Win; the
/// planned release of a Tap does not (<see cref="Lift"/>), so a Win tap still opens Start.
/// </remarks>
/// <param name="Holders">Holders of each physical key.</param>
/// <param name="Items">Items by holder.</param>
public sealed record KeyboardLedger(
    ImmutableDictionary<InjectedKey, ImmutableHashSet<HolderId>> Holders,
    ImmutableDictionary<HolderId, PressedItem> Items
)
{
    private static readonly MouseButtons[] ButtonOrder =
    [
        MouseButtons.Left,
        MouseButtons.Right,
        MouseButtons.Middle,
        MouseButtons.X1,
        MouseButtons.X2,
    ];

    /// <summary>Nothing held.</summary>
    public static KeyboardLedger Empty { get; } =
        new(
            ImmutableDictionary<InjectedKey, ImmutableHashSet<HolderId>>.Empty,
            ImmutableDictionary<HolderId, PressedItem>.Empty
        );

    /// <summary>Whether nothing is held (the panic strip hides, SEG-002).</summary>
    public bool IsEmpty => Items.IsEmpty;

    /// <summary>The mouse buttons some item holds.</summary>
    public MouseButtons HeldButtons => ButtonsOf(Items);

    /// <summary>Whether some holder keeps <paramref name="key"/> down.</summary>
    /// <param name="key">The key.</param>
    public bool IsDown(InjectedKey key) => Holders.ContainsKey(key);

    /// <summary>Adds an item; emits a key down only for keys that go from 0 to 1 holders.</summary>
    /// <param name="item">The item; its holder must not hold anything yet.</param>
    public LedgerTransition Acquire(PressedItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (Items.ContainsKey(item.Holder))
        {
            throw new InvalidOperationException(
                "The holder '" + item.Holder.Value + "' already holds an item."
            );
        }

        var events = ImmutableArray.CreateBuilder<InjectedEvent>();
        var holders = Holders.ToBuilder();
        var keys = ImmutableArray.CreateBuilder<InjectedKey>(item.Keys.Count);
        foreach (var key in item.Keys)
        {
            if (keys.Contains(key))
            {
                continue;
            }

            keys.Add(key);
            AddHolder(holders, key, item.Holder, events);
        }

        AppendButtonDowns(item.Buttons, HeldButtons, events);
        var stored = item with { Keys = new ValueList<InjectedKey>(keys.ToImmutable()) };
        return new LedgerTransition(
            new KeyboardLedger(holders.ToImmutable(), Items.Add(item.Holder, stored)),
            events.ToImmutable()
        );
    }

    /// <summary>
    /// Adds one key to a holder's item, creating the item from <paramref name="template"/> when the holder holds
    /// nothing yet (a Tap presses its keys one event at a time). Emits a key down only when the key goes from 0 to 1
    /// holders.
    /// </summary>
    /// <param name="template">The item to create (its keys and buttons are ignored); ignored when it exists.</param>
    /// <param name="key">The key.</param>
    public LedgerTransition Press(PressedItem template, InjectedKey key)
    {
        var item = ItemOrTemplate(template);
        if (item.Keys.Items.Contains(key))
        {
            return new LedgerTransition(this, []);
        }

        var events = ImmutableArray.CreateBuilder<InjectedEvent>(1);
        var holders = Holders.ToBuilder();
        AddHolder(holders, key, item.Holder, events);
        var updated = item with { Keys = new ValueList<InjectedKey>(item.Keys.Items.Add(key)) };
        return new LedgerTransition(
            new KeyboardLedger(holders.ToImmutable(), Items.SetItem(item.Holder, updated)),
            events.ToImmutable()
        );
    }

    /// <summary>
    /// Adds mouse buttons to a holder's item, creating it from <paramref name="template"/> when needed (the drag step
    /// of a macro). Emits a button down only for buttons no other item holds.
    /// </summary>
    /// <param name="template">The item to create (its keys and buttons are ignored); ignored when it exists.</param>
    /// <param name="buttons">The buttons.</param>
    public LedgerTransition PressButtons(PressedItem template, MouseButtons buttons)
    {
        var item = ItemOrTemplate(template);
        var events = ImmutableArray.CreateBuilder<InjectedEvent>();
        AppendButtonDowns(buttons, HeldButtons, events);
        var updated = item with { Buttons = item.Buttons | buttons };
        return new LedgerTransition(
            this with
            {
                Items = Items.SetItem(item.Holder, updated),
            },
            events.ToImmutable()
        );
    }

    /// <summary>
    /// The planned release of one key of a holder (the second half of a Tap, EJE-003): no menu mask, so a Win tap
    /// still opens Start. Emits the key up only when no other holder keeps the key; the item goes away when it holds
    /// nothing more.
    /// </summary>
    /// <param name="holder">The holder.</param>
    /// <param name="key">The key.</param>
    public LedgerTransition Lift(HolderId holder, InjectedKey key)
    {
        if (!Items.TryGetValue(holder, out var item) || !item.Keys.Items.Contains(key))
        {
            return new LedgerTransition(this, []);
        }

        var events = ImmutableArray.CreateBuilder<InjectedEvent>(1);
        var holders = Holders.ToBuilder();
        RemoveHolder(holders, key, holder, masked: false, events);
        var remaining = item.Keys.Items.Remove(key);
        var items =
            remaining.IsEmpty && item.Buttons == MouseButtons.None
                ? Items.Remove(holder)
                : Items.SetItem(holder, item with { Keys = new ValueList<InjectedKey>(remaining) });
        return new LedgerTransition(
            new KeyboardLedger(holders.ToImmutable(), items),
            events.ToImmutable()
        );
    }

    /// <summary>
    /// Releases mouse buttons of a holder (the second drag step of a macro); the item goes away when it holds nothing
    /// more. Emits a button up only for buttons no other item holds.
    /// </summary>
    /// <param name="holder">The holder.</param>
    /// <param name="buttons">The buttons.</param>
    public LedgerTransition LiftButtons(HolderId holder, MouseButtons buttons)
    {
        if (
            !Items.TryGetValue(holder, out var item)
            || (item.Buttons & buttons) == MouseButtons.None
        )
        {
            return new LedgerTransition(this, []);
        }

        var remainingButtons = item.Buttons & ~buttons;
        var items =
            remainingButtons == MouseButtons.None && item.Keys.IsEmpty
                ? Items.Remove(holder)
                : Items.SetItem(holder, item with { Buttons = remainingButtons });
        var events = ImmutableArray.CreateBuilder<InjectedEvent>();
        AppendButtonUps(item.Buttons & buttons, ButtonsOf(items), events);
        return new LedgerTransition(this with { Items = items }, events.ToImmutable());
    }

    /// <summary>
    /// Removes a holder's item; emits a key up (with the menu mask before Alt or Win) only for keys that go from 1 to 0
    /// holders, in reverse press order.
    /// </summary>
    /// <param name="holder">The holder.</param>
    public LedgerTransition Release(HolderId holder)
    {
        if (!Items.TryGetValue(holder, out var item))
        {
            return new LedgerTransition(this, []);
        }

        var events = ImmutableArray.CreateBuilder<InjectedEvent>();
        var items = Items.Remove(holder);
        AppendButtonUps(item.Buttons, ButtonsOf(items), events);
        var holders = Holders.ToBuilder();
        for (var i = item.Keys.Count - 1; i >= 0; i--)
        {
            RemoveHolder(holders, item.Keys[i], holder, masked: true, events);
        }

        return new LedgerTransition(
            new KeyboardLedger(holders.ToImmutable(), items),
            events.ToImmutable()
        );
    }

    /// <summary>Removes every item and releases everything in reverse order (SEG-003, INV-3).</summary>
    public LedgerTransition ReleaseAll()
    {
        if (Items.IsEmpty && Holders.IsEmpty)
        {
            return new LedgerTransition(this, []);
        }

        var events = ImmutableArray.CreateBuilder<InjectedEvent>();
        AppendButtonUps(HeldButtons, MouseButtons.None, events);
        var released = new HashSet<InjectedKey>();
        foreach (var item in InReleaseOrder(Items.Values))
        {
            for (var i = item.Keys.Count - 1; i >= 0; i--)
            {
                var key = item.Keys[i];
                if (Holders.ContainsKey(key) && released.Add(key))
                {
                    AppendKeyUp(key, masked: true, events);
                }
            }
        }

        // A key without an item cannot exist by construction; release it anyway so nothing can stay down.
        foreach (var key in Holders.Keys.OrderBy(static k => k.Vk).ThenBy(static k => k.Scan))
        {
            if (released.Add(key))
            {
                AppendKeyUp(key, masked: true, events);
            }
        }

        return new LedgerTransition(Empty, events.ToImmutable());
    }

    /// <summary>
    /// Recomputes every deadline from its press with a new global limit (SEG-004: changing the limit recalculates the
    /// deadlines in progress); items with their own limit keep it.
    /// </summary>
    /// <param name="globalLimit">The new global limit, or <see langword="null"/> for «Never».</param>
    /// <param name="ticksPerSecond">Tick frequency of the time source.</param>
    public KeyboardLedger WithGlobalLimit(TimeSpan? globalLimit, long ticksPerSecond)
    {
        var items = Items;
        foreach (var item in Items.Values)
        {
            if (!item.InheritsGlobalLimit)
            {
                continue;
            }

            long? deadline = globalLimit is { } limit
                ? item.SinceTicks + ToTicks(limit, ticksPerSecond)
                : null;
            if (deadline != item.DeadlineTicks)
            {
                items = items.SetItem(item.Holder, item with { DeadlineTicks = deadline });
            }
        }

        return ReferenceEquals(items, Items) ? this : this with { Items = items };
    }

    /// <summary>The earliest deadline, so the engine keeps a single timer; <see langword="null"/> when none.</summary>
    public long? NextDeadline()
    {
        long? next = null;
        foreach (var item in Items.Values)
        {
            if (item.DeadlineTicks is { } deadline && (next is null || deadline < next))
            {
                next = deadline;
            }
        }

        return next;
    }

    /// <summary>The holders whose deadline is at or before <paramref name="nowTicks"/>, oldest press first.</summary>
    /// <param name="nowTicks">Now.</param>
    public ImmutableArray<HolderId> ExpiredAt(long nowTicks) =>
        [
            .. Items
                .Values.Where(item => item.DeadlineTicks is { } deadline && deadline <= nowTicks)
                .OrderBy(static item => item.SinceTicks)
                .ThenBy(static item => item.Holder.Value, StringComparer.Ordinal)
                .Select(static item => item.Holder),
        ];

    /// <summary>
    /// Converts a duration to ticks of a time source with <paramref name="ticksPerSecond"/> ticks per second.
    /// </summary>
    /// <param name="duration">The duration.</param>
    /// <param name="ticksPerSecond">Tick frequency.</param>
    public static long ToTicks(TimeSpan duration, long ticksPerSecond) =>
        (long)((Int128)duration.Ticks * ticksPerSecond / TimeSpan.TicksPerSecond);

    private PressedItem ItemOrTemplate(PressedItem template)
    {
        ArgumentNullException.ThrowIfNull(template);
        return Items.TryGetValue(template.Holder, out var existing)
            ? existing
            : template with
            {
                Keys = [],
                Buttons = MouseButtons.None,
            };
    }

    private static IEnumerable<PressedItem> InReleaseOrder(IEnumerable<PressedItem> items) =>
        items
            .OrderByDescending(static item => item.SinceTicks)
            .ThenByDescending(static item => item.Holder.Value, StringComparer.Ordinal);

    private static MouseButtons ButtonsOf(ImmutableDictionary<HolderId, PressedItem> items)
    {
        var buttons = MouseButtons.None;
        foreach (var item in items.Values)
        {
            buttons |= item.Buttons;
        }

        return buttons;
    }

    private static void AddHolder(
        ImmutableDictionary<InjectedKey, ImmutableHashSet<HolderId>>.Builder holders,
        InjectedKey key,
        HolderId holder,
        ImmutableArray<InjectedEvent>.Builder events
    )
    {
        if (holders.TryGetValue(key, out var set))
        {
            holders[key] = set.Add(holder);
            return;
        }

        holders[key] = [holder];
        events.Add(InjectedEvent.KeyDown(key));
    }

    private static void RemoveHolder(
        ImmutableDictionary<InjectedKey, ImmutableHashSet<HolderId>>.Builder holders,
        InjectedKey key,
        HolderId holder,
        bool masked,
        ImmutableArray<InjectedEvent>.Builder events
    )
    {
        if (!holders.TryGetValue(key, out var set) || !set.Contains(holder))
        {
            return;
        }

        var remaining = set.Remove(holder);
        if (!remaining.IsEmpty)
        {
            holders[key] = remaining;
            return;
        }

        holders.Remove(key);
        AppendKeyUp(key, masked, events);
    }

    private static void AppendKeyUp(
        InjectedKey key,
        bool masked,
        ImmutableArray<InjectedEvent>.Builder events
    )
    {
        if (masked && InjectedKeyKinds.IsAltOrWin(key))
        {
            events.Add(InjectedEvent.MenuMask(key.Mode));
        }

        events.Add(InjectedEvent.KeyUp(key));
    }

    private static void AppendButtonDowns(
        MouseButtons pressed,
        MouseButtons alreadyHeld,
        ImmutableArray<InjectedEvent>.Builder events
    )
    {
        foreach (var button in ButtonOrder)
        {
            if (
                (pressed & button) != MouseButtons.None
                && (alreadyHeld & button) == MouseButtons.None
            )
            {
                events.Add(InjectedEvent.MouseDown(button));
            }
        }
    }

    private static void AppendButtonUps(
        MouseButtons released,
        MouseButtons stillHeld,
        ImmutableArray<InjectedEvent>.Builder events
    )
    {
        for (var i = ButtonOrder.Length - 1; i >= 0; i--)
        {
            var button = ButtonOrder[i];
            if (
                (released & button) != MouseButtons.None
                && (stillHeld & button) == MouseButtons.None
            )
            {
                events.Add(InjectedEvent.MouseUp(button));
            }
        }
    }
}
