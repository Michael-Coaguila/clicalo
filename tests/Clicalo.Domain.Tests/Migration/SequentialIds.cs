using System.Globalization;
using Clicalo.Domain.Primitives;

namespace Clicalo.Domain.Tests.Migration;

/// <summary>A deterministic id source (<c>p1</c>, <c>s1</c>…), so two conversions can be compared.</summary>
internal sealed class SequentialIds : IIdGenerator
{
    private int _profiles;
    private int _shortcuts;

    public ProfileId NewProfileId() =>
        new("p" + (++_profiles).ToString(CultureInfo.InvariantCulture));

    public ShortcutId NewShortcutId() =>
        new("s" + (++_shortcuts).ToString(CultureInfo.InvariantCulture));
}
