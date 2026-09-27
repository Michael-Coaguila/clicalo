using Clicalo.Domain.Geometry;
using Clicalo.Domain.KeySafety;
using Clicalo.Domain.Library;

namespace Clicalo.Application.Ports;

/// <summary>
/// The only way the product sends input (blueprint §4.4, ADR-0004). Implemented once, in Platform.Windows, over
/// <c>Clicalo.Platform.Core.Injection.InjectionGate</c>: each call compares the generation, records presses in the
/// physical ledger before sending them and commits releases after (<c>BeginDown → SendInput → Commit</c>), all under
/// the gate. Only Application.Engine, Platform.Windows and App depend on it (ArchUnit). Called only from the engine
/// thread.
/// </summary>
public interface IInputInjector
{
    /// <summary>Sends key and button events in the mode each key carries, with the menu mask before Alt or Win releases.</summary>
    /// <param name="generation">The caller's generation.</param>
    /// <param name="events">The events, in order.</param>
    InjectionResult Send(EngineGeneration generation, ReadOnlySpan<InjectedEvent> events);

    /// <summary>Types a text as Unicode (<c>KEYEVENTF_UNICODE</c>); line breaks go as Enter (EJE-008).</summary>
    /// <param name="generation">The caller's generation.</param>
    /// <param name="text">The text, in a buffer the caller wipes afterwards.</param>
    InjectionResult TypeText(EngineGeneration generation, ReadOnlySpan<char> text);

    /// <summary>
    /// Moves the pointer to <paramref name="target"/> and performs a click or one scroll step there (EJE-009); a drag
    /// holds the left button through <see cref="Send"/> instead.
    /// </summary>
    /// <param name="generation">The caller's generation.</param>
    /// <param name="operation">The mouse action.</param>
    /// <param name="target">Where, in physical pixels; <see langword="null"/> for the centre of the foreground client area.</param>
    InjectionResult Mouse(EngineGeneration generation, MouseOp operation, PhysicalPoint? target);

    /// <summary>
    /// Sends again the key ups the physical ledger keeps pending because the secure desktop refused them (blueprint
    /// §7.6, INV-3), whoever sent them; the engine calls it when the input desktop is back.
    /// </summary>
    /// <param name="generation">The caller's generation.</param>
    InjectionResult ReleasePending(EngineGeneration generation);
}
