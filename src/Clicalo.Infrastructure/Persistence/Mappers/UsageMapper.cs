using System.Collections.Immutable;
using Clicalo.Domain.Frequents;
using Clicalo.Domain.Primitives;

namespace Clicalo.Infrastructure.Persistence.Mappers;

/// <summary>
/// <see cref="UsageHistory"/> ↔ executions per shortcut in Unix milliseconds (FRE-002), keys in ordinal order so the
/// file and its hash are deterministic.
/// </summary>
internal static class UsageMapper
{
    /// <summary>The persisted form of <paramref name="usage"/>.</summary>
    /// <param name="usage">The usage.</param>
    public static Dictionary<string, List<long>> Encode(UsageHistory usage)
    {
        ArgumentNullException.ThrowIfNull(usage);
        var result = new Dictionary<string, List<long>>(
            usage.Entries.Count,
            StringComparer.Ordinal
        );
        foreach (var pair in usage.Entries.OrderBy(p => p.Key.Value, StringComparer.Ordinal))
        {
            result[pair.Key.Value] = [.. pair.Value.Select(t => t.ToUnixTimeMilliseconds())];
        }

        return result;
    }

    /// <summary>The usage of the persisted form; empty ids and empty lists are dropped.</summary>
    /// <param name="usage">The persisted form, or <see langword="null"/>.</param>
    public static UsageHistory Decode(Dictionary<string, List<long>>? usage)
    {
        if (usage is null || usage.Count == 0)
        {
            return UsageHistory.Empty;
        }

        var builder = ImmutableDictionary.CreateBuilder<ShortcutId, ValueList<DateTimeOffset>>();
        foreach (var pair in usage)
        {
            if (string.IsNullOrEmpty(pair.Key) || pair.Value is not { Count: > 0 } marks)
            {
                continue;
            }

            builder[new ShortcutId(pair.Key)] = ValueListBuilder.From(
                marks.Select(DateTimeOffset.FromUnixTimeMilliseconds)
            );
        }

        return new UsageHistory(builder.ToImmutable());
    }
}
