using Clicalo.Domain.Catalog;
using Clicalo.Domain.Library;
using Clicalo.Domain.Primitives;
using Clicalo.Domain.Privacy;

namespace Clicalo.Domain.Tests.Execution.Support;

/// <summary>Shortcuts built by hand for the engine tests (the library aggregate is not needed to run one).</summary>
internal static class Shortcuts
{
    public static ShortcutOptions Default { get; } =
        new(Confirm: false, new HoldLimit.InheritGlobal(), IsPrivate: false);

    public static Shortcut Of(string id, ShortcutAction action, ShortcutOptions? options = null) =>
        new(
            new ShortcutId(id),
            LocalizedText.Same(id, LangCode.Es, LangCode.En),
            new IconRef("keyboard"),
            AutoIcon: false,
            new CategoryId("general"),
            action,
            options ?? Default,
            Origin: null,
            PinnedFrom: null
        );

    public static Shortcut Tap(string id, params string[] keys) =>
        Of(id, new TapAction(Chords.Of(keys), []));

    public static Shortcut Hold(string id, params string[] keys) =>
        Of(id, new HoldAction(Chords.Of(keys)));

    public static Shortcut Toggle(string id, params string[] keys) =>
        Of(id, new ToggleAction(Chords.Of(keys)));

    public static Shortcut Text(string id, string text, TextMethod method = TextMethod.Unicode) =>
        Of(id, new TextAction(SecretText.From(text), method));

    public static Shortcut Mouse(string id, MouseOp op, ScrollSpeed speed = ScrollSpeed.Normal) =>
        Of(id, new MouseAction(op, speed));

    public static Shortcut Macro(string id, params MacroStep[] steps) =>
        Of(id, new MacroAction([.. steps]));

    public static Shortcut Url(string id, string address) =>
        Of(id, new UrlAction(new UrlTarget.Valid(new Uri(address))));

    public static Shortcut App(string id, string path) =>
        Of(id, new AppAction(new AppTarget.Executable(path, string.Empty)));

    public static Shortcut System(string id, string command) =>
        Of(id, new SystemAction(new SystemCommandId(command)));

    public static ShortcutOptions Confirming { get; } = Default with { Confirm = true };

    public static ShortcutOptions Limited(TimeSpan limit) =>
        Default with
        {
            MaxHold = new HoldLimit.After(limit),
        };

    public static ShortcutOptions Unlimited { get; } =
        Default with
        {
            MaxHold = new HoldLimit.Never(),
        };
}
