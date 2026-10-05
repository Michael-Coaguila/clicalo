using Clicalo.Domain.CommonActions;
using Clicalo.Domain.Library;
using Clicalo.Domain.Primitives;
using Clicalo.Domain.Tests.Execution.Support;

namespace Clicalo.Domain.Tests.Execution;

/// <summary>
/// Decision D4: the engine sends a common action with the combination of the app in front, resolved at the moment of
/// sending; any other app gets the standard one.
/// </summary>
[Trait("Req", "EJE-018")]
[Trait("Req", "EJE-003")]
public sealed class CommonActionTapTests
{
    private const ushort VkG = 0x47;
    private const ushort VkS = 0x53;

    private static readonly CommonActionTable Table = new(
        ["seed"],
        [
            new CommonAction(
                "save",
                Chords.Of("ctrl", "s"),
                [
                    new CommonActionOverride(
                        [new ProcessName("winword.exe")],
                        LangCode.Es,
                        Chords.Of("ctrl", "g")
                    ),
                ]
            ),
        ]
    );

    private static readonly Shortcut Save = Shortcuts.Tap("save", "ctrl", "s") with
    {
        Origin = new CatalogRef("seed", "1", "save"),
    };

    [Theory]
    [InlineData("WINWORD.EXE", VkG)]
    [InlineData("notepad.exe", VkS)]
    public void Save_is_sent_with_the_combination_of_the_app_in_front(string app, ushort letter)
    {
        var engine = new EngineHarness(
            EngineHarness.DefaultConfig with
            {
                InterEventDelay = TimeSpan.Zero,
                AppsLanguage = LangCode.Es,
                CommonActions = Table,
            }
        );
        engine.Foreground(process: app);

        engine.Tap(Save);

        var keys = engine.Sent.Select(static e => e.Key.Vk).ToList();
        keys.ShouldContain(letter);
        keys.ShouldNotContain(letter == VkG ? VkS : VkG);
    }
}
