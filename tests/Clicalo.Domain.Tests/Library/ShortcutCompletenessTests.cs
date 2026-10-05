using Clicalo.Domain.Catalog;
using Clicalo.Domain.Keys;
using Clicalo.Domain.Library;
using Clicalo.Domain.Primitives;
using Clicalo.Domain.Privacy;
using static Clicalo.Domain.Tests.Library.LibraryBuilder;

namespace Clicalo.Domain.Tests.Library;

/// <summary>The single completeness rule (ATJ-009) the editor marks and the panel refuses with (EJE-015).</summary>
public sealed class ShortcutCompletenessTests
{
    private static readonly KeyChord CtrlC = KeyChord.FromKeys(KeyIds.Ctrl, KeyIds.C);

    public static TheoryData<ShortcutAction, CompletenessIssue> Actions =>
        new()
        {
            { new TapAction(CtrlC, []), CompletenessIssue.None },
            { new TapAction(KeyChord.Empty, []), CompletenessIssue.MissingKeys },
            { new TapAction(KeyChord.FromKeys(KeyIds.AltGr), []), CompletenessIssue.None },
            { new HoldAction(KeyChord.Empty), CompletenessIssue.MissingKeys },
            { new ToggleAction(KeyChord.Empty), CompletenessIssue.MissingKeys },
            { new ToggleAction(CtrlC), CompletenessIssue.None },
            { new TextAction(SecretText.From("hola"), TextMethod.Unicode), CompletenessIssue.None },
            { new TextAction(SecretText.Empty, TextMethod.Paste), CompletenessIssue.MissingText },
            {
                new TextAction(SecretText.Unavailable, TextMethod.Unicode),
                CompletenessIssue.TextUnavailable
            },
            { new MouseAction(MouseOp.Drag, ScrollSpeed.Normal), CompletenessIssue.None },
            { new MacroAction([]), CompletenessIssue.MissingSteps },
            { new MacroAction([new KeysStep(KeyChord.Empty)]), CompletenessIssue.MissingKeys },
            {
                new MacroAction([new TextStep(SecretText.Unavailable)]),
                CompletenessIssue.TextUnavailable
            },
            {
                new MacroAction([
                    new KeysStep(CtrlC),
                    new WaitStep(TimeSpan.FromMilliseconds(500)),
                    new MouseStep(MouseOp.RightClick),
                ]),
                CompletenessIssue.None
            },
            {
                new UrlAction(new UrlTarget.Valid(new Uri("https://ejemplo.com"))),
                CompletenessIssue.None
            },
            {
                new UrlAction(new UrlTarget.Valid(new Uri("http://localhost:8080/a?b=ñ"))),
                CompletenessIssue.None
            },
            {
                new UrlAction(new UrlTarget.Valid(new Uri("ftp://ejemplo.com"))),
                CompletenessIssue.InvalidAddress
            },
            { new UrlAction(new UrlTarget.Raw("ejemplo")), CompletenessIssue.InvalidAddress },
            { new AppAction(new AppTarget.Executable("notepad.exe", "")), CompletenessIssue.None },
            {
                new AppAction(
                    new AppTarget.StoreApp("Microsoft.WindowsCalculator_8wekyb3d8bbwe!App")
                ),
                CompletenessIssue.None
            },
            { new AppAction(new AppTarget.Document(@"C:\a.txt")), CompletenessIssue.None },
            { new AppAction(new AppTarget.Executable(" ", "")), CompletenessIssue.InvalidApp },
            { new AppAction(new AppTarget.Raw("cmd /c del *")), CompletenessIssue.InvalidApp },
            { new SystemAction(new SystemCommandId("lock")), CompletenessIssue.None },
        };

    [Theory]
    [Trait("Req", "ATJ-009")]
    [Trait("Req", "EJE-015")]
    [Trait("Req", "COP-005")]
    [MemberData(nameof(Actions))]
    public void Every_action_kind_has_its_rule(ShortcutAction action, CompletenessIssue issue) =>
        ShortcutCompleteness.Evaluate(action).ShouldBe(issue);

    [Fact]
    [Trait("Req", "ATJ-009")]
    public void The_facet_table_covers_every_action_kind() =>
        Actions
            .Select(row => row.Data.Item1.Kind)
            .Distinct()
            .Order()
            .ShouldBe(Enum.GetValues<ActionKind>());

    [Fact]
    [Trait("Req", "ATJ-009")]
    public void A_shortcut_without_a_name_in_any_language_is_incomplete()
    {
        ShortcutCompleteness
            .Evaluate(Tap("a", " ", KeyIds.A))
            .ShouldBe(CompletenessIssue.MissingName);
        ShortcutCompleteness
            .Evaluate(Named("a", "", "Copy", KeyIds.A))
            .ShouldBe(CompletenessIssue.None);
        ShortcutCompleteness
            .Evaluate(Shortcut("a", "Vacío", new TapAction(KeyChord.Empty, [])))
            .ShouldBe(CompletenessIssue.MissingKeys);
    }

    [Fact]
    [Trait("Req", "ATJ-011")]
    public void A_blank_draft_has_no_name_no_keys_and_is_a_tap()
    {
        var blank = Shortcut("d", "", new TapAction(KeyChord.Empty, []));

        ShortcutCompleteness.IsBlankDraft(blank).ShouldBeTrue();
        ShortcutCompleteness
            .IsBlankDraft(blank with { Name = LocalizedText.Same("x", LangCode.Es) })
            .ShouldBeFalse();
        ShortcutCompleteness
            .IsBlankDraft(blank with { Action = new TapAction(CtrlC, []) })
            .ShouldBeFalse();
        ShortcutCompleteness
            .IsBlankDraft(blank with { Action = new HoldAction(KeyChord.Empty) })
            .ShouldBeFalse();
        ShortcutCompleteness
            .IsBlankDraft(
                blank with
                {
                    Action = new MouseAction(MouseOp.RightClick, ScrollSpeed.Normal),
                }
            )
            .ShouldBeFalse();
    }
}
