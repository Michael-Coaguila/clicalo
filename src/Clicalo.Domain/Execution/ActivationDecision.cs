using Clicalo.Domain.Keys;
using Clicalo.Domain.Library;
using Clicalo.Domain.Primitives;
using Clicalo.Domain.Touch;

namespace Clicalo.Domain.Execution;

/// <summary>What the single activation policy decides (EJE-001, blueprint §7.2).</summary>
public abstract record ActivationDecision
{
    private ActivationDecision() { }

    /// <summary>The touch filter ignored the contact (TAC-002).</summary>
    /// <param name="Verdict">Why.</param>
    public sealed record Ignored(TouchVerdict Verdict) : ActivationDecision;

    /// <summary>Edit mode: open the editor instead of running.</summary>
    /// <param name="Shortcut">The shortcut to edit.</param>
    public sealed record OpenEditor(ShortcutId Shortcut) : ActivationDecision;

    /// <summary>Test mode: mark the tile and send nothing (TAC-008, INV-7).</summary>
    /// <param name="Accepted">Whether the filter accepted the touch.</param>
    /// <param name="Verdict">The filter verdict.</param>
    public sealed record TestMark(bool Accepted, TouchVerdict Verdict) : ActivationDecision;

    /// <summary>The foreground app is elevated and Clícalo is not: send nothing (EJE-013).</summary>
    public sealed record BlockedElevated : ActivationDecision;

    /// <summary>First tap of a shortcut that asks for confirmation (EJE-002).</summary>
    /// <param name="Shortcut">The armed shortcut.</param>
    /// <param name="Until">When it disarms.</param>
    public sealed record Armed(ShortcutId Shortcut, DateTimeOffset Until) : ActivationDecision;

    /// <summary>The shortcut cannot run (EJE-014, EJE-015).</summary>
    /// <param name="Reason">Why.</param>
    public sealed record Refused(RefusalReason Reason) : ActivationDecision;

    /// <summary>Run the shortcut; the planner of its kind produces the effects.</summary>
    /// <param name="Shortcut">The shortcut.</param>
    /// <param name="Injection">The mode of the profile it was activated from (D24).</param>
    public sealed record Execute(Shortcut Shortcut, InjectionMode Injection) : ActivationDecision;

    /// <summary>A second tap on a running macro cancels it and releases what it holds (EJE-010).</summary>
    /// <param name="Run">The run to cancel.</param>
    public sealed record CancelMacro(MacroRunId Run) : ActivationDecision;
}
