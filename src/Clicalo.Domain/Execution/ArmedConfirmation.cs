using Clicalo.Domain.Primitives;

namespace Clicalo.Domain.Execution;

/// <summary>A shortcut armed by its first tap and waiting for the second (EJE-002).</summary>
/// <param name="Shortcut">The armed shortcut.</param>
/// <param name="Until">When it disarms (<c>Timings.Confirmation.ExecuteConfirmWindow</c>).</param>
public sealed record ArmedConfirmation(ShortcutId Shortcut, DateTimeOffset Until);
