using Clicalo.TestKit.Windows;
using Clicalo.TestKit.Windows.Input;

namespace Clicalo.Platform.IntegrationTests.Injection;

/// <summary>
/// The test injector's safety rules, checked without a desktop and without sending anything: every case below is
/// refused before <c>SendInput</c> is called.
/// </summary>
public sealed class InjectorSafetyTests
{
    [Fact]
    public void A_window_that_is_not_in_the_foreground_is_refused_without_injecting()
    {
        // The desktop root window exists in every session and is never the foreground window.
        var injector = new TestKeyboardInjector(ForegroundWindows.DesktopWindow);

        var refusal = Should.Throw<InjectionRefusedException>(() =>
            injector.Send(KeyStrokes.Chord(VirtualKeyCode.A))
        );

        refusal.Message.ShouldContain("not in the foreground");
        refusal.Message.ShouldContain("Nothing was injected");
        injector.SentBatches.ShouldBe(0);
    }

    [Fact]
    public void A_window_that_does_not_exist_is_refused_without_injecting()
    {
        var injector = new TestKeyboardInjector(0x7FFF_FFF0);

        var refusal = Should.Throw<InjectionRefusedException>(() =>
            injector.Send(KeyStrokes.Chord(VirtualKeyCode.A))
        );

        refusal.Message.ShouldContain("does not exist");
        injector.SentBatches.ShouldBe(0);
    }

    [Fact]
    public void An_injector_needs_a_target_window() =>
        Should.Throw<ArgumentException>(() => new TestKeyboardInjector(0));

    [Fact]
    public void An_unbalanced_batch_is_refused_before_the_foreground_is_even_checked()
    {
        var injector = new TestKeyboardInjector(ForegroundWindows.DesktopWindow);

        Should.Throw<ArgumentException>(() =>
            injector.Send([KeyStroke.Press(VirtualKeyCode.LeftControl)])
        );
        injector.SentBatches.ShouldBe(0);
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
}
