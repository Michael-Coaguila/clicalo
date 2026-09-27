using Clicalo.Application.Ports;
using Clicalo.Domain.Execution;
using Clicalo.Domain.Touch;

namespace Clicalo.Application.Coordinators;

/// <summary>
/// Turns what happens on a tile into engine events (blueprint §7.1, the <c>PanelInteractionController</c>): it adds the
/// activation context the surface does not know (origin, profile and injection mode, edit mode, foreground epoch) and
/// posts to the engine mailbox. It decides nothing: the single activation policy runs in the engine (EJE-001), so a
/// tap, a hold and a UI Automation invocation of the same tile all end in <see cref="EngineEvent.Activation"/>.
/// </summary>
/// <remarks>
/// Called on the UI thread of the Surfaces role; <see cref="IEngineInbox.Post"/> never blocks. Every method returns
/// whether the engine accepted the event (false only after the engine stopped).
/// </remarks>
public sealed class PanelInteractionController
{
    private readonly IEngineInbox _engine;
    private readonly Func<long> _foregroundEpoch;
    private readonly TimeProvider _time;

    /// <summary>Creates the controller.</summary>
    /// <param name="engine">The engine mailbox.</param>
    /// <param name="foregroundEpoch">
    /// The epoch of the verified external foreground (<see cref="ForegroundChangeCoordinator.CurrentEpoch"/>): the engine
    /// refuses a planned injection whose epoch no longer matches (INV-6).
    /// </param>
    /// <param name="time">Clock of the invocations, which carry no pointer timestamp.</param>
    public PanelInteractionController(
        IEngineInbox engine,
        Func<long> foregroundEpoch,
        TimeProvider time
    )
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(foregroundEpoch);
        ArgumentNullException.ThrowIfNull(time);
        _engine = engine;
        _foregroundEpoch = foregroundEpoch;
        _time = time;
    }

    /// <summary>
    /// An accepted tap lifted on the tile (the gesture recognizer already applied the touch filter of TAC-002): the
    /// activation arrives with <see cref="ActivationPhase.ContactEnded"/> and the contact summary.
    /// </summary>
    /// <param name="tile">The tile.</param>
    /// <param name="contactId">The pointer id of the contact.</param>
    /// <param name="device">Finger, pen or mouse.</param>
    /// <param name="summary">Duration, displacement and palm of the contact.</param>
    /// <param name="at">When the contact lifted.</param>
    public bool Tapped(
        TileBinding tile,
        uint contactId,
        PointerKind device,
        ContactSummary summary,
        DateTimeOffset at
    ) =>
        Activate(
            tile,
            new ActivationRequest(
                ActivationPhase.ContactEnded,
                Origin(device),
                ContactId(contactId),
                summary,
                at
            )
        );

    /// <summary>
    /// A Hold tile starts holding (after the minimum contact and the debounce, EJE-004): the activation arrives with
    /// <see cref="ActivationPhase.ContactStarted"/>, and the keys stay down until <see cref="HoldEnded"/> for the same
    /// contact (INV-9).
    /// </summary>
    /// <param name="tile">The tile.</param>
    /// <param name="contactId">The pointer id of the contact that owns the hold.</param>
    /// <param name="device">Finger, pen or mouse.</param>
    /// <param name="at">When the hold started.</param>
    public bool HoldStarted(
        TileBinding tile,
        uint contactId,
        PointerKind device,
        DateTimeOffset at
    ) =>
        Activate(
            tile,
            new ActivationRequest(
                ActivationPhase.ContactStarted,
                Origin(device),
                ContactId(contactId),
                null,
                at
            )
        );

    /// <summary>
    /// The contact of a hold ended (EJE-004, EJE-006): lifted, cancelled by the system, out of the extra hit area or
    /// reset. Travels in the priority lane, so releasing never waits behind a macro (§3.2 rule 3).
    /// </summary>
    /// <param name="contactId">The pointer id of the contact.</param>
    /// <param name="summary">Duration, displacement and palm of the contact.</param>
    /// <param name="reason">Why the hold ended.</param>
    public bool HoldEnded(uint contactId, ContactSummary summary, HoldEndReason reason) =>
        _engine.Post(
            new EngineEvent.ContactEnded(
                ContactId(contactId),
                summary,
                Cancelled: reason != HoldEndReason.Lifted
            )
        );

    /// <summary>
    /// A UI Automation Invoke or Toggle of the tile (Voice access, Narrator, Windows Speech Recognition, switches;
    /// EJE-005, ACC-004): no contact, no touch filter and no duration, so a Hold behaves as a toggle.
    /// </summary>
    /// <param name="tile">The tile.</param>
    public bool Invoked(TileBinding tile) =>
        Activate(
            tile,
            new ActivationRequest(
                ActivationPhase.Invoke,
                ActivationOrigin.UiaInvoke,
                null,
                null,
                _time.GetUtcNow()
            )
        );

    /// <summary>«Release all» on the panic strip or the tray (SEG-003).</summary>
    public bool ReleaseAll() => _engine.Post(new EngineEvent.ReleaseAll(ReleaseReason.User));

    private static ActivationOrigin Origin(PointerKind device) =>
        device switch
        {
            PointerKind.Finger => ActivationOrigin.Touch,
            PointerKind.Pen => ActivationOrigin.Pen,
            PointerKind.Mouse => ActivationOrigin.Mouse,
            _ => throw new ArgumentOutOfRangeException(nameof(device), device, null),
        };

    // Pointer ids are small positive numbers handed out by Windows; the engine keys holders by int (HolderId).
    private static int ContactId(uint contactId) => unchecked((int)contactId);

    private bool Activate(TileBinding tile, ActivationRequest request)
    {
        ArgumentNullException.ThrowIfNull(tile);
        return _engine.Post(
            new EngineEvent.Activation(
                request,
                tile.Shortcut,
                tile.OriginProfile,
                tile.Injection,
                LastExternalPointer: null,
                EditMode: false,
                _foregroundEpoch(),
                RequiredForeground: null
            )
        );
    }
}
