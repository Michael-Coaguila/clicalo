using Clicalo.Domain.Library;
using Clicalo.Domain.Privacy;
using CsCheck;

namespace Clicalo.Domain.Tests.Execution.Support;

/// <summary>Generated engine scenarios of up to 200 steps (blueprint §7.10, item 1).</summary>
internal static class EngineScenarios
{
    /// <summary>The shortcuts a scenario touches: every action kind, shared keys, limits, confirmation, refusals.</summary>
    public static IReadOnlyList<Shortcut> Pool { get; } =
    [
        Shortcuts.Tap("copy", "ctrl", "c"),
        Shortcuts.Tap("upper", "shift", "a"),
        Shortcuts.Tap("start", "win"),
        Shortcuts.Of("close", new TapAction(Chords.Of("alt", "f4"), []), Shortcuts.Confirming),
        Shortcuts.Tap("enye", "ctrl", "char:ñ"),
        Shortcuts.Tap("hash", "char:#"),
        Shortcuts.Tap("empty"),
        Shortcuts.Tap("cad", "ctrl", "alt", "delete"),
        Shortcuts.Hold("shift", "shift"),
        Shortcuts.Hold("ctrlalt", "ctrl", "alt"),
        Shortcuts.Of(
            "hold2s",
            new HoldAction(Chords.Of("ctrl", "shift")),
            Shortcuts.Limited(TimeSpan.FromSeconds(2))
        ),
        Shortcuts.Of("holdnever", new HoldAction(Chords.Of("win")), Shortcuts.Unlimited),
        Shortcuts.Toggle("lctrl", "ctrl"),
        Shortcuts.Toggle("lshift", "shift"),
        Shortcuts.Text("text", "¡Hola, ñandú!"),
        Shortcuts.Text("paste", "pegar", TextMethod.Paste),
        Shortcuts.Mouse("right", MouseOp.RightClick),
        Shortcuts.Mouse("drag", MouseOp.Drag),
        Shortcuts.Mouse("scroll", MouseOp.ScrollDown, ScrollSpeed.Fast),
        Shortcuts.Macro(
            "macro",
            new KeysStep(Chords.Of("ctrl", "c")),
            new WaitStep(TimeSpan.FromMilliseconds(300)),
            new MouseStep(MouseOp.Drag),
            new KeysStep(Chords.Of("shift", "tab")),
            new TextStep(SecretText.From("x")),
            new MouseStep(MouseOp.Drag)
        ),
        Shortcuts.Macro(
            "dragmacro",
            new MouseStep(MouseOp.Drag),
            new WaitStep(TimeSpan.FromSeconds(3))
        ),
        Shortcuts.Url("web", "https://example.com"),
        Shortcuts.App("app", @"C:\Windows\notepad.exe"),
        Shortcuts.System("lock", "lock"),
    ];

    private static readonly Gen<EngineOp> Op = Gen.Select(
        Gen.Int[0, 99],
        Gen.Int[0, 1_000],
        Gen.Int[0, 1_000],
        static (kind, a, b) =>
            kind switch
            {
                < 22 => new EngineOp(EngineOpKind.Tap, a, b),
                < 32 => new EngineOp(EngineOpKind.Press, a, b),
                < 40 => new EngineOp(EngineOpKind.Invoke, a, b),
                < 52 => new EngineOp(EngineOpKind.Lift, a, b),
                < 66 => new EngineOp(EngineOpKind.Wait, a % 120, b),
                < 72 => new EngineOp(EngineOpKind.Wait, 100 + (a * 2), b),
                < 73 => new EngineOp(EngineOpKind.Wait, 2_000 + (a * 60), b),
                < 77 => new EngineOp(EngineOpKind.Switch, a, b),
                < 78 => new EngineOp(EngineOpKind.Layout, a, b),
                < 80 => new EngineOp(EngineOpKind.Terminal, a, b),
                < 82 => new EngineOp(EngineOpKind.ReleaseAll, a, b),
                < 85 => new EngineOp(EngineOpKind.FailPress, a, b),
                < 90 => new EngineOp(EngineOpKind.ShellResult, a, b),
                < 92 => new EngineOp(EngineOpKind.TestMode, a, b),
                < 94 => new EngineOp(EngineOpKind.Pause, a, b),
                < 96 => new EngineOp(EngineOpKind.Config, a, b),
                < 98 => new EngineOp(EngineOpKind.Mode, a, b),
                _ => new EngineOp(EngineOpKind.Resume, a, b),
            }
    );

    /// <summary>Sequences of 1 to 200 steps.</summary>
    public static Gen<EngineOp[]> Scenario { get; } = Op.Array[1, 200];
}
