namespace Clicalo.Platform.Core.KeyLedger;

/// <summary>
/// The byte layout of the physical ledger v2 (blueprint §7.4, ADR-0004), shared by the engine, Sentinel and the tests.
/// A persisted-in-memory contract between two executables: changing it means a new <see cref="LayoutVersion"/>.
/// <code>
/// 0x000 uint32 Magic 'CLKL' · 0x004 uint16 LayoutVersion = 2
/// 0x006 uint16 Marks (CleanShutdown, NoRelaunch, EngineAlive, EmergencyRestart)
/// 0x008 uint64 Sequence (Interlocked++) · 0x010 int64 LastHeartbeatTicks
/// 0x018 uint64 EngineGeneration (Interlocked; only goes up)
/// 0x020 Slot[128] { uint16 Vk; uint16 Scan; uint8 KeyAttributes; uint8 State; uint16 RefCount }
/// 0x420 uint8 MouseButtonsDown (L/R/M/X1/X2)
/// </code>
/// </summary>
public static class KeyLedgerLayout
{
    /// <summary>Size of the unnamed section: 4 KiB.</summary>
    public const int SectionSize = 4096;

    /// <summary><c>'CLKL'</c> read as a little-endian 32-bit integer.</summary>
    public const uint Magic = 0x4C4B4C43;

    /// <summary>Layout version 2: generation, injection mode per slot and marks.</summary>
    public const ushort LayoutVersion = 2;

    /// <summary>Offset of <see cref="Magic"/>.</summary>
    public const int MagicOffset = 0x000;

    /// <summary>Offset of the layout version.</summary>
    public const int LayoutVersionOffset = 0x004;

    /// <summary>Offset of the marks.</summary>
    public const int MarksOffset = 0x006;

    /// <summary>Offset of the sequence number.</summary>
    public const int SequenceOffset = 0x008;

    /// <summary>Offset of the last heartbeat.</summary>
    public const int HeartbeatOffset = 0x010;

    /// <summary>Offset of the engine generation.</summary>
    public const int GenerationOffset = 0x018;

    /// <summary>Offset of the first slot.</summary>
    public const int SlotsOffset = 0x020;

    /// <summary>Number of key slots.</summary>
    public const int SlotCount = 128;

    /// <summary>Size of one slot in bytes.</summary>
    public const int SlotSize = 8;

    /// <summary>Offset of the mouse buttons byte.</summary>
    public const int MouseButtonsOffset = 0x420;
}
