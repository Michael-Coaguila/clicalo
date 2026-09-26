using Clicalo.Domain.Keys;
using Clicalo.Domain.Migration.V1;
using Clicalo.Domain.Primitives;

namespace Clicalo.Domain.Tests.Migration;

/// <summary>Builds v1 documents as <c>V1Reader</c> returns them, for the conversion tests.</summary>
internal static class V1Docs
{
    public const string Blue = "#2980B9";

    /// <summary>The user's touch screen: 2400×1600 at 175 %, taskbar at the bottom.</summary>
    public static V1Monitor Primary { get; } = new(@"\\.\DISPLAY1", 0, 0, 2400, 1520, 1.75, true);

    /// <summary>A second monitor to the right, at 100 %.</summary>
    public static V1Monitor Secondary { get; } =
        new(@"\\.\DISPLAY2", 2400, 0, 1920, 1040, 1.0, false);

    public static V1Button Hotkey(string label, string? hotkey, string? color = Blue) =>
        new(V1ButtonKind.ImplicitHotkey, label, hotkey, null, color, null);

    public static V1Button Url(string label, string? action) =>
        new(V1ButtonKind.Url, label, null, action, Blue, "url");

    public static V1Button App(string label, string? action) =>
        new(V1ButtonKind.App, label, null, action, Blue, "app");

    public static V1Button Separator() =>
        new(V1ButtonKind.Separator, string.Empty, null, null, null, "separator");

    public static V1Profile Profile(string name, string process, params V1Button[] buttons) =>
        new(name, process, null, [.. buttons]);

    public static V1Document V1File(params V1Profile[] profiles) =>
        new(null, null, null, null, null, null, null, [.. profiles], []);

    /// <summary>A document with a General profile and the given buttons in it.</summary>
    public static V1Document General(params V1Button[] buttons) =>
        V1File(Profile("General", string.Empty, buttons));

    public static KeyStroke Key(KeyId key, KeySide side = KeySide.Any) => new(key, side);

    public static ValueList<KeyStroke> Chord(params KeyStroke[] strokes) => [.. strokes];
}
