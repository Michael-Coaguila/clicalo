using System.Collections.Frozen;
using System.Globalization;
using System.Text.RegularExpressions;
using Clicalo.Domain.Catalog;
using Clicalo.Domain.Keys;
using Clicalo.Domain.Library;
using Clicalo.Domain.Primitives;
using Clicalo.Domain.Timing;

namespace Clicalo.Domain.Templates;

/// <summary>
/// The semantic check of an AI answer (blueprint §9.2, step 2; PLA-008, LOG-006): what of an
/// <see cref="AiTemplateProposal"/> can reach the preview. Pure.
/// <list type="bullet">
/// <item>Only taps: a proposal can only press combinations of keys of the catalog, never open, type or run anything.</item>
/// <item>Names of at most <c>Timings.Ai.AiNameMaxLength</c> characters, without control or bidirectional characters.</item>
/// <item>Blocked combinations are left out; a repeated combination is kept once; dangerous ones (Alt+F4, Ctrl+W,
/// Supr) stay and the preview marks them (<see cref="IsDangerous"/>).</item>
/// <item>The process is a plain executable name, and only for a known program (PLA-007).</item>
/// </list>
/// A proposal with nothing left, or with more than <c>Timings.Ai.AiMaxShortcuts</c> shortcuts, is invalid.
/// </summary>
public static partial class TemplateSchema
{
    /// <summary>The id of the template an AI proposal becomes.</summary>
    public const string AiTemplateId = "ai";

    private static readonly FrozenSet<string>[] Dangerous =
    [
        new[] { "alt", "f4" }.ToFrozenSet(StringComparer.Ordinal),
        new[] { "ctrl", "w" }.ToFrozenSet(StringComparer.Ordinal),
        new[] { "delete" }.ToFrozenSet(StringComparer.Ordinal),
    ];

    /// <summary>The ten colour categories (docs/07): anything else becomes <c>edit</c>.</summary>
    public static FrozenSet<string> Categories { get; } =
        new[]
        {
            "edit",
            "hist",
            "file",
            "sel",
            "win",
            "voice",
            "nav",
            "fmt",
            "web",
            "text",
        }.ToFrozenSet(StringComparer.Ordinal);

    /// <summary>
    /// The proposal as a template, or <see langword="null"/> when nothing valid is left (the «invalid» error, PLA-006).
    /// </summary>
    /// <param name="proposal">The structurally valid answer.</param>
    /// <param name="requested">The program name the person asked for: the profile name when the AI's is not usable.</param>
    /// <param name="isBlocked">Whether a combination is blocked (EJE-014).</param>
    /// <param name="isKnownIcon">Whether an icon exists in the icon library; unknown icons become the default.</param>
    public static AiTemplate? Validate(
        AiTemplateProposal proposal,
        string requested,
        Func<KeyChord, bool> isBlocked,
        Func<string, bool> isKnownIcon
    )
    {
        ArgumentNullException.ThrowIfNull(proposal);
        ArgumentNullException.ThrowIfNull(isBlocked);
        ArgumentNullException.ThrowIfNull(isKnownIcon);
        if (proposal.Shortcuts.Count > Timings.Ai.AiMaxShortcuts)
        {
            return null;
        }

        var shortcuts = new List<TemplateShortcut>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in proposal.Shortcuts)
        {
            if (
                SafeName(item.NameEs) is not { } es
                || SafeName(item.NameEn) is not { } en
                || ChordOf(item.Keys) is not { } chord
                || isBlocked(chord)
                || !CanonicalChord.TryFrom(chord, out var canonical)
                || !seen.Add(canonical.ToStableString())
            )
            {
                continue;
            }

            shortcuts.Add(
                new TemplateShortcut(
                    AiTemplateId
                        + (shortcuts.Count + 1).ToString(CultureInfo.InvariantCulture),
                    new LocalizedText([new(LangCode.Es, es), new(LangCode.En, en)]),
                    Icon(item.Icon, isKnownIcon, "bolt"),
                    new CategoryId(Categories.Contains(item.Category) ? item.Category : "edit"),
                    new TapAction(chord, []),
                    false
                )
            );
        }

        if (shortcuts.Count == 0)
        {
            return null;
        }

        var name = SafeName(proposal.App) ?? SafeName(requested) ?? AiTemplateId;
        var process = proposal.Known ? ProcessOf(proposal.Process) : null;
        return new AiTemplate(
            new ProfileTemplate(
                AiTemplateId,
                1,
                LocalizedText.Same(name, LangCode.Es, LangCode.En),
                Icon(proposal.Icon, isKnownIcon, "apps"),
                process is { } p ? [p] : [],
                [.. shortcuts]
            ),
            proposal.Known
        );
    }

    /// <summary>
    /// Whether <paramref name="chord"/> closes or deletes something at once (Alt+F4, Ctrl+W, Supr): the preview marks it
    /// so the person reviews it before installing (blueprint §9.2).
    /// </summary>
    /// <param name="chord">The combination.</param>
    public static bool IsDangerous(KeyChord chord)
    {
        ArgumentNullException.ThrowIfNull(chord);
        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var stroke in chord.Strokes)
        {
            var id = stroke.Key;
            if (KeyDefinitions.TryGet(id, out var definition) && definition.BaseKey is { } baseKey)
            {
                id = baseKey;
            }

            _ = keys.Add(id.Value ?? string.Empty);
        }

        return Dangerous.Any(d => d.SetEquals(keys));
    }

    /// <summary>
    /// <paramref name="text"/> trimmed, when it is a name a shortcut can show: not empty, at most
    /// <c>Timings.Ai.AiNameMaxLength</c> characters, and without control or bidirectional formatting characters.
    /// </summary>
    /// <param name="text">The text.</param>
    public static string? SafeName(string? text)
    {
        var name = (text ?? string.Empty).Trim();
        if (name.Length == 0 || name.Length > Timings.Ai.AiNameMaxLength)
        {
            return null;
        }

        foreach (var c in name)
        {
            if (char.IsControl(c) || IsBidiOrInvisible(c))
            {
                return null;
            }
        }

        return name;
    }

    private static bool IsBidiOrInvisible(char c) =>
        c is '؜' or '​' or '‌' or '‍' or '‎' or '‏' or '﻿'
        || c is >= '‪' and <= '‮'
        || c is >= '⁦' and <= '⁩';

    private static KeyChord? ChordOf(ValueList<string> keys)
    {
        var strokes = new List<KeyStroke>(keys.Count);
        foreach (var key in keys)
        {
            var id = new KeyId((key ?? string.Empty).Trim().ToLowerInvariant());
            if (!KeyDefinitions.TryGet(id, out _))
            {
                return null;
            }

            strokes.Add(new KeyStroke(id));
        }

        var chord = KeyChord.Create(strokes);
        return chord.IsEmpty || chord.IsModifiersOnly ? null : chord;
    }

    private static IconRef Icon(string? icon, Func<string, bool> isKnownIcon, string fallback) =>
        new(
            !string.IsNullOrEmpty(icon) && IconName().IsMatch(icon) && isKnownIcon(icon)
                ? icon
                : fallback
        );

    private static ProcessName? ProcessOf(string? process)
    {
        var name = (process ?? string.Empty).Trim();
        return ProcessPattern().IsMatch(name) ? new ProcessName(name) : null;
    }

    [GeneratedRegex(
        "^[a-z0-9_]{1,40}$",
        RegexOptions.CultureInvariant,
        matchTimeoutMilliseconds: 1000
    )]
    private static partial Regex IconName();

    [GeneratedRegex(
        @"^[A-Za-z0-9][A-Za-z0-9 ._()+-]{0,59}\.exe$",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase,
        matchTimeoutMilliseconds: 1000
    )]
    private static partial Regex ProcessPattern();
}
