using System.Collections.Frozen;
using Clicalo.Domain.Library;
using Clicalo.Domain.Settings;

namespace Clicalo.Infrastructure.Persistence.Mappers;

/// <summary>
/// The persisted names of the Domain enums (schema 1.0). They are part of the format, so they never follow a rename in
/// C#; the mouse ids are those of <c>data/catalogs/mouse.json</c> and the setting names those of docs/02.
/// </summary>
internal static class PersistedNames
{
    public static NameMap<ThemeChoice> Theme { get; } =
        new(
            (ThemeChoice.Auto, "auto"),
            (ThemeChoice.Dark, "dark"),
            (ThemeChoice.Light, "light"),
            (ThemeChoice.HighContrast, "hc")
        );

    public static NameMap<PanelDensity> Density { get; } =
        new(
            (PanelDensity.Full, "full"),
            (PanelDensity.Compact, "compact"),
            (PanelDensity.Dock, "dock")
        );

    public static NameMap<PanelSize> Size { get; } =
        new((PanelSize.Small, "S"), (PanelSize.Medium, "M"), (PanelSize.Large, "L"));

    public static NameMap<DockSide> DockSide { get; } =
        new(
            (Domain.Settings.DockSide.Right, "right"),
            (Domain.Settings.DockSide.Left, "left"),
            (Domain.Settings.DockSide.Top, "top"),
            (Domain.Settings.DockSide.Bottom, "bottom")
        );

    public static NameMap<UpdateChannel> Channel { get; } =
        new((UpdateChannel.Stable, "stable"), (UpdateChannel.Beta, "beta"));

    public static NameMap<MouseOp> Mouse { get; } =
        new(
            (MouseOp.RightClick, "rclick"),
            (MouseOp.DoubleClick, "dbl"),
            (MouseOp.MiddleClick, "mid"),
            (MouseOp.Drag, "drag"),
            (MouseOp.ScrollUp, "sup"),
            (MouseOp.ScrollDown, "sdn"),
            (MouseOp.ScrollLeft, "sleft"),
            (MouseOp.ScrollRight, "sright")
        );

    public static NameMap<ScrollSpeed> Speed { get; } =
        new((ScrollSpeed.Slow, "slow"), (ScrollSpeed.Normal, "normal"), (ScrollSpeed.Fast, "fast"));

    public static NameMap<TextMethod> TextMethod { get; } =
        new(
            (Domain.Library.TextMethod.Unicode, "type"),
            (Domain.Library.TextMethod.Paste, "paste")
        );

    public static NameMap<ActionKind> Action { get; } =
        new(
            (ActionKind.Tap, "tap"),
            (ActionKind.Hold, "hold"),
            (ActionKind.Toggle, "toggle"),
            (ActionKind.Text, "text"),
            (ActionKind.Mouse, "mouse"),
            (ActionKind.Macro, "macro"),
            (ActionKind.Url, "url"),
            (ActionKind.App, "app"),
            (ActionKind.System, "system")
        );

    /// <summary>A two-way, ordinal map between the values of <typeparamref name="T"/> and their persisted names.</summary>
    /// <typeparam name="T">An enum.</typeparam>
    internal sealed class NameMap<T>
        where T : struct, Enum
    {
        private readonly FrozenDictionary<T, string> _names;
        private readonly FrozenDictionary<string, T> _values;

        public NameMap(params (T Value, string Name)[] pairs)
        {
            _names = pairs.ToFrozenDictionary(p => p.Value, p => p.Name);
            _values = pairs.ToFrozenDictionary(p => p.Name, p => p.Value, StringComparer.Ordinal);
        }

        public string Name(T value) =>
            _names.TryGetValue(value, out var name)
                ? name
                : throw new ArgumentOutOfRangeException(nameof(value), value, "No persisted name.");

        public bool TryParse(string? name, out T value)
        {
            if (name is not null && _values.TryGetValue(name, out value))
            {
                return true;
            }

            value = default;
            return false;
        }

        public T ParseOr(string? name, T fallback) =>
            TryParse(name, out var value) ? value : fallback;
    }
}
