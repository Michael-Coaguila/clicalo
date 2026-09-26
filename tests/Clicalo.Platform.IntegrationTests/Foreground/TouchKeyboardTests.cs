using Clicalo.Application.Ports;
using Clicalo.Platform.IntegrationTests.Desktop;
using Clicalo.Platform.Windows.Foreground;

namespace Clicalo.Platform.IntegrationTests.Foreground;

/// <summary>
/// <c>TouchKeyboard</c> without showing the keyboard (spike S4): runners have no touch keyboard and showing it on the
/// maintainer's machine is part of the manual script. Nothing is injected here.
/// </summary>
[Collection(DesktopCollectionDefinition.Name)]
[Trait("Requires", "Desktop")]
[Trait("Req", "BUS-002")]
public sealed class TouchKeyboardTests
{
    [DesktopFact]
    public void The_occluded_area_is_empty_when_the_keyboard_is_hidden()
    {
        using var keyboard = new TouchKeyboard(new NoKeyEffects());

        keyboard.OccludedArea.IsEmpty.ShouldBeTrue(
            "the touch keyboard must be closed while the desktop tests run"
        );
    }

    [DesktopFact]
    [Trait("Req", "BUS-003")]
    public async Task Dictation_goes_through_the_internal_key_effects()
    {
        var effects = new NoKeyEffects();
        using var keyboard = new TouchKeyboard(effects);

        (await keyboard.StartDictationAsync(TestContext.Current.CancellationToken)).ShouldBeFalse();

        effects.DictationRequests.ShouldBe(1);
    }

    [DesktopFact]
    public async Task Hiding_a_keyboard_Clicalo_did_not_show_does_nothing()
    {
        using var keyboard = new TouchKeyboard(new NoKeyEffects());
        var changes = 0;
        keyboard.OccludedAreaChanged += (_, _) => changes++;

        await keyboard.HideKeyboardAsync(TestContext.Current.CancellationToken);

        changes.ShouldBe(0);
        keyboard.OccludedArea.IsEmpty.ShouldBeTrue();
    }

    /// <summary>Records the requests and injects nothing.</summary>
    private sealed class NoKeyEffects : IInternalKeyEffects
    {
        public int DictationRequests { get; private set; }

        public ValueTask<bool> SendRightsHotkeyAsync(CancellationToken cancellationToken) =>
            ValueTask.FromResult(false);

        public ValueTask<bool> SendDictationChordAsync(CancellationToken cancellationToken)
        {
            DictationRequests++;
            return ValueTask.FromResult(false);
        }
    }
}
