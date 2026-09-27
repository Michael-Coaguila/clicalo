using System.Buffers;
using Clicalo.Platform.Core.KeyLedger;

namespace Clicalo.Platform.Core.Injection;

/// <summary>
/// The generation fence (blueprint §3.2, rule 6, ADR-0004). Every effect with external consequences runs inside one
/// lock that first compares the caller's generation with the ledger's:
/// <code>
/// lock (gate) {
///   if (Volatile.Read(ledger.EngineGeneration) != g) return Fenced;   // a zombie thread sends nothing
///   ledger.BeginDown(...); SendInput(...); ledger.Commit(...);
/// }
/// </code>
/// </summary>
/// <remarks>
/// <para>
/// Write-ahead (INV-2): every key down of a batch is recorded (<see cref="LedgerSlotState.DownPending"/>) and every
/// mouse button down marked before the batch reaches <c>SendInput</c>; after it, the events that went are committed
/// (down to <see cref="LedgerSlotState.Down"/>, up to a released reference) and the downs that did not go are rolled
/// back. An up that did not go leaves its key recorded, and the secure desktop's refusal marks it
/// <see cref="LedgerSlotState.ReleasePending"/>: at no instant is a key down without being recorded.
/// </para>
/// <para>
/// A batch that would need a 129th slot is refused whole before anything is sent.
/// </para>
/// </remarks>
public sealed class InjectionGate
{
    /// <summary><c>ERROR_ACCESS_DENIED</c>: <c>SendInput</c> is refused while the secure desktop is in front.</summary>
    public const int AccessDenied = 5;

    /// <summary><c>ERROR_OUTOFMEMORY</c>: the ledger has no free slot for a key down; nothing was sent.</summary>
    public const int LedgerFull = 14;

    private const int StackSlots = 64;

    private readonly Lock _gate = new();
    private readonly KeyLedgerSection _ledger;
    private readonly ILowLevelSender _sender;

    /// <summary>Creates the gate of a ledger.</summary>
    /// <param name="ledger">The engine's writable ledger.</param>
    /// <param name="sender">The real <see cref="LowLevelInjector"/>, or the tests' physical state injector.</param>
    public InjectionGate(KeyLedgerSection ledger, ILowLevelSender sender)
    {
        ArgumentNullException.ThrowIfNull(ledger);
        ArgumentNullException.ThrowIfNull(sender);
        if (ledger.IsReadOnly)
        {
            throw new ArgumentException(
                "The gate needs the engine's writable ledger.",
                nameof(ledger)
            );
        }

        _ledger = ledger;
        _sender = sender;
    }

    /// <summary>The ledger this gate writes.</summary>
    public KeyLedgerSection Ledger => _ledger;

    /// <summary>
    /// Sends a batch under the fence, recording key downs before sending and committing key ups after (INV-2).
    /// </summary>
    /// <param name="generation">The caller's generation.</param>
    /// <param name="batch">The events.</param>
    public GateOutcome TryInject(ulong generation, ReadOnlySpan<LowLevelInput> batch)
    {
        lock (_gate)
        {
            if (_ledger.Generation != generation)
            {
                return new GateOutcome(GateResult.Fenced, default);
            }

            return new GateOutcome(GateResult.Ran, SendRecorded(batch));
        }
    }

    /// <summary>
    /// Sends a balanced chord under the fence (Clícalo's own internal keys, blueprint §3.6): presses in order the keys
    /// that are not already down and releases them in reverse order in the same batch, so a key an engine holder keeps
    /// is neither pressed again nor released under it.
    /// </summary>
    /// <param name="generation">The caller's generation.</param>
    /// <param name="chord">The keys, in press order.</param>
    public GateOutcome TryInjectChord(ulong generation, ReadOnlySpan<PhysicalKey> chord)
    {
        lock (_gate)
        {
            if (_ledger.Generation != generation)
            {
                return new GateOutcome(GateResult.Fenced, default);
            }

            var batch = new List<LowLevelInput>(chord.Length * 2);
            var pressed = new List<PhysicalKey>(chord.Length);
            foreach (var key in chord)
            {
                if (!_ledger.TryFindDown(key, out _) && !pressed.Contains(key))
                {
                    pressed.Add(key);
                    batch.Add(LowLevelInput.KeyDown(key));
                }
            }

            for (var i = pressed.Count - 1; i >= 0; i--)
            {
                batch.Add(LowLevelInput.KeyUp(pressed[i]));
            }

            return new GateOutcome(GateResult.Ran, SendRecorded([.. batch]));
        }
    }

    /// <summary>
    /// Runs another external effect under the fence (clipboard paste, handing a launch or system command to the Shell
    /// thread).
    /// </summary>
    /// <typeparam name="TState">Caller state, to avoid closures.</typeparam>
    /// <param name="generation">The caller's generation.</param>
    /// <param name="state">Passed to <paramref name="effect"/>.</param>
    /// <param name="effect">The effect.</param>
    public GateResult TryRun<TState>(ulong generation, TState state, Action<TState> effect)
    {
        ArgumentNullException.ThrowIfNull(effect);
        lock (_gate)
        {
            if (_ledger.Generation != generation)
            {
                return GateResult.Fenced;
            }

            effect(state);
            return GateResult.Ran;
        }
    }

    /// <summary>
    /// Writes the engine heartbeat under the fence (blueprint §3.2, rule 6: writes to the ledger go through the gate):
    /// a zombie host, whose generation an emergency already raised, never renews the heartbeat of the engine that
    /// replaced it, so a later hang of that engine is still seen.
    /// </summary>
    /// <param name="generation">The caller's generation.</param>
    /// <param name="ticks">Now, in the engine clock's ticks.</param>
    public GateResult TryWriteHeartbeat(ulong generation, long ticks)
    {
        lock (_gate)
        {
            if (_ledger.Generation != generation)
            {
                return GateResult.Fenced;
            }

            _ledger.WriteHeartbeat(ticks);
            return GateResult.Ran;
        }
    }

    /// <summary>
    /// Sets and clears header marks under the fence (the engine's <c>EngineAlive</c> and the marks of its terminal
    /// events): a zombie host never clears the mark of the engine that replaced it, nor marks a clean shutdown.
    /// </summary>
    /// <param name="generation">The caller's generation.</param>
    /// <param name="toSet">Marks to set.</param>
    /// <param name="toClear">Marks to clear.</param>
    public GateResult TryUpdateMarks(ulong generation, LedgerMarks toSet, LedgerMarks toClear)
    {
        lock (_gate)
        {
            if (_ledger.Generation != generation)
            {
                return GateResult.Fenced;
            }

            if (toSet != LedgerMarks.None)
            {
                _ledger.SetMarks(toSet);
            }

            if (toClear != LedgerMarks.None)
            {
                _ledger.ClearMarks(toClear);
            }

            return GateResult.Ran;
        }
    }

    /// <summary>
    /// The emergency of a hung engine (SysEvents, after <c>Timings.Engine.EngineStallThreshold</c> without heartbeat):
    /// tries to take the gate for <paramref name="wait"/> (<c>Timings.Engine.EmergencyGateWait</c>); if it can, raises
    /// the generation and releases everything recorded inside the lock.
    /// </summary>
    /// <param name="wait">How long to try to take the gate.</param>
    /// <param name="newGeneration">The generation of the new engine when released.</param>
    public EmergencyOutcome TryEmergencyRelease(TimeSpan wait, out ulong newGeneration)
    {
        if (!_gate.TryEnter(wait))
        {
            newGeneration = 0;
            return EmergencyOutcome.GateBusy;
        }

        try
        {
            // From here on, the old engine's thread gets Fenced for everything, even if it resumes (INV-11).
            newGeneration = _ledger.IncrementGeneration();
            ReleaseEverythingRecorded();
            return EmergencyOutcome.Released;
        }
        finally
        {
            _gate.Exit();
        }
    }

    /// <summary>
    /// Releases everything the ledger records under the fence, with the caller's generation (the soltado of an engine
    /// that caught an exception, NFR-005): the same batch as the emergency, without raising the generation.
    /// </summary>
    /// <param name="generation">The caller's generation.</param>
    public GateResult TryReleaseEverything(ulong generation)
    {
        lock (_gate)
        {
            if (_ledger.Generation != generation)
            {
                return GateResult.Fenced;
            }

            ReleaseEverythingRecorded();
            return GateResult.Ran;
        }
    }

    /// <summary>
    /// Sends again, under the fence, the key ups the ledger keeps <see cref="LedgerSlotState.ReleasePending"/> (the
    /// secure desktop refused them): when the input desktop is back (unlock, resume, the end of UAC or Ctrl+Alt+Del),
    /// whoever refused them — an engine's batch, the release of an engine that caught an exception, an emergency —
    /// they go again, even if no engine state remembers them (INV-3). The ones that go free their slots.
    /// </summary>
    /// <param name="generation">The caller's generation.</param>
    /// <param name="count">How many events the batch had (zero when nothing was pending).</param>
    public GateOutcome TryReleasePending(ulong generation, out int count)
    {
        lock (_gate)
        {
            count = 0;
            if (_ledger.Generation != generation)
            {
                return new GateOutcome(GateResult.Fenced, default);
            }

            var batch = LedgerRelease.BuildPendingReleaseBatch(_ledger.Snapshot());
            count = batch.Length;
            if (batch.IsEmpty)
            {
                return new GateOutcome(GateResult.Ran, new SendResult(0, 0));
            }

            var result = _sender.Send(batch.AsSpan());
            var sent = Math.Clamp(result.Sent, 0, batch.Length);
            for (var i = 0; i < sent; i++)
            {
                // Only the gate writes slots and it is held: the slots found are still the pending ones.
                if (
                    batch[i].Kind == LowLevelInputKind.KeyUp
                    && _ledger.TryFindDown(batch[i].Key, out var slot)
                )
                {
                    _ledger.ForceFree(slot);
                }
            }

            return new GateOutcome(GateResult.Ran, result);
        }
    }

    private void ReleaseEverythingRecorded()
    {
        var snapshot = _ledger.Snapshot();
        var batch = LedgerRelease.BuildReleaseBatch(snapshot);
        if (batch.IsEmpty)
        {
            return;
        }

        var result = _sender.Send(batch.AsSpan());
        var sent = Math.Clamp(result.Sent, 0, batch.Length);
        var blocked = sent == 0 && result.LastError == AccessDenied;
        var buttons = _ledger.MouseButtons;
        for (var i = 0; i < batch.Length; i++)
        {
            var input = batch[i];
            switch (input.Kind)
            {
                case LowLevelInputKind.KeyUp when _ledger.TryFindDown(input.Key, out var slot):
                    if (i < sent)
                    {
                        // Every holder of the key is released at once.
                        _ledger.ForceFree(slot);
                    }
                    else if (blocked)
                    {
                        _ledger.MarkReleasePending(slot);
                    }

                    break;
                case LowLevelInputKind.MouseButtonUp when i < sent:
                    buttons &= ~input.Button;
                    break;
            }
        }

        if (buttons != _ledger.MouseButtons)
        {
            _ledger.SetMouseButtons(buttons);
        }
    }

    private SendResult SendRecorded(ReadOnlySpan<LowLevelInput> batch)
    {
        if (batch.IsEmpty)
        {
            return new SendResult(0, 0);
        }

        int[]? rented = null;
        var slots =
            batch.Length <= StackSlots
                ? stackalloc int[batch.Length]
                : (rented = ArrayPool<int>.Shared.Rent(batch.Length)).AsSpan(0, batch.Length);
        try
        {
            // 1. Write-ahead: record every down (and find every up) before anything is sent.
            if (!Record(batch, slots))
            {
                return new SendResult(0, LedgerFull);
            }

            var before = _ledger.MouseButtons;
            var pending = before;
            foreach (var input in batch)
            {
                if (input.Kind == LowLevelInputKind.MouseButtonDown)
                {
                    pending |= input.Button;
                }
            }

            if (pending != before)
            {
                _ledger.SetMouseButtons(pending);
            }

            // 2. Send.
            var result = _sender.Send(batch);
            var sent = Math.Clamp(result.Sent, 0, batch.Length);
            var blocked = sent == 0 && result.LastError == AccessDenied;

            // 3. Commit what went, roll back the downs that did not.
            var buttons = before;
            for (var i = 0; i < batch.Length; i++)
            {
                var input = batch[i];
                switch (input.Kind)
                {
                    case LowLevelInputKind.KeyDown when i < sent:
                        _ledger.CommitDown(slots[i]);
                        break;
                    case LowLevelInputKind.KeyDown:
                        _ledger.RollbackDown(slots[i]);
                        break;
                    case LowLevelInputKind.KeyUp when slots[i] >= 0 && i < sent:
                        _ledger.CommitUp(slots[i]);
                        break;
                    case LowLevelInputKind.KeyUp when slots[i] >= 0 && blocked:
                        _ledger.MarkReleasePending(slots[i]);
                        break;
                    case LowLevelInputKind.MouseButtonDown when i < sent:
                        buttons |= input.Button;
                        break;
                    case LowLevelInputKind.MouseButtonUp when i < sent:
                        buttons &= ~input.Button;
                        break;
                }
            }

            if (buttons != _ledger.MouseButtons)
            {
                _ledger.SetMouseButtons(buttons);
            }

            return result;
        }
        finally
        {
            if (rented is not null)
            {
                ArrayPool<int>.Shared.Return(rented);
            }
        }
    }

    private bool Record(ReadOnlySpan<LowLevelInput> batch, Span<int> slots)
    {
        for (var i = 0; i < batch.Length; i++)
        {
            var input = batch[i];
            slots[i] = -1;
            if (input.Kind == LowLevelInputKind.KeyDown)
            {
                if (!_ledger.TryBeginDown(input.Key, out var slot))
                {
                    // No room: undo this batch's records and send nothing.
                    for (var j = 0; j < i; j++)
                    {
                        if (batch[j].Kind == LowLevelInputKind.KeyDown)
                        {
                            _ledger.RollbackDown(slots[j]);
                        }
                    }

                    return false;
                }

                slots[i] = slot;
            }
            else if (
                input.Kind == LowLevelInputKind.KeyUp
                && _ledger.TryFindDown(input.Key, out var down)
            )
            {
                slots[i] = down;
            }
        }

        return true;
    }
}
