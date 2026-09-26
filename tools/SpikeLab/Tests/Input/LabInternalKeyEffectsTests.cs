using Clicalo.Application.Ports;
using Clicalo.TestKit.Windows.Input;
using Clicalo.Tools.SpikeLab.Input;

namespace Clicalo.Tools.SpikeLab.Tests.Input;

/// <summary>
/// The rule of S4.md: the internal chord and Win+H only go to a window of the laboratory or InputProbe. The sender is
/// fake: nothing here injects real input.
/// </summary>
public sealed class LabInternalKeyEffectsTests
{
    private const nint Own = 0x10;
    private const nint Word = 0x20;
    private readonly List<(nint Target, IReadOnlyList<KeyStroke> Batch)> _sent = [];
    private readonly List<string> _refusals = [];
    private readonly FakeRightsHotkey _hotkey = new() { IsRegistered = true };
    private int _rightsChords;
    private nint _foreground = Own;

    [Fact]
    public async Task The_rights_chord_goes_to_an_own_window_and_is_counted()
    {
        (
            await Effects().SendRightsHotkeyAsync(TestContext.Current.CancellationToken)
        ).ShouldBeTrue();

        _sent.ShouldHaveSingleItem().Target.ShouldBe(Own);
        _rightsChords.ShouldBe(1);
        _refusals.ShouldBeEmpty();
    }

    [Fact]
    public async Task Nothing_is_sent_when_the_chord_is_not_registered()
    {
        _hotkey.IsRegistered = false;

        (
            await Effects().SendRightsHotkeyAsync(TestContext.Current.CancellationToken)
        ).ShouldBeFalse();

        _sent.ShouldBeEmpty();
        _refusals.ShouldHaveSingleItem().ShouldContain("no está registrado");
    }

    [Fact]
    public async Task Nothing_is_sent_while_the_rights_hotkey_has_not_answered()
    {
        var effects = new LabInternalKeyEffects(
            () => null,
            () => _foreground,
            window => window == Own,
            Send,
            _refusals.Add,
            () => _rightsChords++
        );

        (
            await effects.SendRightsHotkeyAsync(TestContext.Current.CancellationToken)
        ).ShouldBeFalse();
        _sent.ShouldBeEmpty();
    }

    [Fact]
    public async Task Nothing_is_sent_to_another_app()
    {
        _foreground = Word;

        (
            await Effects().SendRightsHotkeyAsync(TestContext.Current.CancellationToken)
        ).ShouldBeFalse();
        (
            await Effects().SendDictationChordAsync(TestContext.Current.CancellationToken)
        ).ShouldBeFalse();

        _sent.ShouldBeEmpty();
        _rightsChords.ShouldBe(0);
        _refusals.Count.ShouldBe(2);
    }

    [Fact]
    public async Task Win_H_goes_to_an_own_focused_field()
    {
        (
            await Effects().SendDictationChordAsync(TestContext.Current.CancellationToken)
        ).ShouldBeTrue();

        _sent.ShouldHaveSingleItem().Batch.Count.ShouldBe(4);
        _rightsChords.ShouldBe(0);
    }

    [Fact]
    public async Task A_refusal_of_the_guarded_injector_is_reported()
    {
        var effects = new LabInternalKeyEffects(
            () => _hotkey,
            () => _foreground,
            window => window == Own,
            (_, _) => InjectionOutcome.Refused("La tecla Ctrl está pulsada."),
            _refusals.Add,
            () => _rightsChords++
        );

        (
            await effects.SendRightsHotkeyAsync(TestContext.Current.CancellationToken)
        ).ShouldBeFalse();

        _rightsChords.ShouldBe(0);
        _refusals.ShouldHaveSingleItem().ShouldContain("Ctrl está pulsada");
    }

    [Theory]
    [InlineData(0x10, 7u, 7u, 0, true)]
    [InlineData(0x20, 9u, 7u, 0x20, true)]
    [InlineData(0x20, 9u, 7u, 0, false)]
    [InlineData(0x20, 9u, 7u, 0x30, false)]
    [InlineData(0, 7u, 7u, 0, false)]
    public void Own_windows_and_the_probe_are_the_only_targets(
        long window,
        uint windowProcess,
        uint ownProcess,
        long probe,
        bool allowed
    ) =>
        LabTargetPolicy
            .IsOwnOrProbe((nint)window, windowProcess, ownProcess, (nint)probe)
            .ShouldBe(allowed);

    private LabInternalKeyEffects Effects() =>
        new(
            () => _hotkey,
            () => _foreground,
            window => window == Own,
            Send,
            _refusals.Add,
            () => _rightsChords++
        );

    private InjectionOutcome Send(nint target, IReadOnlyList<KeyStroke> batch)
    {
        _sent.Add((target, batch));
        return InjectionOutcome.Done;
    }

    private sealed class FakeRightsHotkey : IInternalRightsHotkey
    {
        public bool IsRegistered { get; set; }

        public ValueTask<bool> WaitForRightsAsync(CancellationToken cancellationToken) =>
            ValueTask.FromResult(true);
    }
}
