using System.Collections.Frozen;
using System.Reflection;
using System.Windows;
using Clicalo.UI.Wpf.Theming.Generated;

namespace Clicalo.UI.Wpf.Controls;

/// <summary>
/// Resource key of the frozen brush of one color token (TEM-002). <see cref="ThemeScope"/> writes one brush per key
/// into the resources of a surface; controls reference them with <c>SetResourceReference</c> (or
/// <c>{DynamicResource}</c>), so a theme or contrast change repaints them in place without rebuilding the tree (S3).
/// </summary>
/// <remarks>There is one shared instance per token (<see cref="For"/>); keys compare by token.</remarks>
public sealed class ThemeBrushKey : ResourceKey, IEquatable<ThemeBrushKey>
{
    private static readonly FrozenDictionary<ColorToken, ThemeBrushKey> Keys =
        Enum.GetValues<ColorToken>()
            .ToFrozenDictionary(token => token, token => new ThemeBrushKey(token));

    private ThemeBrushKey(ColorToken token) => Token = token;

    /// <summary>The color token whose brush this key names.</summary>
    public ColorToken Token { get; }

    /// <summary>Every key, one per color token.</summary>
    public static IEnumerable<ThemeBrushKey> All => Keys.Values;

    /// <inheritdoc />
    public override Assembly Assembly => typeof(ThemeBrushKey).Assembly;

    /// <summary>The key of <paramref name="token"/>.</summary>
    public static ThemeBrushKey For(ColorToken token) =>
        Keys.TryGetValue(token, out var key)
            ? key
            : throw new ArgumentOutOfRangeException(nameof(token), token, message: null);

    /// <inheritdoc />
    public bool Equals(ThemeBrushKey? other) => other is not null && other.Token == Token;

    /// <inheritdoc />
    public override bool Equals(object? obj) => Equals(obj as ThemeBrushKey);

    /// <inheritdoc />
    public override int GetHashCode() => Token.GetHashCode();

    /// <summary>The token name, for diagnostics.</summary>
    public override string ToString() => Token.ToString();
}
