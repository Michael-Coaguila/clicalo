namespace Clicalo.Domain.Settings;

/// <summary>Key safety (GEN-012, SEG-004, SEG-005, docs/02 <c>keySafety</c>).</summary>
/// <param name="MaxHold">
/// Global automatic release limit, one of <c>Timings.KeySafety.AutoReleaseChoices</c>; <see langword="null"/> is
/// «Never», which still releases on «Release all» and on system events.
/// </param>
/// <param name="ReleaseOnAppSwitch">Release everything when the app really changes (<c>safeSwitch</c>).</param>
public sealed record KeySafetySettings(TimeSpan? MaxHold, bool ReleaseOnAppSwitch);
