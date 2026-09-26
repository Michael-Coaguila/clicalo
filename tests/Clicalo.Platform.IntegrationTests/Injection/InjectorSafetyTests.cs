using Clicalo.TestKit.Windows;
using Clicalo.TestKit.Windows.Input;

namespace Clicalo.Platform.IntegrationTests.Injection;

/// <summary>
/// The test injector's safety rules, checked without a desktop. <c>SendInput</c> is replaced by a recorder, so even
/// a broken guard could never type into the foreground window of the machine running the tests.
/// </summary>
public sealed class InjectorSafetyTests
{
    private readonly List<IReadOnlyList<KeyStroke>> _handedToSendInput = [];

    [Fact]
    public void A_window_that_is_not_in_the_foreground_is_refused_without_injecting()
    {
        // The desktop root window exists in every session and is never the foreground window.
        var injector = CreateInjector(ForegroundWindows.DesktopWindow);

        var refusal = Should.Throw<InjectionRefusedException>(() =>
            injector.Send(KeyStrokes.Chord(VirtualKeyCode.A))
        );

        refusal.Message.ShouldContain("not in the foreground");
        refusal.Message.ShouldContain("Nothing was injected");
        injector.SentBatches.ShouldBe(0);
        _handedToSendInput.ShouldBeEmpty();
    }

    [Fact]
    public void A_window_that_does_not_exist_is_refused_without_injecting()
    {
        var injector = CreateInjector(0x7FFF_FFF0);

        var refusal = Should.Throw<InjectionRefusedException>(() =>
            injector.Send(KeyStrokes.Chord(VirtualKeyCode.A))
        );

        refusal.Message.ShouldContain("does not exist");
        injector.SentBatches.ShouldBe(0);
        _handedToSendInput.ShouldBeEmpty();
    }

    [Fact]
    public void An_injector_needs_a_target_window() =>
        Should.Throw<ArgumentException>(() => new TestKeyboardInjector(0));

    [Fact]
    public void An_unbalanced_batch_is_refused_before_the_foreground_is_even_checked()
    {
        var injector = CreateInjector(ForegroundWindows.DesktopWindow);

        Should
            .Throw<ArgumentException>(() =>
                injector.Send([KeyStroke.Press(VirtualKeyCode.LeftControl)])
            )
            .Message.ShouldContain("leaves 1 key(s) down");
        injector.SentBatches.ShouldBe(0);
        _handedToSendInput.ShouldBeEmpty();
    }

    [Fact]
    public void Virtual_key_strokes_get_the_informative_scan_code_and_extended_flag_of_the_target_layout()
    {
        var injector = CreateInjector(ForegroundWindows.DesktopWindow);

        var resolved = injector.Resolve(
            KeyStrokes.Chord(
                VirtualKeyCode.LeftControl,
                VirtualKeyCode.RightControl,
                VirtualKeyCode.Left
            )
        );

        // Ctrl and the arrow keys have the same scan codes in every layout.
        resolved
            .Select(stroke =>
                (stroke.VirtualKey, stroke.ScanCode, stroke.IsExtended, stroke.IsKeyUp)
            )
            .ShouldBe([
                (VirtualKeyCode.LeftControl, (ushort)0x1D, false, false),
                (VirtualKeyCode.RightControl, (ushort)0x1D, true, false),
                (VirtualKeyCode.Left, (ushort)0x4B, true, false),
                (VirtualKeyCode.Left, (ushort)0x4B, true, true),
                (VirtualKeyCode.RightControl, (ushort)0x1D, true, true),
                (VirtualKeyCode.LeftControl, (ushort)0x1D, false, true),
            ]);
        resolved.ShouldAllBe(stroke => !stroke.IsScanCodeMode && !stroke.IsUnicode);
    }

    [Fact]
    public void Scan_code_and_Unicode_strokes_are_sent_as_given()
    {
        var injector = CreateInjector(ForegroundWindows.DesktopWindow);
        KeyStroke[] batch =
        [
            .. KeyStrokes.ScanCodeTap(0x4B, extended: true),
            .. KeyStrokes.UnicodeText("ñ"),
        ];

        injector.Resolve(batch).ShouldBe(batch);
    }

    [Fact]
    public void Balanced_batches_are_accepted()
    {
        KeyStrokeBatch.Validate(
            KeyStrokes.Chord(VirtualKeyCode.LeftControl, VirtualKeyCode.LeftShift, VirtualKeyCode.A)
        );
        KeyStrokeBatch.Validate(KeyStrokes.ScanCodeTap(0x4B, extended: true));
        KeyStrokeBatch.Validate(KeyStrokes.UnicodeText("ñ😀"));
        KeyStrokeBatch.Validate([
            KeyStroke.Press(VirtualKeyCode.LeftControl),
            KeyStroke.Release(VirtualKeyCode.LeftControl),
            KeyStroke.Press(VirtualKeyCode.LeftControl),
            KeyStroke.Release(VirtualKeyCode.LeftControl),
        ]);
    }

    [Fact]
    public void A_press_without_its_release_is_rejected() =>
        Should
            .Throw<ArgumentException>(() =>
                KeyStrokeBatch.Validate([KeyStroke.Press(VirtualKeyCode.A)])
            )
            .Message.ShouldContain("leaves 1 key(s) down");

    [Fact]
    public void A_release_without_its_press_is_rejected() =>
        Should
            .Throw<ArgumentException>(() =>
                KeyStrokeBatch.Validate([KeyStroke.Release(VirtualKeyCode.A)])
            )
            .Message.ShouldContain("Release without a press");

    [Fact]
    public void A_second_press_of_a_held_key_is_rejected() =>
        Should
            .Throw<ArgumentException>(() =>
                KeyStrokeBatch.Validate([
                    KeyStroke.Press(VirtualKeyCode.A),
                    KeyStroke.Press(VirtualKeyCode.A),
                    KeyStroke.Release(VirtualKeyCode.A),
                ])
            )
            .Message.ShouldContain("already down");

    [Fact]
    public void A_scan_code_release_must_match_the_extended_flag_of_its_press() =>
        Should.Throw<ArgumentException>(() =>
            KeyStrokeBatch.Validate([
                KeyStroke.PressScanCode(0x1D, extended: true),
                KeyStroke.ReleaseScanCode(0x1D, extended: false),
            ])
        );

    [Fact]
    public void Empty_and_oversized_batches_are_rejected()
    {
        Should.Throw<ArgumentException>(() => KeyStrokeBatch.Validate([]));
        Should.Throw<ArgumentException>(() =>
            KeyStrokeBatch.Validate(
                KeyStrokes.UnicodeText(new string('x', (KeyStrokeBatch.MaxLength / 2) + 1))
            )
        );
    }

    [Fact]
    public void A_chord_presses_in_order_and_releases_in_reverse_order() =>
        KeyStrokes
            .Chord(VirtualKeyCode.LeftControl, VirtualKeyCode.LeftShift, VirtualKeyCode.A)
            .Select(stroke => (stroke.VirtualKey, stroke.IsKeyUp))
            .ShouldBe([
                (VirtualKeyCode.LeftControl, false),
                (VirtualKeyCode.LeftShift, false),
                (VirtualKeyCode.A, false),
                (VirtualKeyCode.A, true),
                (VirtualKeyCode.LeftShift, true),
                (VirtualKeyCode.LeftControl, true),
            ]);

    [Fact]
    public void Unicode_text_sends_every_UTF16_unit_including_both_surrogates()
    {
        var strokes = KeyStrokes.UnicodeText("ñ😀");

        strokes.ShouldAllBe(stroke => stroke.IsUnicode && stroke.VirtualKey == VirtualKeyCode.None);
        strokes
            .Select(stroke => (stroke.ScanCode, stroke.IsKeyUp))
            .ShouldBe([
                (0x00F1, false),
                (0x00F1, true),
                (0xD83D, false),
                (0xD83D, true),
                (0xDE00, false),
                (0xDE00, true),
            ]);
    }

    [Fact]
    public void Scan_code_strokes_use_the_scan_code_flag_and_no_virtual_key()
    {
        var press = KeyStroke.PressScanCode(0x4B, extended: true);

        press.VirtualKey.ShouldBe(VirtualKeyCode.None);
        press.Flags.ShouldBe(KeyStrokeOptions.ScanCode | KeyStrokeOptions.ExtendedKey);
        press.ToString().ShouldBe("SC E0 0x4B down");
        Should.Throw<ArgumentOutOfRangeException>(() =>
            KeyStroke.PressScanCode(0xE04B, extended: true)
        );
    }

    [Theory]
    [InlineData(VirtualKeyCode.RightControl)]
    [InlineData(VirtualKeyCode.RightMenu)]
    public void Right_Ctrl_and_AltGr_are_refused_outside_continuous_integration(VirtualKeyCode key)
    {
        // The desktop root window is never in the foreground: the reserved-key rule must refuse before that check.
        var injector = new TestKeyboardInjector(
            ForegroundWindows.DesktopWindow,
            _handedToSendInput.Add,
            reservedKeysAllowed: static () => false
        );

        var refusal = Should.Throw<InjectionRefusedException>(() =>
            injector.Send(KeyStrokes.Chord(key))
        );

        refusal.Message.ShouldContain("right Ctrl or right Alt");
        refusal.Message.ShouldContain("Nothing was injected");
        injector.SentBatches.ShouldBe(0);
        _handedToSendInput.ShouldBeEmpty();
    }

    [Fact]
    public void A_right_Ctrl_as_an_extended_scan_code_is_refused_outside_continuous_integration()
    {
        var injector = new TestKeyboardInjector(
            ForegroundWindows.DesktopWindow,
            _handedToSendInput.Add,
            reservedKeysAllowed: static () => false
        );

        Should
            .Throw<InjectionRefusedException>(() =>
                injector.Send(KeyStrokes.ScanCodeTap(0x1D, extended: true))
            )
            .Message.ShouldContain("right Ctrl or right Alt");
        _handedToSendInput.ShouldBeEmpty();
    }

    [Fact]
    public void Left_Ctrl_and_left_Alt_are_not_reserved()
    {
        var injector = new TestKeyboardInjector(
            ForegroundWindows.DesktopWindow,
            _handedToSendInput.Add,
            reservedKeysAllowed: static () => false
        );

        // They pass the reserved-key rule and stop at the next one: the desktop is not in the foreground.
        Should
            .Throw<InjectionRefusedException>(() =>
                injector.Send(KeyStrokes.Chord(VirtualKeyCode.LeftControl, VirtualKeyCode.LeftMenu))
            )
            .Message.ShouldContain("not in the foreground");
        _handedToSendInput.ShouldBeEmpty();
    }

    private TestKeyboardInjector CreateInjector(nint targetWindow) =>
        new(targetWindow, _handedToSendInput.Add);
}
