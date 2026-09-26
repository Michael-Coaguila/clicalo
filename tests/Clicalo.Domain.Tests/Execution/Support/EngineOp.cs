using Clicalo.Domain.Execution;
using Clicalo.Domain.Library;

namespace Clicalo.Domain.Tests.Execution.Support;

/// <summary>
/// One step of a generated engine scenario (blueprint §7.10): touches with several contacts, invocations, timers,
/// app switches, locks, suspensions, «Release all», failed batches, test mode, pause, settings and both injection
/// modes, with Shell and clipboard results arriving in any order.
/// </summary>
/// <param name="Kind">What happens.</param>
/// <param name="A">First parameter (shortcut, contact, milliseconds or choice, by kind).</param>
/// <param name="B">Second parameter.</param>
internal sealed record EngineOp(EngineOpKind Kind, int A, int B)
{
    public override string ToString() => Kind + "(" + A + "," + B + ")";

    /// <summary>Plays the step on <paramref name="engine"/>.</summary>
    public void Play(EngineHarness engine, IReadOnlyList<Shortcut> pool)
    {
        switch (Kind)
        {
            case EngineOpKind.Tap:
                engine.Apply(
                    engine.Activation(
                        pool[A % pool.Count],
                        ActivationPhase.ContactEnded,
                        1 + (B % 3),
                        TimeSpan.FromMilliseconds(40 + (B % 400)),
                        epoch: B % 17 == 0 ? engine.Epoch - 1 : null,
                        editMode: B % 29 == 0
                    )
                );
                break;
            case EngineOpKind.Press:
                engine.Press(pool[A % pool.Count], 1 + (B % 3));
                break;
            case EngineOpKind.Invoke:
                engine.Invoke(pool[A % pool.Count]);
                break;
            case EngineOpKind.Lift:
                engine.Lift(1 + (A % 3), cancelled: B % 2 == 0);
                break;
            case EngineOpKind.Wait:
                engine.Advance(TimeSpan.FromMilliseconds(A));
                break;
            case EngineOpKind.Switch:
                engine.Foreground(
                    A % 2 == 0 ? "notepad" : "word",
                    (B % 7) switch
                    {
                        0 => ElevationState.TargetElevated,
                        1 => ElevationState.Unknown,
                        _ => ElevationState.Allowed,
                    },
                    userSwitch: B % 3 != 0,
                    B % 5 == 0 ? Layouts.English : Layouts.Spanish
                );
                break;
            case EngineOpKind.Layout when engine.State.Foreground is not null:
                engine.Apply(
                    new EngineEvent.LayoutChanged(A % 2 == 0 ? Layouts.English : Layouts.Spanish)
                );
                break;
            case EngineOpKind.Terminal:
                var reasons = Enum.GetValues<TerminalReason>();
                engine.Apply(new EngineEvent.Terminal(reasons[A % reasons.Length]));
                break;
            case EngineOpKind.ReleaseAll:
                engine.Apply(
                    new EngineEvent.ReleaseAll(
                        A % 2 == 0 ? ReleaseReason.User : ReleaseReason.AppSwitch
                    )
                );
                break;
            case EngineOpKind.FailPress when engine.SimulateFailures:
                // The next press batch will be taken only in part (INV-5), whenever it goes.
                engine.FailNextPress = A % 4;
                break;
            case EngineOpKind.ShellResult when !engine.State.PendingExternal.IsEmpty:
                var pending = engine
                    .State.PendingExternal.Values.OrderBy(static p => p.Id.Value)
                    .ToList();
                var chosen = pending[A % pending.Count];
                engine.Apply(
                    chosen.Kind switch
                    {
                        PendingKind.Paste => new EngineEvent.ClipboardReady(chosen.Id),
                        PendingKind.System => new EngineEvent.SystemCommandCompleted(
                            chosen.Id,
                            B % 2 == 0
                        ),
                        _ => new EngineEvent.LaunchCompleted(chosen.Id),
                    }
                );
                break;
            case EngineOpKind.TestMode:
                engine.Apply(new EngineEvent.SetTestMode(A % 3 == 0));
                break;
            case EngineOpKind.Pause:
                engine.Apply(new EngineEvent.SetPaused(A % 3 == 0));
                break;
            case EngineOpKind.Config:
                engine.Apply(
                    new EngineEvent.ConfigChanged(
                        engine.Config with
                        {
                            MaxHold = (A % 4) switch
                            {
                                0 => null,
                                1 => TimeSpan.FromSeconds(30),
                                2 => TimeSpan.FromSeconds(2),
                                _ => TimeSpan.FromSeconds(60),
                            },
                            InterEventDelay =
                                B % 3 == 0 ? TimeSpan.Zero : TimeSpan.FromMilliseconds(20),
                            ReleaseOnAppSwitch = B % 5 != 0,
                        }
                    )
                );
                break;
            case EngineOpKind.Mode:
                engine.Mode =
                    A % 2 == 0
                        ? Clicalo.Domain.Keys.InjectionMode.VirtualKey
                        : Clicalo.Domain.Keys.InjectionMode.ScanCode;
                break;
            case EngineOpKind.Resume:
                engine.Apply(new EngineEvent.SessionResumed());
                break;
            case EngineOpKind.Sticky:
                engine.Apply(
                    B % 7 == 0
                        ? new EngineEvent.ClearSticky()
                        : new EngineEvent.StickyTapped((Clicalo.Domain.Keys.ModifierKind)(A % 4))
                );
                break;
        }
    }
}
