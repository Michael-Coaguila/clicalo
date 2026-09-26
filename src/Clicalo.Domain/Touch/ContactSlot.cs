using System.Runtime.InteropServices;
using Clicalo.Domain.Geometry;

namespace Clicalo.Domain.Touch;

/// <summary>
/// The state of one contact that is down on the surface. <see cref="GestureRecognizer"/> keeps them in a preallocated
/// array and changes them in place, so following a contact never allocates.
/// </summary>
[StructLayout(LayoutKind.Auto)]
internal struct ContactSlot
{
    /// <summary>The contact identifier.</summary>
    public uint PointerId;

    /// <summary>Where the contact is in its life.</summary>
    public ContactStage Stage;

    /// <summary>Why the contact is ignored, when <see cref="Stage"/> is <see cref="ContactStage.Ignored"/>.</summary>
    public IgnoreReason Reason;

    /// <summary>True when the contact went down on a target or its extra hit area.</summary>
    public bool HasTarget;

    /// <summary>The target the contact went down on, frozen for its whole life (PAN-009).</summary>
    public TouchTarget Target;

    /// <summary>The values the contact started with.</summary>
    public ContactThresholds Thresholds;

    /// <summary>Where the contact went down.</summary>
    public PhysicalPoint DownPosition;

    /// <summary>The latest known position.</summary>
    public PhysicalPoint Position;

    /// <summary>When the contact went down.</summary>
    public DateTimeOffset DownTime;

    /// <summary>When the pending long press or hold start is due.</summary>
    public DateTimeOffset Deadline;

    /// <summary>True while a long press can still happen at <see cref="Deadline"/>.</summary>
    public bool LongPressPending;

    /// <summary>Largest distance from <see cref="DownPosition"/>, in physical pixels.</summary>
    public double MaxDisplacement;

    /// <summary>True once the contact area was palm-sized.</summary>
    public bool PalmLike;

    /// <summary>True once the contact left the extra hit area of its target.</summary>
    public bool LeftTarget;

    /// <summary>The target identifier to report, or null without target.</summary>
    public readonly TouchTargetId? ReportedTarget => HasTarget ? Target.Id : null;

    /// <summary>True when a deadline is pending (long press or hold start).</summary>
    public readonly bool HasDeadline =>
        (Stage == ContactStage.Pending && LongPressPending) || Stage == ContactStage.HoldPending;
}
