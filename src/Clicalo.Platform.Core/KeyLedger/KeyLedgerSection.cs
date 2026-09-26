using System.Collections.Immutable;
using System.ComponentModel;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.System.Memory;

namespace Clicalo.Platform.Core.KeyLedger;

/// <summary>
/// The physical ledger v2 (blueprint §7.4, ADR-0004): 4 KiB of <b>unnamed</b> shared memory
/// (<c>CreateFileMapping(INVALID_HANDLE_VALUE)</c>) with write-ahead recording of every key down, inheritable only by
/// Sentinel through a read-only duplicate. Key slots are written only inside <c>InjectionGate</c>; there is no instant
/// when a key is down without being recorded (INV-2), and no thread with an old generation can press anything.
/// </summary>
/// <remarks>
/// <para>
/// Single writer: the gate's lock serializes every slot write, and the sequence number works as a sequence lock
/// (odd while a write is in progress), so <see cref="Snapshot"/> returns a consistent copy. A slot's key is written
/// before its state, so a slot that is not <see cref="LedgerSlotState.Free"/> always names a complete key, even if
/// the process died in the middle of a write; in that case the sequence stays odd and the snapshot gives up waiting
/// after a few rounds and reads what is there.
/// </para>
/// <para>The byte layout is <see cref="KeyLedgerLayout"/> (ADR-0018): changing it is layout version 3.</para>
/// </remarks>
public sealed unsafe class KeyLedgerSection : IDisposable
{
    private const int KeyFieldOffset = 0;
    private const int ScanFieldOffset = 2;
    private const int FlagsFieldOffset = 4;
    private const int StateFieldOffset = 5;
    private const int RefCountFieldOffset = 6;
    private const int SnapshotRounds = 64;
    private const uint FileMapRead = 0x0004;

    private readonly byte* _view;
    private readonly HANDLE _mapping;
    private readonly bool _inMemory;
    private int _disposed;

    private KeyLedgerSection(byte* view, HANDLE mapping, bool readOnly, bool inMemory)
    {
        _view = view;
        _mapping = mapping;
        IsReadOnly = readOnly;
        _inMemory = inMemory;
    }

    /// <summary>Whether this view can only read (Sentinel).</summary>
    public bool IsReadOnly { get; }

    /// <summary>The current engine generation (volatile read).</summary>
    public ulong Generation
    {
        get
        {
            ThrowIfDisposed();
            return (ulong)Volatile.Read(ref *(long*)(_view + KeyLedgerLayout.GenerationOffset));
        }
    }

    /// <summary>The header marks.</summary>
    public LedgerMarks Marks
    {
        get
        {
            ThrowIfDisposed();
            return (LedgerMarks)(ushort)((uint)Volatile.Read(ref HeaderWord) >> 16);
        }
    }

    /// <summary>The mouse buttons recorded down.</summary>
    public LedgerMouseButtons MouseButtons
    {
        get
        {
            ThrowIfDisposed();
            return (LedgerMouseButtons)
                Volatile.Read(ref *(_view + KeyLedgerLayout.MouseButtonsOffset));
        }
    }

    /// <summary>The last engine heartbeat written.</summary>
    public long LastHeartbeatTicks
    {
        get
        {
            ThrowIfDisposed();
            return Volatile.Read(ref *(long*)(_view + KeyLedgerLayout.HeartbeatOffset));
        }
    }

    private ref int HeaderWord => ref *(int*)(_view + KeyLedgerLayout.LayoutVersionOffset);

    private ref long SequenceField => ref *(long*)(_view + KeyLedgerLayout.SequenceOffset);

    /// <summary>Creates the engine's section, writing the magic, layout version and generation 1.</summary>
    public static KeyLedgerSection CreateForEngine()
    {
        var invalid = new HANDLE(-1);
        var mapping = PInvoke.CreateFileMapping(
            invalid,
            null,
            PAGE_PROTECTION_FLAGS.PAGE_READWRITE,
            0,
            KeyLedgerLayout.SectionSize,
            default
        );
        if (mapping.IsNull)
        {
            throw new Win32Exception(Marshal.GetLastPInvokeError());
        }

        var view = PInvoke.MapViewOfFile(
            mapping,
            FILE_MAP.FILE_MAP_READ | FILE_MAP.FILE_MAP_WRITE,
            0,
            0,
            KeyLedgerLayout.SectionSize
        );
        if (view.Value is null)
        {
            var error = Marshal.GetLastPInvokeError();
            PInvoke.CloseHandle(mapping);
            throw new Win32Exception(error);
        }

        var section = new KeyLedgerSection(
            (byte*)view.Value,
            mapping,
            readOnly: false,
            inMemory: false
        );
        section.Initialize();
        return section;
    }

    /// <summary>Maps the read-only section Sentinel inherited; validates magic and layout version.</summary>
    /// <param name="inheritedHandle">The handle value received in <c>SentinelStartInfo</c>.</param>
    /// <exception cref="InvalidDataException">The section has another magic or layout version.</exception>
    public static KeyLedgerSection OpenInherited(nint inheritedHandle) =>
        TryOpenInherited(inheritedHandle, out var section)
            ? section!
            : throw new InvalidDataException(
                "The inherited key ledger is not a layout version "
                    + KeyLedgerLayout.LayoutVersion
                    + " ledger."
            );

    /// <summary>
    /// Maps the read-only section Sentinel inherited; <see langword="false"/> when it cannot be mapped or has another
    /// magic or layout version (Sentinel then exits with <c>LedgerUnreadable</c>).
    /// </summary>
    /// <param name="inheritedHandle">The handle value received in <c>SentinelStartInfo</c>.</param>
    /// <param name="section">The mapped section.</param>
    public static bool TryOpenInherited(nint inheritedHandle, out KeyLedgerSection? section)
    {
        var mapping = new HANDLE(inheritedHandle);
        var view = PInvoke.MapViewOfFile(
            mapping,
            FILE_MAP.FILE_MAP_READ,
            0,
            0,
            KeyLedgerLayout.SectionSize
        );
        if (view.Value is null)
        {
            section = null;
            return false;
        }

        var candidate = new KeyLedgerSection(
            (byte*)view.Value,
            mapping,
            readOnly: true,
            inMemory: false
        );
        if (!candidate.HasValidHeader())
        {
            PInvoke.UnmapViewOfFile(view);
            section = null;
            return false;
        }

        section = candidate;
        return true;
    }

    /// <summary>A private in-memory section with the same layout, for tests («death at every step», §7.10).</summary>
    public static KeyLedgerSection CreateInMemory()
    {
        var memory = (byte*)NativeMemory.AllocZeroed(KeyLedgerLayout.SectionSize);
        var section = new KeyLedgerSection(memory, default, readOnly: false, inMemory: true);
        section.Initialize();
        return section;
    }

    /// <summary>
    /// A read-only view of the same memory as an in-memory section, as Sentinel sees the engine's: the tests' guardian
    /// reads it while the engine writes it.
    /// </summary>
    public KeyLedgerSection ReadOnlyView()
    {
        ThrowIfDisposed();
        if (!_inMemory)
        {
            throw new InvalidOperationException(
                "Only an in-memory section has a read-only twin; a real one is duplicated for Sentinel."
            );
        }

        return new KeyLedgerSection(_view, default, readOnly: true, inMemory: false);
    }

    /// <summary>
    /// Duplicates the section handle as inheritable and read-only, to pass to Sentinel in
    /// <c>PROC_THREAD_ATTRIBUTE_HANDLE_LIST</c>. The caller closes it after the launch.
    /// </summary>
    public nint DuplicateForGuardian()
    {
        ThrowIfDisposed();
        if (_inMemory || IsReadOnly)
        {
            throw new InvalidOperationException(
                "Only the engine's shared section can be given to Sentinel."
            );
        }

        HANDLE duplicate;
        var process = PInvoke.GetCurrentProcess();
        if (
            !PInvoke.DuplicateHandle(
                process,
                _mapping,
                process,
                &duplicate,
                FileMapRead,
                true,
                default
            )
        )
        {
            throw new Win32Exception(Marshal.GetLastPInvokeError());
        }

        return (nint)duplicate.Value;
    }

    /// <summary>Raises the generation (only the emergency releaser, under the gate) and returns the new value.</summary>
    public ulong IncrementGeneration()
    {
        ThrowIfReadOnly();
        return (ulong)Interlocked.Increment(ref *(long*)(_view + KeyLedgerLayout.GenerationOffset));
    }

    /// <summary>Writes the engine heartbeat.</summary>
    /// <param name="ticks">Now, in the engine clock's ticks.</param>
    public void WriteHeartbeat(long ticks)
    {
        ThrowIfReadOnly();
        Volatile.Write(ref *(long*)(_view + KeyLedgerLayout.HeartbeatOffset), ticks);
    }

    /// <summary>Sets header marks.</summary>
    /// <param name="marks">Marks to set.</param>
    public void SetMarks(LedgerMarks marks) => UpdateMarks(marks, set: true);

    /// <summary>Clears header marks.</summary>
    /// <param name="marks">Marks to clear.</param>
    public void ClearMarks(LedgerMarks marks) => UpdateMarks(marks, set: false);

    /// <summary>Records a key as <see cref="LedgerSlotState.DownPending"/> (or adds a reference) before sending it.</summary>
    /// <param name="key">The key.</param>
    /// <param name="slot">The slot.</param>
    /// <returns><see langword="false"/> when the 128 slots are full: the press is refused.</returns>
    public bool TryBeginDown(PhysicalKey key, out int slot)
    {
        ThrowIfReadOnly();
        BeginWrite();
        try
        {
            if (Find(key, out slot))
            {
                SetRefCount(slot, (ushort)(RefCount(slot) + 1));
                if (State(slot) == LedgerSlotState.ReleasePending)
                {
                    SetState(slot, LedgerSlotState.DownPending);
                }

                return true;
            }

            for (var i = 0; i < KeyLedgerLayout.SlotCount; i++)
            {
                if (State(i) != LedgerSlotState.Free)
                {
                    continue;
                }

                var at = Slot(i);
                *(ushort*)(at + KeyFieldOffset) = key.Vk;
                *(ushort*)(at + ScanFieldOffset) = key.Scan;
                *(at + FlagsFieldOffset) = (byte)key.Attributes;
                *(ushort*)(at + RefCountFieldOffset) = 1;

                // The state goes last: a slot that is not free always names a complete key.
                SetState(i, LedgerSlotState.DownPending);
                slot = i;
                return true;
            }

            slot = -1;
            return false;
        }
        finally
        {
            EndWrite();
        }
    }

    /// <summary>Marks the slot <see cref="LedgerSlotState.Down"/> after <c>SendInput(down)</c>.</summary>
    /// <param name="slot">The slot.</param>
    public void CommitDown(int slot)
    {
        ThrowIfReadOnly();
        CheckSlot(slot);
        BeginWrite();
        try
        {
            if (State(slot) == LedgerSlotState.DownPending)
            {
                SetState(slot, LedgerSlotState.Down);
            }
        }
        finally
        {
            EndWrite();
        }
    }

    /// <summary>Finds the slot of a key before sending its release.</summary>
    /// <param name="key">The key, with the attributes it was pressed with.</param>
    /// <param name="slot">The slot.</param>
    public bool TryFindDown(PhysicalKey key, out int slot)
    {
        ThrowIfDisposed();
        return Find(key, out slot);
    }

    /// <summary>Drops a reference after <c>SendInput(up)</c>; the slot becomes free at zero.</summary>
    /// <param name="slot">The slot.</param>
    public void CommitUp(int slot)
    {
        ThrowIfReadOnly();
        CheckSlot(slot);
        BeginWrite();
        try
        {
            var state = State(slot);
            if (state == LedgerSlotState.Free)
            {
                return;
            }

            var count = RefCount(slot);
            if (state == LedgerSlotState.ReleasePending || count <= 1)
            {
                Free(slot);
            }
            else
            {
                SetRefCount(slot, (ushort)(count - 1));
            }
        }
        finally
        {
            EndWrite();
        }
    }

    /// <summary>Marks a release that could not be sent (secure desktop).</summary>
    /// <param name="slot">The slot.</param>
    public void MarkReleasePending(int slot)
    {
        ThrowIfReadOnly();
        CheckSlot(slot);
        BeginWrite();
        try
        {
            if (State(slot) != LedgerSlotState.Free)
            {
                SetState(slot, LedgerSlotState.ReleasePending);
            }
        }
        finally
        {
            EndWrite();
        }
    }

    /// <summary>Records the mouse buttons down.</summary>
    /// <param name="buttons">The buttons.</param>
    public void SetMouseButtons(LedgerMouseButtons buttons)
    {
        ThrowIfReadOnly();
        BeginWrite();
        try
        {
            Volatile.Write(ref *(_view + KeyLedgerLayout.MouseButtonsOffset), (byte)buttons);
        }
        finally
        {
            EndWrite();
        }
    }

    /// <summary>A consistent copy (retries while the sequence number changes).</summary>
    public LedgerSnapshot Snapshot()
    {
        ThrowIfDisposed();
        LedgerSnapshot copy;
        var round = 0;
        while (true)
        {
            var before = Volatile.Read(ref SequenceField);
            copy = Copy();
            Interlocked.MemoryBarrier();
            var after = Volatile.Read(ref SequenceField);
            if ((before == after && (before & 1) == 0) || ++round >= SnapshotRounds)
            {
                return copy;
            }

            Thread.SpinWait(16);
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        if (_inMemory)
        {
            NativeMemory.Free(_view);
            return;
        }

        if (_mapping.IsNull)
        {
            // The read-only twin of an in-memory section owns nothing.
            return;
        }

        PInvoke.UnmapViewOfFile(new MEMORY_MAPPED_VIEW_ADDRESS(_view));
        PInvoke.CloseHandle(_mapping);
    }

    /// <summary>
    /// Undoes a <see cref="TryBeginDown"/> whose key never reached <c>SendInput</c>: drops that reference only, so a key
    /// that was already down (another press, or a release still pending) stays recorded; a slot the undone press
    /// created goes back to free.
    /// </summary>
    internal void RollbackDown(int slot)
    {
        ThrowIfReadOnly();
        CheckSlot(slot);
        BeginWrite();
        try
        {
            if (State(slot) == LedgerSlotState.Free)
            {
                return;
            }

            var count = RefCount(slot);
            if (count <= 1)
            {
                Free(slot);
            }
            else
            {
                SetRefCount(slot, (ushort)(count - 1));
            }
        }
        finally
        {
            EndWrite();
        }
    }

    /// <summary>Frees a slot whatever its reference count (the emergency release and Sentinel release every holder).</summary>
    internal void ForceFree(int slot)
    {
        ThrowIfReadOnly();
        CheckSlot(slot);
        BeginWrite();
        try
        {
            Free(slot);
        }
        finally
        {
            EndWrite();
        }
    }

    private void Initialize()
    {
        *(uint*)(_view + KeyLedgerLayout.MagicOffset) = KeyLedgerLayout.Magic;
        *(ushort*)(_view + KeyLedgerLayout.LayoutVersionOffset) = KeyLedgerLayout.LayoutVersion;
        *(long*)(_view + KeyLedgerLayout.GenerationOffset) = 1;
        Interlocked.MemoryBarrier();
    }

    private bool HasValidHeader() =>
        *(uint*)(_view + KeyLedgerLayout.MagicOffset) == KeyLedgerLayout.Magic
        && *(ushort*)(_view + KeyLedgerLayout.LayoutVersionOffset) == KeyLedgerLayout.LayoutVersion;

    private LedgerSnapshot Copy()
    {
        var slots = ImmutableArray.CreateBuilder<LedgerSlot>();
        for (var i = 0; i < KeyLedgerLayout.SlotCount; i++)
        {
            var state = State(i);
            if (state == LedgerSlotState.Free)
            {
                continue;
            }

            var at = Slot(i);
            slots.Add(
                new LedgerSlot(
                    new PhysicalKey(
                        *(ushort*)(at + KeyFieldOffset),
                        *(ushort*)(at + ScanFieldOffset),
                        (LedgerKeyAttributes)(*(at + FlagsFieldOffset))
                    ),
                    state,
                    *(ushort*)(at + RefCountFieldOffset)
                )
            );
        }

        return new LedgerSnapshot(
            *(ushort*)(_view + KeyLedgerLayout.LayoutVersionOffset),
            (LedgerMarks)(ushort)((uint)Volatile.Read(ref HeaderWord) >> 16),
            (ulong)Volatile.Read(ref SequenceField),
            Volatile.Read(ref *(long*)(_view + KeyLedgerLayout.HeartbeatOffset)),
            (ulong)Volatile.Read(ref *(long*)(_view + KeyLedgerLayout.GenerationOffset)),
            slots.ToImmutable(),
            (LedgerMouseButtons)Volatile.Read(ref *(_view + KeyLedgerLayout.MouseButtonsOffset))
        );
    }

    private bool Find(PhysicalKey key, out int slot)
    {
        for (var i = 0; i < KeyLedgerLayout.SlotCount; i++)
        {
            if (State(i) == LedgerSlotState.Free)
            {
                continue;
            }

            var at = Slot(i);
            if (
                *(ushort*)(at + KeyFieldOffset) == key.Vk
                && *(ushort*)(at + ScanFieldOffset) == key.Scan
                && (LedgerKeyAttributes)(*(at + FlagsFieldOffset)) == key.Attributes
            )
            {
                slot = i;
                return true;
            }
        }

        slot = -1;
        return false;
    }

    private void UpdateMarks(LedgerMarks marks, bool set)
    {
        ThrowIfReadOnly();
        var bits = (int)((uint)(ushort)marks << 16);
        int old;
        int updated;
        do
        {
            old = Volatile.Read(ref HeaderWord);
            updated = set ? old | bits : old & ~bits;
        } while (Interlocked.CompareExchange(ref HeaderWord, updated, old) != old);
    }

    private void BeginWrite() => Interlocked.Increment(ref SequenceField);

    private void EndWrite() => Interlocked.Increment(ref SequenceField);

    private byte* Slot(int index) =>
        _view + KeyLedgerLayout.SlotsOffset + (index * KeyLedgerLayout.SlotSize);

    private LedgerSlotState State(int index) =>
        (LedgerSlotState)Volatile.Read(ref *(Slot(index) + StateFieldOffset));

    private void SetState(int index, LedgerSlotState state) =>
        Volatile.Write(ref *(Slot(index) + StateFieldOffset), (byte)state);

    private ushort RefCount(int index) => *(ushort*)(Slot(index) + RefCountFieldOffset);

    private void SetRefCount(int index, ushort count) =>
        *(ushort*)(Slot(index) + RefCountFieldOffset) = count;

    private void Free(int index)
    {
        SetState(index, LedgerSlotState.Free);
        var at = Slot(index);
        *(ushort*)(at + KeyFieldOffset) = 0;
        *(ushort*)(at + ScanFieldOffset) = 0;
        *(at + FlagsFieldOffset) = 0;
        *(ushort*)(at + RefCountFieldOffset) = 0;
    }

    private static void CheckSlot(int slot) =>
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(
            (uint)slot,
            (uint)KeyLedgerLayout.SlotCount,
            nameof(slot)
        );

    private void ThrowIfDisposed() =>
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);

    private void ThrowIfReadOnly()
    {
        ThrowIfDisposed();
        if (IsReadOnly)
        {
            throw new InvalidOperationException("This view of the key ledger is read-only.");
        }
    }
}
