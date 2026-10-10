using Clicalo.Application.Interaction;
using Clicalo.Application.Ports;

namespace Clicalo.Application.Tests.Interaction;

/// <summary>
/// <see cref="MenuOutsideCancel"/> (CUA-014): the menu of a shortcut of the Tab view is cancelled with Esc and with a
/// touch on another app, without any window of Clícalo taking the foreground (REG-01): Esc is taken only while the
/// menu shows, and the pointer only cancels a menu that a finger opened.
/// </summary>
public sealed class MenuOutsideCancelTests
{
    private readonly FakeSignals _signals = new();
    private readonly List<Action> _posted = [];
    private readonly MenuOutsideCancel _cancel;
    private int _closed;

    public MenuOutsideCancelTests() =>
        _cancel = new MenuOutsideCancel(_signals, _posted.Add, () => _closed++);

    [Fact]
    [Trait("Req", "CUA-014")]
    [Trait("Req", "REG-01")]
    public void Esc_is_taken_only_while_the_menu_shows()
    {
        _signals.Watches.ShouldBeEmpty();

        _cancel.Apply(menuOpen: true, openedByFinger: false);
        _cancel.Apply(menuOpen: true, openedByFinger: false);
        _signals.Watches.ShouldBe([true]);

        _cancel.Apply(menuOpen: false, openedByFinger: false);
        _cancel.Apply(menuOpen: false, openedByFinger: false);
        _signals.Watches.ShouldBe([true, false]);
    }

    [Fact]
    [Trait("Req", "CUA-014")]
    public void Esc_closes_the_open_menu_on_the_thread_that_owns_it()
    {
        _cancel.Apply(menuOpen: true, openedByFinger: false);

        _signals.PressEscape();

        _closed.ShouldBe(0);
        Run();
        _closed.ShouldBe(1);
    }

    [Fact]
    [Trait("Req", "CUA-014")]
    public void A_touch_on_another_app_closes_a_menu_that_a_finger_opened()
    {
        _cancel.Apply(menuOpen: true, openedByFinger: true);

        _signals.MovePointerOutside();
        Run();

        _closed.ShouldBe(1);
    }

    [Fact]
    [Trait("Req", "CUA-014")]
    public void The_pointer_does_not_close_a_menu_opened_with_the_mouse_the_pen_or_by_voice()
    {
        // A mouse on its way from the shortcut to the menu crosses pixels that are not of Clícalo.
        _cancel.Apply(menuOpen: true, openedByFinger: false);

        _signals.MovePointerOutside();

        // Every move of a mouse over another app raises the signal: nothing is queued for it.
        _posted.ShouldBeEmpty();
        Run();

        _closed.ShouldBe(0);
        _signals.PressEscape();
        Run();
        _closed.ShouldBe(1);
    }

    [Fact]
    [Trait("Req", "CUA-014")]
    public void Nothing_closes_while_no_menu_is_open()
    {
        _signals.PressEscape();
        _signals.MovePointerOutside();
        Run();
        _closed.ShouldBe(0);

        // A signal that arrives while the menu is open and is handled after it closed does nothing either.
        _cancel.Apply(menuOpen: true, openedByFinger: true);
        _signals.PressEscape();
        _signals.MovePointerOutside();
        _cancel.Apply(menuOpen: false, openedByFinger: false);
        Run();

        _closed.ShouldBe(0);
    }

    private void Run()
    {
        foreach (var work in _posted.ToArray())
        {
            work();
        }

        _posted.Clear();
    }

    private sealed class FakeSignals : IMenuCancelSignals
    {
        public event EventHandler? EscapePressed;

        public event EventHandler? PointerWentOutside;

        public List<bool> Watches { get; } = [];

        public void WatchEscape(bool watch) => Watches.Add(watch);

        public void PressEscape() => EscapePressed?.Invoke(this, EventArgs.Empty);

        public void MovePointerOutside() => PointerWentOutside?.Invoke(this, EventArgs.Empty);
    }
}
